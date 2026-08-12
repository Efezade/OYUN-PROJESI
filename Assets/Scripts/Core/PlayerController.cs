using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Oyuncu karakterini hex karolar üzerinde hareket ettirir.
    /// A* yoluyla ilerler, her adımda FogOfWar'ı günceller (dinamik görüş baloncuğu).
    /// Kule ile kalıcı sis açma artık WatchtowerManager'ın işi (yakınlık istemi + onay).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager  _gridManager;
        [SerializeField] private FogOfWarManager _fogManager;

        [Header("Hareket")]
        [Tooltip("Yürüme hızı (m/sn). Walk klibinin temposuyla birlikte ayarlanır — hızlanırsa " +
                 "ayak kayar (CharacterAnimationImporter.walkState.speed).")]
        [SerializeField] private float _moveSpeed    = 3.5f;
        [Tooltip("Karakterin yüzeye göre dikey ofseti (ayak payı). Ayağı-orijinde bake edilmiş " +
                 "modelde TileHeight (clearance 0 → ayak yüzeye basar); kapsül fallback'inde daha büyük.")]
        [SerializeField] private float _heightOffset = 0.15f;

        [Header("Görüş")]
        [Tooltip("Kam'ın bulunduğu karodan kaç karo uzağa kadar bulutsuz gördüğü (hex adımı). " +
                 "Keşif KALICI: baloncuk ilerledikçe arkası yeniden bulutlanmaz, iz açık kalır.\n\n" +
                 "2026-08-12: 1 → 3 (kullanıcı isteği, sis 3 kat daha fazla açılsın). Gece bu değer " +
                 "FogOfWarManager._nightVisionMultiplier ile çarpılır, yani gece görüşü de 3 katına çıkar.")]
        [SerializeField] private int _visionRange = 3;

        [Header("Başlangıç Koordinatı")]
        [SerializeField] private HexCoordinate _startCoord;

        public HexCoordinate CurrentCoord { get; private set; }
        public bool          IsMoving     { get; private set; }

        /// <summary>Yürüme hızı çarpanı (mağaza "Hızlı Yürüme" iksiri/çizmeleri buradan artırır).
        /// PlayerBuffs yönetir; 1 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        // Faz 1.4 AP/Zaman motoru bu event'i dinleyecek
        public event Action<HexCoordinate> OnMoved;

        // Harita üreticisi (ChapterMapGenerator, execution order -90) Start'ından ÖNCE çalışıp
        // Initialize'ı çağırıyor. Bu bayrak olmadan buradaki Start, üreticinin hesapladığı
        // başlangıcı SERİLEŞMİŞ eski koordinatla EZİYORDU (2026-08-12 hata raporu: "kıyıda köşede,
        // kapalı yerde doğuyorum, hareket edemiyorum" — oyuncu her seferinde Inspector'daki
        // sabit (3,4)'e konuyordu, organik haritada orası denizin içi/cep oluyordu).
        private bool _initialized;

        private void Start()
        {
            if (_gridManager == null) { Debug.LogError("[PlayerController] _gridManager NULL! Faz 1.3'ü yeniden çalıştır."); return; }
            if (_fogManager  == null) { Debug.LogError("[PlayerController] _fogManager NULL! Faz 1.3'ü yeniden çalıştır."); return; }
            if (_initialized) return;                 // üretici zaten yerleştirdi — EZME
            Initialize(_startCoord);
        }

        public void Initialize(HexCoordinate startCoord)
        {
            _initialized = true;

            // GÜVENLİK AĞI: verilen karo yoksa ya da yürünemezse en yakın YÜRÜNÜR karoya kay.
            // Üretici doğru koordinatı veriyor; bu, elle atanmış/bayat koordinatlara karşı sigorta —
            // oyuncunun hiç hareket edemediği bir karoda başlaması her şeyi bitiriyor.
            if (!IsUsableStart(startCoord) && TryFindNearestWalkable(startCoord, out HexCoordinate fixedCoord))
            {
                Debug.LogWarning($"[PlayerController] Baslangic {startCoord} kullanilamaz " +
                                 $"(karo yok ya da yurunemez) → en yakin yurunur karo {fixedCoord}.");
                startCoord = fixedCoord;
            }

            CurrentCoord = startCoord;

            if (_gridManager.TryGetCell(startCoord, out HexCell cell))
                transform.position = GroundedAt(cell);
            else
                Debug.LogError($"[PlayerController] Baslangic koordinati {startCoord} grid'de YOK — " +
                               "haritada hic yurunur karo bulunamadi.");

            _fogManager.UpdateFogAround(transform.position, _visionRange);
            _lastFogSample = transform.position;
        }

        private bool IsUsableStart(HexCoordinate c)
            => _gridManager.TryGetCell(c, out HexCell cell) && cell.IsWalkable;

        /// <summary>Verilen koordinata en yakın yürünür karo (hex mesafesine göre).</summary>
        private bool TryFindNearestWalkable(HexCoordinate from, out HexCoordinate result)
        {
            result = from;
            if (_gridManager.Cells == null) return false;

            int best = int.MaxValue;
            bool found = false;
            foreach (var kv in _gridManager.Cells)
            {
                if (!kv.Value.IsWalkable) continue;
                int d = from.DistanceTo(kv.Key);
                if (d >= best) continue;
                best = d; result = kv.Key; found = true;
            }
            return found;
        }

        // Karakter hareket ettikçe sisi CANLI konumla güncelle → saydamlık sürekli/akışkan değişir
        // (karoya varınca pat diye değil). Dururken güncelleme yok (konum değişmez).
        private Vector3 _lastFogSample = new Vector3(float.MaxValue, 0f, 0f);
        private void Update()
        {
            if (_fogManager == null) return;
            if ((transform.position - _lastFogSample).sqrMagnitude < 0.00005f) return;
            _fogManager.UpdateFogAround(transform.position, _visionRange);
            _lastFogSample = transform.position;
        }

        public void MoveAlongPath(List<HexCell> path)
        {
            if (IsMoving || path == null || path.Count < 2) return;
            StartCoroutine(MoveCoroutine(path));
        }

        private IEnumerator MoveCoroutine(List<HexCell> path)
        {
            IsMoving = true;

            for (int i = 1; i < path.Count; i++)
            {
                HexCell from   = path[i - 1];
                HexCell target = path[i];

                Vector3 fromXZ   = new Vector3(from.WorldPosition.x,   0f, from.WorldPosition.z);
                Vector3 targetXZ = new Vector3(target.WorldPosition.x, 0f, target.WorldPosition.z);
                float   fromY    = SurfaceY(from);
                float   toY      = SurfaceY(target);
                float   span     = Vector3.Distance(fromXZ, targetXZ);

                // Yatayda hedefe ilerle; Y'yi iki karo YÜZEYİ arasında enterpole et → güverteden
                // güverteye pürüzsüz yürüme. (Eski her-kare RaycastAll rastgele en yüksek collider'a
                // biniyordu → köprü geçişinde kemer üstüne çıkıp havada kalıyordu. Artık ayak yüzeyde.)
                while (HorizontalSqrDistance(transform.position, targetXZ) > 0.0001f)
                {
                    Vector3 curXZ  = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 nextXZ = Vector3.MoveTowards(curXZ, targetXZ, _moveSpeed * Mathf.Max(0.1f, SpeedMultiplier) * Time.deltaTime);

                    float k = span > 0.0001f ? 1f - Vector3.Distance(nextXZ, targetXZ) / span : 1f;
                    float y = Mathf.Lerp(fromY, toY, Mathf.Clamp01(k));
                    transform.position = new Vector3(nextXZ.x, y, nextXZ.z);
                    yield return null;
                }

                transform.position = new Vector3(targetXZ.x, toY, targetXZ.z);
                CurrentCoord       = target.Coordinate;

                // Sis Update()'te canlı konumla sürekli güncellenir; burada sadece tur/olay eventi.
                OnMoved?.Invoke(CurrentCoord);
            }

            IsMoving = false;
        }

        /// <summary>Görüş baloncuğunu mevcut konumda yeniden kurar (sis kilidi değişince —
        /// örn. savaştan dönünce ya da kule açılmamış adaya dönünce WatchtowerManager çağırır).</summary>
        public void RefreshVision()
        {
            if (_fogManager != null) _fogManager.UpdateFogAround(transform.position, _visionRange);
        }

        // Karonun YÜRÜME yüzeyinin dünya Y'si + karakterin ayak payı (clearance).
        // clearance = _heightOffset - TileHeight → ayağı-orijinde bake'li modelde 0 (ayak yüzeye basar).
        private float SurfaceY(HexCell cell) =>
            cell.WorldPosition.y + cell.SurfaceHeight + (_heightOffset - HexMetrics.TileHeight);

        private Vector3 GroundedAt(HexCell cell) =>
            new Vector3(cell.WorldPosition.x, SurfaceY(cell), cell.WorldPosition.z);

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
