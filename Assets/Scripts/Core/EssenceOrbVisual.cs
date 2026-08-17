using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// HARİTADAKİ ÖZ KÜRESİNİN CANLANDIRMASI — karonun biraz üstünde duran, içi hareketli,
    /// kor gibi parlayan küre (kullanıcı isteği 2026-08-17).
    ///
    /// Küre üç katmandan oluşur ve parçalar ADLARINDAN bulunur (serileşmiş referans yok →
    /// üreteç değişse de bağ kopmaz; boş kalan Inspector referansı en sık sessiz kırılma sebebi):
    ///   • "<see cref="CoreName"/>" — ortadaki kor. Nabız atar.
    ///   • "<see cref="ShellName"/>" — dıştaki yarı saydam kabuk. Yavaşça nefes alır.
    ///   • "<see cref="MotePrefix"/>*" — kabuğun İÇİNDE dönen parçacıklar (alev dilleri, su
    ///     kabarcıkları, toz zerreleri, taş kırıkları, yapraklar). Asıl "hareketli" his bunlardan gelir.
    ///
    /// Biçime göre karakter (<see cref="EssenceOrbShape"/>) tek bir parametre setiyle verilir;
    /// beş ayrı animasyon sınıfı yazmak yerine aynı denklemin katsayıları değişir.
    /// </summary>
    public class EssenceOrbVisual : MonoBehaviour
    {
        public const string CoreName   = "Core";
        public const string ShellName  = "Shell";
        public const string MotePrefix = "Mote";

        [Header("Biçim")]
        [SerializeField] private EssenceOrbShape _shape = EssenceOrbShape.Kristal;

        [Header("Küre hareketi")]
        [Tooltip("Küre kendi ekseninde saniyede kaç derece dönsün.")]
        [SerializeField] private float _spinSpeed = 35f;
        [Tooltip("Havada süzülme genliği (dünya birimi) ve hızı (tam döngü/sn).")]
        [SerializeField] private float _bobAmplitude = 0.055f;
        [SerializeField] private float _bobSpeed     = 0.55f;

        [Header("İç parçacıklar")]
        [Tooltip("Parçacıkların kürenin içindeki yörünge hızı (tur/sn).")]
        [SerializeField] private float _orbitSpeed = 0.35f;
        [Tooltip("Parçacıkların kendi etrafında takla atma hızı (derece/sn). Yaprak/kristal için.")]
        [SerializeField] private float _tumbleSpeed = 60f;
        [Tooltip("Boyut titremesi (0 = sabit, 1 = çok oynak). Alev ve toz için yüksek.")]
        [SerializeField, Range(0f, 1f)] private float _flicker = 0.15f;
        [Tooltip("Parçacıkların yukarı süzülme hızı (alev/duman hissi). 0 = süzülme yok.")]
        [SerializeField] private float _rise = 0f;

        [Header("Işık")]
        [Tooltip("Korun bir tam nabız döngüsü (sn). 0 = nabız yok.")]
        [SerializeField] private float _pulsePeriod = 1.8f;
        [SerializeField] private Vector2 _pulseRange = new(0.7f, 1.6f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

        private Transform   _core, _shell;
        private Transform[] _motes = System.Array.Empty<Transform>();
        private Vector3[]   _moteBasePos;
        private Vector3[]   _moteBaseScale;
        private Vector3[]   _moteAxis;
        private float[]     _motePhase;
        private float[]     _moteSpeed;

        private Renderer[]  _renderers = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock _mpb;

        private Color _color = Color.white;
        private float _glow  = 2f;
        private float _phase;      // her küreye farklı faz → yan yana duranlar senkron olmasın
        private float _baseY;
        private float _fade = 1f;  // toplanma animasyonunda 1 → 0

        private void Awake()
        {
            _mpb   = new MaterialPropertyBlock();
            _phase = Random.value * 10f;
            _baseY = transform.localPosition.y;
            Collect();
        }

        /// <summary>Rengi ve parlaklığı dışarıdan verir (öz türünün rengi). Materyal
        /// DEĞİŞTİRİLMEZ — MaterialPropertyBlock yazılır, böylece tüm küreler tek materyali paylaşır.</summary>
        public void Apply(Color color, float glow, EssenceOrbShape shape)
        {
            _color = color;
            _glow  = Mathf.Max(0f, glow);
            _shape = shape;
            if (_mpb == null) { _mpb = new MaterialPropertyBlock(); Collect(); }

            // Süzülmenin merkezi BURADA alınır, Awake'te DEĞİL: Instantiate sırasında Awake
            // koşarken küre henüz (0,0,0)'da; yerleştiren kod konumu SONRA yazıyor. Awake'teki
            // değer kullanılsaydı Update ilk karede küreyi karonun içine gömerdi.
            _baseY = transform.localPosition.y;
            Paint(1f);
        }

        /// <summary>
        /// TOPLANDI — "karonun ruhu çekiliyor". Küre hüzmeyle birlikte yukarı fırlar, küçülür ve
        /// RENGİNİ KAYBEDER: özün canlı rengi yol boyunca <paramref name="drainTo"/>'ya (kül/siyah)
        /// çekilir. Süre bitince kendini yok eder.
        /// </summary>
        public void PlayCollected(Color drainTo, float seconds = 0.55f, float riseSpeed = 4.5f)
        {
            if (!gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            StartCoroutine(CollectRoutine(drainTo, seconds, riseSpeed));
        }

        private System.Collections.IEnumerator CollectRoutine(Color drainTo, float seconds, float riseSpeed)
        {
            // Süzülmenin merkezini BURADA tazele: çağıran küreyi karonun süs kökünden alıp genel
            // köke taşıyor (karo yok edilirken gösteri kesilmesin diye). Ebeveyn değişince yerel
            // konum da değişir — eski _baseY kullanılsaydı küre bir anda başka bir yüksekliğe atlardı.
            _baseY = transform.localPosition.y;

            Vector3 from  = transform.localScale;
            Color   start = _color;
            float   e     = 0f;

            while (e < seconds)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / seconds);

                transform.localScale = Vector3.Lerp(from, from * 0.1f, k);
                // Hızlanarak yüksel (k² ivme) — hüzmeyle aynı anda göğe gitsin.
                _baseY += Time.deltaTime * riseSpeed * (0.35f + k);
                _color  = Color.Lerp(start, drainTo, k);   // rengi çekilir
                _fade   = 1f - k * k;                      // sonda hızlı söner
                yield return null;
            }
            Destroy(gameObject);
        }

        private void Collect()
        {
            var motes = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (t.name.StartsWith(MotePrefix))     motes.Add(t);
                else if (t.name.StartsWith(CoreName))  _core  = t;
                else if (t.name.StartsWith(ShellName)) _shell = t;
            }

            _motes         = motes.ToArray();
            _moteBasePos   = new Vector3[_motes.Length];
            _moteBaseScale = new Vector3[_motes.Length];
            _moteAxis      = new Vector3[_motes.Length];
            _motePhase     = new float[_motes.Length];
            _moteSpeed     = new float[_motes.Length];

            for (int i = 0; i < _motes.Length; i++)
            {
                _moteBasePos[i]   = _motes[i].localPosition;
                _moteBaseScale[i] = _motes[i].localScale;
                // Yörünge ekseni parçacığın KENDİ konumundan türer → her parçacık farklı düzlemde
                // döner, hepsi tek bir halkada dizilmez.
                _moteAxis[i]  = Vector3.Normalize(new Vector3(
                    Mathf.Sin(i * 2.399963f), 1f + Mathf.Cos(i * 1.61803f) * 0.4f, Mathf.Cos(i * 2.399963f)));
                _motePhase[i] = i * 0.7f;
                _moteSpeed[i] = 1f + (i % 3) * 0.35f;   // hepsi aynı hızda dönmesin
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers) if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            float t = Time.time + _phase;

            // Küre: yerinde dön + havada süzül.
            transform.localRotation = Quaternion.Euler(0f, t * _spinSpeed, 0f);
            Vector3 p = transform.localPosition;
            p.y = _baseY + Mathf.Sin(t * _bobSpeed * Mathf.PI * 2f) * _bobAmplitude;
            transform.localPosition = p;

            AnimateMotes(t);
            AnimateShell(t);
            Paint(PulseGlow(t));
        }

        private void AnimateMotes(float t)
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                Transform m = _motes[i];
                if (m == null) continue;

                float spin = t * _orbitSpeed * _moteSpeed[i] * 360f + _motePhase[i] * 57.29578f;
                Vector3 pos = Quaternion.AngleAxis(spin, _moteAxis[i]) * _moteBasePos[i];

                // Alev/duman: parçacık yukarı süzülür, tepeye varınca başa döner (döngü görünmez
                // olsun diye konum yükseldikçe küçülür).
                float lift = 1f;
                if (_rise > 0.0001f)
                {
                    float cycle = Mathf.Repeat(t * _rise + _motePhase[i], 1f);
                    pos.y += cycle * 0.22f;
                    lift   = Mathf.Sin(cycle * Mathf.PI);      // 0 → 1 → 0: belirir, büyür, söner
                }

                m.localPosition = pos;

                float wobble = 1f + Mathf.Sin(t * (5.5f + i) ) * _flicker;
                m.localScale  = _moteBaseScale[i] * Mathf.Max(0.02f, wobble * lift);

                if (_tumbleSpeed > 0.01f)
                    m.localRotation = Quaternion.Euler(t * _tumbleSpeed * (1f + i * 0.13f),
                                                       t * _tumbleSpeed * 0.7f, 0f);
            }
        }

        private void AnimateShell(float t)
        {
            if (_shell == null) return;
            // Kabuk nefes alır; su/alevde biraz da yamulur (elipsoid) → "içi kaynıyor" hissi.
            float breathe = 1f + Mathf.Sin(t * 1.3f) * 0.05f;
            float squash  = _shape == EssenceOrbShape.Su || _shape == EssenceOrbShape.Alev
                          ? Mathf.Sin(t * 2.1f) * 0.07f : 0f;
            _shell.localScale = new Vector3(breathe - squash, breathe + squash, breathe - squash);
        }

        private float PulseGlow(float t)
        {
            if (_pulsePeriod <= 0.01f) return _pulseRange.x;
            float k = (Mathf.Sin(t * (Mathf.PI * 2f / _pulsePeriod)) + 1f) * 0.5f;
            return Mathf.Lerp(_pulseRange.x, _pulseRange.y, k);
        }

        private void Paint(float pulse)
        {
            if (_renderers.Length == 0 || _mpb == null) return;

            Color body = _color;
            body.a = _color.a * _fade;
            Color emis = _color * (_glow * pulse * _fade);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                // Kor daha parlak, kabuk daha sönük — derinlik hissi.
                float k = (_core != null && r.transform == _core) ? 1.45f
                        : (_shell != null && r.transform == _shell) ? 0.55f : 1f;

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, body);
                _mpb.SetColor(ColorId,     body);
                _mpb.SetColor(EmissionId,  emis * k);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
