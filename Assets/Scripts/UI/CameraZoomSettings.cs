using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Kamera yakınlığı — OVERWORLD ve SAVAŞ için AYRI iki ayar (kullanıcı isteği 2026-08-12:
    /// "her ikisine de zoom, yakınlığı test edebileyim").
    ///
    /// Kamera ORTOGRAFİK olduğu için yakınlık = <c>Camera.orthographicSize</c>. Ölçek çarpan olarak
    /// tutulur (1.0 = mevcut görünüm, 0.5 = iki kat yakın), böylece "0.5x'i sevdim" denince tek bir
    /// sayıyı sabitleyip ayarı kaldırmak yeterli olur.
    ///
    /// Değerler PlayerPrefs'te kalıcı; durum değişince (overworld ↔ savaş) doğru ölçek uygulanır.
    /// Oyunu bozmaz: yalnız kameranın görüş alanını değiştirir, hiçbir kural/mesafe etkilenmez.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class CameraZoomSettings : MonoBehaviour
    {
        private const string OverworldKey = "TacticalRPG.Zoom.Overworld.v2";
        private const string CombatKey    = "TacticalRPG.Zoom.Combat.v2";

        [SerializeField] private Camera           _camera;
        [Tooltip("Atanmazsa hep overworld ölçeği kullanılır.")]
        [SerializeField] private GameStateManager _state;
        [Tooltip("Savaşta arenayı kadrajlamak için gerekli — atanmazsa kamera overworld'de " +
                 "kaldığı yerde kalır ve arena kadrajın dışında görünür.")]
        [SerializeField] private HexGridManager    _grid;

        [Header("Taban görüş alanı (ortografik size)")]
        [Tooltip("Ölçek 1.0 iken overworld'de kullanılacak orthographicSize. Kurulumdaki değer 10.")]
        [SerializeField, Min(1f)] private float _overworldBaseSize = 10f;

        [Tooltip("Savaşta arena EKRANA SIĞDIRILIR; bu değer yalnız arena ölçülemezse yedek olarak " +
                 "kullanılır. Normalde kadraj otomatik hesaplanır.")]
        [SerializeField, Min(1f)] private float _combatBaseSize = 7f;

        [Tooltip("Arena kadrajının kenar payı (1.0 = tam sığar, 1.15 = %15 nefes payı).")]
        [SerializeField, Range(1f, 2f)] private float _combatPadding = 1.30f;

        [Header("Ölçek sınırları")]
        [SerializeField, Min(0.05f)] private float _minScale = 0.3f;
        [SerializeField, Min(0.1f)]  private float _maxScale = 2f;

        /// <summary>Overworld yakınlık çarpanı (küçük = daha yakın).</summary>
        public float OverworldScale { get; private set; } = 0.65f;

        /// <summary>Savaş yakınlık çarpanı. 1.0 = arena kenar payıyla tam sığar.</summary>
        public float CombatScale { get; private set; } = 1.15f;

        public float MinScale => _minScale;
        public float MaxScale => _maxScale;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            // Varsayılanlar bilerek 1.0 DEĞİL: kullanıcı "fazlaca uzaktan bakıyoruz" dedi,
            // açılış hâli zaten bir tık yakın olsun; slider'dan istediği yere çekebilir.
            // NOT: anahtarlar ".v2" — eski kayitli 0.80 savas degeri kadraji fazla sikiyordu
            // (karolar ekran kenarina degiyordu, 2026-08-12 ekran goruntusu). Surum atlanarak
            // yeni varsayilanlar devreye alindi.
            OverworldScale = PlayerPrefs.GetFloat(OverworldKey, 0.65f);
            CombatScale    = PlayerPrefs.GetFloat(CombatKey,    1.15f);
        }

        private void OnEnable()
        {
            if (_state != null) _state.OnStateChanged += HandleStateChanged;
            // Arena üretilince hücreler değişir → kadraj yeniden hesaplanmalı.
            if (_grid  != null) _grid.OnGridRegenerated += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
            if (_grid  != null) _grid.OnGridRegenerated -= Apply;
        }

        private void HandleStateChanged(GameState state) => Apply();

        public void SetOverworldScale(float scale)
        {
            OverworldScale = Mathf.Clamp(scale, _minScale, _maxScale);
            PlayerPrefs.SetFloat(OverworldKey, OverworldScale);
            Apply();
        }

        public void SetCombatScale(float scale)
        {
            CombatScale = Mathf.Clamp(scale, _minScale, _maxScale);
            PlayerPrefs.SetFloat(CombatKey, CombatScale);
            Apply();
        }

        /// <summary>Şu anki duruma göre kamerayı ayarlar.
        ///
        /// SAVAŞTA kamera ARENAYI KADRAJLAR. Bunun ayrıca yapılması şart: <c>CameraFollow</c> hedefi
        /// (oyuncu jetonu) savaşta GİZLENDİĞİ için takip durur ve kamera overworld'de kaldığı yerde
        /// donar — arena ekranın dışında/köşesinde kalırdı (2026-08-12 hata raporu).</summary>
        public void Apply()
        {
            if (_camera == null) return;
            bool inCombat = _state != null &&
                            (_state.State == GameState.Combat || _state.State == GameState.Deployment);

            if (!inCombat)
            {
                SetBaseSize(_overworldBaseSize * OverworldScale);
                return;
            }

            if (TryFrameArena(out Vector3 center, out float fitSize))
            {
                // Kamera AÇISINA dokunulmaz: konum = merkez − forward × mesafe (CameraFollow ile aynı kural).
                float dist = Mathf.Max(20f, fitSize * 4f);          // ortografikte yalnız kırpma için
                _camera.transform.position = center - _camera.transform.forward * dist;
                _shakeBase = _camera.transform.position;
                SetBaseSize(fitSize * CombatScale);
            }
            else
            {
                SetBaseSize(_combatBaseSize * CombatScale);
            }
        }

        // ── Sinematik uzaklaştırma + sarsıntı (Kam'ın büyüleri) ─────────────
        // Kullanıcı kuralı 2026-08-13: "skill seçerse Kam, kamera uzaklaşsın ve ilahi bir görünüm
        // olsun". Kamerayı SAHİPLENEN sınıf burası olduğu için çarpan da burada uygulanır —
        // büyü sistemi orthographicSize'a doğrudan dokunsaydı, ayarlar menüsünden gelen ölçek ile
        // çakışır ve savaş bitince kamera yanlış yakınlıkta kalırdı.

        [Header("Sinematik (büyü hedeflemesi)")]
        [Tooltip("Sinematik çarpanın hedefe ulaşma hızı (çarpan/sn).")]
        [SerializeField, Min(0.1f)] private float _cinematicSpeed = 2.2f;

        private float   _baseSize    = 10f;   // duruma göre hesaplanan ölçek (çarpansız)
        private float   _cineCurrent = 1f;
        private float   _cineTarget  = 1f;
        private Vector3 _shakeBase;
        private Coroutine _shakeCo;

        /// <summary>1 = normal. Büyü hedeflemesinde >1 verilir; bitince tekrar 1.</summary>
        public void SetCinematicZoom(float multiplier)
            => _cineTarget = Mathf.Clamp(multiplier, 0.5f, 3f);

        private void SetBaseSize(float size)
        {
            _baseSize = Mathf.Max(0.5f, size);
            if (_camera != null) _camera.orthographicSize = _baseSize * _cineCurrent;
        }

        private void LateUpdate()
        {
            if (_camera == null || Mathf.Approximately(_cineCurrent, _cineTarget)) return;
            _cineCurrent = Mathf.MoveTowards(_cineCurrent, _cineTarget, _cinematicSpeed * Time.deltaTime);
            _camera.orthographicSize = Mathf.Max(0.5f, _baseSize * _cineCurrent);
        }

        /// <summary>Kamerayı kısa süre sarsar (meteor çarpması). Ortografik kamerada konum
        /// kaydırması görüntüyü kaydırır — ölçeğe dokunmaz.</summary>
        public void Shake(float amount, float seconds)
        {
            if (_camera == null || amount <= 0f || seconds <= 0f) return;
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine(amount, seconds));
        }

        private System.Collections.IEnumerator ShakeRoutine(float amount, float seconds)
        {
            Vector3 basePos = _camera.transform.position;
            _shakeBase = basePos;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float fade = 1f - t / seconds;
                _camera.transform.position = basePos + Random.insideUnitSphere * (amount * fade);
                yield return null;
            }
            _camera.transform.position = _shakeBase;
            _shakeCo = null;
        }

        /// <summary>Arenanın merkezini ve TAM SIĞACAK ortografik boyutu hesaplar.
        /// Hücre köşeleri kameranın kendi eksenlerine izdüşürülür → kamera açısı ne olursa olsun
        /// (izometrik 30°/45°) doğru sonuç verir.</summary>
        private bool TryFrameArena(out Vector3 center, out float fitSize)
        {
            center = Vector3.zero; fitSize = 0f;
            if (_grid == null || _grid.Cells == null || _grid.Cells.Count == 0) return false;

            Vector3 right = _camera.transform.right, up = _camera.transform.up;
            float minR = float.MaxValue, maxR = float.MinValue;
            float minU = float.MaxValue, maxU = float.MinValue;
            Vector3 sum = Vector3.zero;
            int n = 0;

            foreach (var kv in _grid.Cells)
            {
                Vector3 p = kv.Value.WorldPosition;
                sum += p; n++;
                // Karonun yarıçapını da hesaba kat, kenardaki karo yarım kalmasın.
                float r = Vector3.Dot(p, right), u = Vector3.Dot(p, up);
                if (r < minR) minR = r; if (r > maxR) maxR = r;
                if (u < minU) minU = u; if (u > maxU) maxU = u;
            }
            if (n == 0) return false;

            center = sum / n;
            const float cellPad = 1.1f;                       // karo yarıçapı payı
            float halfWidth  = (maxR - minR) * 0.5f + cellPad;
            float halfHeight = (maxU - minU) * 0.5f + cellPad;

            float aspect = _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
            fitSize = Mathf.Max(halfHeight, halfWidth / aspect) * _combatPadding;
            return true;
        }
    }
}
