using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Hex karolarının görünürlük durumunu (sis) yönetir — SAYDAMLIK yöntemiyle.
    /// Her karonun ÜSTÜNDE KALICI bir bulut kapağı durur (Instantiate/Destroy YOK — pop yok);
    /// bulutun OPAKLIĞI (alpha) yumuşakça değişir:
    ///   Visible  = alpha 0 (tam saydam → karo net görünür),
    ///   Hidden   = alpha _hiddenAlpha (opak → alttaki karo görünmez).
    /// Görüş baloncuğu (RevealArea) karakterle gezer; kule (SetFullReveal) tüm adayı saydamlaştırır.
    /// Ayrıca karonun temel rengi de parlaklıkla karartılır (ikinci katman).
    /// Grid yeniden üretilince (harita geçişi) HexGridManager.OnGridRegenerated ile sis yeniden kurulur.
    /// </summary>
    [DefaultExecutionOrder(-50)] // HexGridManager'dan sonra, PlayerController'dan önce
    public class FogOfWarManager : MonoBehaviour
    {
        [Header("Bağımlılık")]
        [SerializeField] private HexGridManager _gridManager;

        [Header("Sis Parlaklığı (karonun temel rengi çarpanı)")]
        [SerializeField, Range(0f, 1f)] private float _visibleBrightness = 1f;
        [Tooltip("Hiç görülmemiş — neredeyse siyah (bulut zaten örter; bu ikinci katman).")]
        [SerializeField, Range(0f, 1f)] private float _hiddenBrightness  = 0.05f;

        [Header("Bulut Kapağı (SAYDAMLIKLA açılır/kapanır — yok edilmez)")]
        [Tooltip("Karonun üstüne konan bulut prefabı. Kendi bulut modelinle değiştirebilirsin.")]
        [SerializeField] private GameObject _fogTilePrefab;
        [Tooltip("Bulutun karo tabanının ne kadar üstünde duracağı. Puf küreleri origin'in ~0.33m " +
                 "altına sarkar → bu değerin altında küreler karo yüzeyine gömülür (bulut karoya oturur).")]
        [SerializeField] private float _fogLift = 0.18f;
        [Tooltip("Gizli karonun bulut opaklığı (1 = gerçek karoyu tamamen örter).")]
        [SerializeField, Range(0f, 1f)] private float _hiddenAlpha = 1f;
        [Tooltip("Opaklık değişim hızı (birim/sn) — DÜŞÜK = daha yumuşak/geçişken belirme.")]
        [SerializeField] private float _fadeSpeed = 2.5f;
        [Tooltip("Bulutsuz baloncuğun kenarındaki geçiş bandı (karo). Karakter yürürken saydamlık " +
                 "bu bant üzerinden sürekli/dinamik değişir.")]
        [SerializeField] private float _fogFalloff = 1.4f;
        [Tooltip("Gerçek karo yalnız bulut bundan OPAKKEN gizli (ağaçlar sızmasın). Yüksek(0.9) → " +
                 "sadece TAM sisliyken gizli; bulut inceldikçe karo altından yumuşak belirir (pat yok).")]
        [SerializeField, Range(0f, 1f)] private float _tileRevealAlpha = 0.9f;

        [Header("Rüzgar (bulutlar hafif hareketli)")]
        [SerializeField] private float _windSpeed = 0.7f;
        [SerializeField] private float _windAmount = 0.13f;

        [Header("GECE — karanlıkta görüş daralır")]
        [Tooltip("Gece görüş menzili çarpanı (0.5 = yarı yarıya). Kule AÇILMAMIŞ adada uygulanır.")]
        [SerializeField, Range(0.1f, 1f)] private float _nightVisionMultiplier = 0.5f;
        [Tooltip("Gece TAVANI (karo): kule ile ada açılmış olsa BİLE geceleyin bundan uzaktaki " +
                 "karolar karanlıkta kalır. Karanlık zorunlu sınır.")]
        [SerializeField] private float _nightRevealRadius = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/Lit
        private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Standard yedek
        private MaterialPropertyBlock _block;

        // Kule ile açılmış ada → dinamik sis (RevealArea) kilitlenir, tüm karolar saydam kalır.
        private bool _fullyRevealed;
        public bool IsFullyRevealed => _fullyRevealed;

        // GECE: kilit devre dışı kalır, görüş _nightRevealRadius ile TAVANLANIR.
        private bool _nightMode;
        public bool IsNightMode => _nightMode;

        // UpdateFogAround'a en son verilen konum/menzil — sis kuralı değişince (gece/kule)
        // oyuncu HAREKET ETMEDEN de yeniden uygulanabilsin diye saklanır.
        private Vector3 _lastFogPos;
        private float   _lastFogRadius = 1f;
        private bool    _hasFogSample;

        // Karo başına KALICI bulut kapağı.
        private class Cap
        {
            public GameObject     go;
            public MeshRenderer[] rends;      // kahverengi örtünün render'ları
            public Renderer[]     tileRends;  // GERÇEK karonun render'ları (gizliyken kapalı)
            public Color          baseColor;
            public float          alpha;
            public float          target;
            public Vector3        basePos;   // rüzgar salınımının merkez konumu
            public float          phase;     // her buluta farklı faz (senkron olmasınlar)
        }

        private Transform _fogRoot;
        private readonly Dictionary<HexCoordinate, Cap> _caps    = new();
        private readonly HashSet<HexCoordinate>         _fading  = new();
        private readonly List<HexCoordinate>            _doneTmp = new();

        private void Awake()
        {
            if (_gridManager == null)
            {
                Debug.LogError("[FogOfWarManager] _gridManager NULL! Inspector'da bağlantı eksik. TAM KURULUM'u yeniden çalıştır.");
                return;
            }
            _fogRoot = new GameObject("FogTiles").transform;
            _fogRoot.SetParent(transform, false);

            _gridManager.OnGridRegenerated += InitializeFog;
            InitializeFog();
        }

        private void OnDestroy()
        {
            if (_gridManager != null) _gridManager.OnGridRegenerated -= InitializeFog;
        }

        private void InitializeFog()
        {
            if (_gridManager.Cells == null) return;
            _fading.Clear();
            foreach (var cell in _gridManager.Cells.Values)
            {
                cell.FogState = FogState.Hidden;
                SetCellBrightness(cell, _hiddenBrightness);

                Cap cap = EnsureCap(cell);
                if (cap == null) continue;
                cap.target = TargetAlpha(cell);
                cap.alpha  = cap.target;   // yüklemede anında (fade yok)
                ApplyCapAlpha(cap);
            }
        }

        /// <summary>Tüm karoları yeniden Hidden yapar (grid yeniden üretilince otomatik çağrılır).</summary>
        public void ResetFog() => InitializeFog();

        // ── Genel API ────────────────────────────────────────────────────

        /// <summary>Tüm karoları görünür yapar (savaş haritasında tam görüş / kule açılınca).</summary>
        public void RevealAll()
        {
            if (_gridManager.Cells == null) return;
            foreach (var cell in _gridManager.Cells.Values)
            {
                cell.FogState = FogState.Visible;
                SetCellBrightness(cell, _visibleBrightness);
                MarkFade(cell);
            }
        }

        /// <summary>
        /// DİNAMİK SİS (SÜREKLİ): karakterin CANLI dünya konumuna göre her karonun bulut opaklığını
        /// mesafeyle belirler → karakter YÜRÜRKEN saydamlık sürekli/akışkan değişir (karoya varınca
        /// pat diye değil). visionRadius içi tam saydam, _fogFalloff bandında yumuşak geçiş, ötesi opak.
        /// Kule ile açık adada (kilitli) yok sayılır.
        /// </summary>
        public void UpdateFogAround(Vector3 worldPos, float visionRadiusTiles)
        {
            if (_gridManager.Cells == null) return;

            // Sonraki yeniden-uygulamalar için sakla (gece/kule değişiminde oyuncu dursa bile lazım).
            _lastFogPos    = worldPos;
            _lastFogRadius = visionRadiusTiles;
            _hasFogSample  = true;

            // GÜNDÜZ + kule açık → ada tamamen görünür, dinamik sis kilitli.
            // GECE ise kilit devre dışı: karanlıkta ada açık olsa bile uzağı göremezsin.
            if (_fullyRevealed && !_nightMode) return;

            float radius = _nightMode
                ? (_fullyRevealed ? _nightRevealRadius              // kule açık ama gece → 3 karo tavanı
                                  : visionRadiusTiles * _nightVisionMultiplier) // kule yok → yarı görüş
                : visionRadiusTiles;

            float spacing = Mathf.Sqrt(3f) * Mathf.Max(0.01f, _gridManager.HexSize); // komşu karo mesafesi
            float band    = Mathf.Max(0.01f, _fogFalloff);

            foreach (var cell in _gridManager.Cells.Values)
            {
                float dx = worldPos.x - cell.WorldPosition.x;
                float dz = worldPos.z - cell.WorldPosition.z;
                float d  = Mathf.Sqrt(dx * dx + dz * dz) / spacing;           // karo biriminde mesafe
                float a  = Mathf.Clamp01((d - radius) / band) * _hiddenAlpha;

                // KEŞİF KALICI: oyuncunun bir kez GÖRDÜĞÜ karo bir daha sislenmez — arkasında iz
                // kapanmaz (kullanıcı isteği 2026-07-28). GAME_DESIGN §3 zaten bunu varsayıyordu:
                // "sis zaten hiç geri kapanmıyor, kule sadece erken açma".
                if (_permanentExploration && a <= 0.001f) _permanentReveals.Add(cell.Coordinate);

                // Kalıcı açılmış alan (keşif izi + gözetleme kulesi): sis bir daha kapanmaz.
                if (_permanentReveals.Count > 0 && _permanentReveals.Contains(cell.Coordinate)) a = 0f;

                if (_caps.TryGetValue(cell.Coordinate, out Cap cap) && cap != null &&
                    !Mathf.Approximately(cap.target, a))
                {
                    cap.target = a;
                    _fading.Add(cell.Coordinate);
                }

                // Mantık durumu + ikinci katman karartma (sürekli).
                cell.FogState = a < _hiddenAlpha * 0.5f ? FogState.Visible : FogState.Hidden;
                SetCellBrightness(cell, Mathf.Lerp(_visibleBrightness, _hiddenBrightness, a / Mathf.Max(0.001f, _hiddenAlpha)));
            }
        }

        /// <summary>
        /// Kule ile bu ADAnın sisini kalıcı aç/kapat. Açıkken dinamik sis (RevealArea) kilitlenir,
        /// tüm bulutlar saydamlaşır (ada %100 görünür). Harita değişiminde WatchtowerManager yeniden uygular.
        /// </summary>
        public void SetFullReveal(bool revealed)
        {
            _fullyRevealed = revealed;

            // GECE ise "hepsini aç" YOK — karanlıkta kule bile uzağı göstermez; bunun yerine
            // gece tavanıyla (_nightRevealRadius) yeniden uygula.
            if (revealed && !_nightMode) RevealAll();  // hepsi Visible → bulutlar 0'a fade
            else                         RefreshFromLastPosition();
        }

        /// <summary>
        /// GECE modunu aç/kapat. Açıkken görüş yarıya iner (<see cref="_nightVisionMultiplier"/>) ve
        /// kule ile açılmış adada bile <see cref="_nightRevealRadius"/> karo TAVANI uygulanır —
        /// karanlıkta uzağı göremezsin. DayNightCycle gündüz↔gece sınırında çağırır.
        /// </summary>
        public void SetNightMode(bool night)
        {
            if (_nightMode == night) return;
            _nightMode = night;

            // Gece bitti + kule açık → adanın tam görüşü geri gelir.
            if (!night && _fullyRevealed) RevealAll();
            else                          RefreshFromLastPosition();
        }

        /// <summary>Sis kuralı değiştiğinde (gece/kule) oyuncu HAREKET ETMEDEN de sisi yeniden uygular.
        /// Henüz hiç örnek alınmadıysa (oyun daha başlamadı) sessizce atlanır.</summary>
        public void RefreshFromLastPosition()
        {
            if (_hasFogSample) UpdateFogAround(_lastFogPos, _lastFogRadius);
        }

        /// <summary>Kule aktifleşince adanın bulutlarını yumuşakça saydamlaştırır (epik his — beam/halka
        /// WatchtowerManager'da). Saydamlık geçişi _fadeSpeed ile animasyonlu olduğu için SetFullReveal yeter.</summary>
        public void RevealAllAnimated(float duration) => SetFullReveal(true);

        // ── Kalıcı açılan alanlar (gözetleme kulesi — TASK-006) ─────────────
        // Sis bu projede DİNAMİK (her adımda oyuncu mesafesine göre yeniden hesaplanır), o yüzden
        // "kalıcı açık" bir küme olarak tutulup UpdateFogAround'da muaf tutuluyor.
        private readonly HashSet<HexCoordinate> _permanentReveals = new();

        [Tooltip("Oyuncunun bir kez GÖRDÜĞÜ karo kalıcı açık kalsın mı? Açıkken keşfedilen alan bir " +
                 "daha sislenmez (iz kapanmaz) — GAME_DESIGN §3'ün varsaydığı davranış. Kapatılırsa " +
                 "eski dinamik sis geri gelir (arkanda kapanır).")]
        [SerializeField] private bool _permanentExploration = true;

        /// <summary>Merkez etrafındaki <paramref name="radius"/> yarıçaplı alanı KALICI açar —
        /// oyuncu uzaklaşsa da sis geri kapanmaz. (Hex'te "5×5 alan" ≈ yarıçap 2 → 19 karo.)</summary>
        public void RevealAreaPermanent(HexCoordinate center, int radius)
        {
            if (_gridManager == null || _gridManager.Cells == null) return;
            foreach (var cell in _gridManager.Cells.Values)
                if (center.DistanceTo(cell.Coordinate) <= radius)
                    _permanentReveals.Add(cell.Coordinate);
            RefreshFromLastPosition();
        }

        public bool IsPermanentlyRevealed(HexCoordinate coord) => _permanentReveals.Contains(coord);

        /// <summary>Yeni harita üretilince kalıcı açıklıklar sıfırlanır.</summary>
        public void ClearPermanentReveals() => _permanentReveals.Clear();

        public FogState GetFogState(HexCoordinate coord) =>
            _gridManager.TryGetCell(coord, out HexCell c) ? c.FogState : FogState.Hidden;

        public bool IsVisible(HexCoordinate coord) => GetFogState(coord) == FogState.Visible;
        public bool IsKnown(HexCoordinate coord)   => GetFogState(coord) != FogState.Hidden;

        // ── Opaklık animasyonu + rüzgar salınımı ────────────────────────────
        private void Update()
        {
            // (1) Opaklık fade.
            if (_fading.Count > 0)
            {
                float step = _fadeSpeed * Time.deltaTime;
                _doneTmp.Clear();
                foreach (var coord in _fading)
                {
                    if (!_caps.TryGetValue(coord, out Cap cap) || cap == null || cap.go == null)
                    { _doneTmp.Add(coord); continue; }

                    cap.alpha = Mathf.MoveTowards(cap.alpha, cap.target, step);
                    ApplyCapAlpha(cap);
                    if (Mathf.Approximately(cap.alpha, cap.target)) _doneTmp.Add(coord);
                }
                for (int i = 0; i < _doneTmp.Count; i++) _fading.Remove(_doneTmp[i]);
            }

            // (2) Rüzgar: görünen bulutlar merkezleri etrafında yavaş/yumuşak salınır (esinti hissi).
            if (_windAmount > 0.0001f)
            {
                float t = Time.time * _windSpeed;
                foreach (var kv in _caps)
                {
                    Cap cap = kv.Value;
                    if (cap == null || cap.go == null || cap.alpha <= 0.01f) continue; // saydam bulut = kapalı
                    float p = cap.phase;
                    Vector3 w = new Vector3(
                        Mathf.Sin(t + p)          * _windAmount,
                        Mathf.Sin(t * 0.6f + p)   * _windAmount * 0.5f,
                        Mathf.Cos(t * 0.8f + p)   * _windAmount);
                    cap.go.transform.position = cap.basePos + w;
                }
            }
        }

        // ── Yardımcılar ─────────────────────────────────────────────────────

        private float TargetAlpha(HexCell cell) =>
            (_fullyRevealed || cell.FogState == FogState.Visible) ? 0f : _hiddenAlpha;

        private void MarkFade(HexCell cell)
        {
            Cap cap = EnsureCap(cell);
            if (cap == null) return;
            cap.target = TargetAlpha(cell);
            if (!Mathf.Approximately(cap.alpha, cap.target))
                _fading.Add(cell.Coordinate);
        }

        // Karo başına KALICI bulut kapağı (yoksa üretir, varsa konumlar). Asla yok edilmez.
        private Cap EnsureCap(HexCell cell)
        {
            if (_fogTilePrefab == null || _fogRoot == null) return null;

            if (!_caps.TryGetValue(cell.Coordinate, out Cap cap) || cap == null || cap.go == null)
            {
                GameObject go = Instantiate(_fogTilePrefab, _fogRoot);
                go.name = $"Fog_{cell.Coordinate}";
                var rends = go.GetComponentsInChildren<MeshRenderer>();

                Color bc = Color.white;
                if (rends.Length > 0 && rends[0].sharedMaterial != null &&
                    rends[0].sharedMaterial.HasProperty(BaseColorId))
                    bc = rends[0].sharedMaterial.GetColor(BaseColorId);
                bc.a = 1f;

                cap = new Cap { go = go, rends = rends, baseColor = bc, alpha = _hiddenAlpha, target = _hiddenAlpha,
                                phase = Random.Range(0f, 6.283f) };
                _caps[cell.Coordinate] = cap;
            }

            // Bulut karonun ÜSTÜNDE havada dursun. GERÇEK karonun render'larını önbelleğe al (grid
            // yeniden üretilmişse yenile) — sadece TAM sisliyken kapatacağız (yumuşak beliriş).
            cap.basePos = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _fogLift);
            cap.go.transform.position = cap.basePos;
            if (!cap.go.activeSelf) cap.go.SetActive(true);
            return cap;
        }

        // Örtüye opaklığı yaz (MPB) + GERÇEK karoyu aç/kapat: örtü opaksa gerçek karo GİZLİ (kahverengi
        // görünür), örtü yeterince solunca gerçek karo (ağaçlar dahil) BELİRİR → "var oluyor" hissi.
        private void ApplyCapAlpha(Cap cap)
        {
            if (cap.rends == null) return;
            _block ??= new MaterialPropertyBlock();

            Color c = cap.baseColor;
            c.a = cap.alpha;
            bool coverVisible = cap.alpha > 0.001f;

            foreach (var r in cap.rends)
            {
                if (r == null) continue;
                if (coverVisible)
                {
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, c);
                    _block.SetColor(ColorId, c);
                    r.SetPropertyBlock(_block);
                }
                if (r.enabled != coverVisible) r.enabled = coverVisible;
            }
            // NOT: gerçek karo render'ı HİÇ kapatılmaz (havada bulut → kapatırsak karonun yerinde delik
            // kalır). Havadaki opak bulut karoyu görsel olarak örter; bulut solunca karo yumuşak belirir.
        }

        /// <summary>Karonun temel rengini MEVCUT sis durumuna göre yeniden yazar. Harici bir efekt
        /// (örn. çöküş dalgasının geçici kırmızı karo boyaması — CollapseWaveEffect) kendi MPB
        /// değişikliğini geri alırken çağırır; böylece karo sis-doğru parlaklığa döner.</summary>
        public void ReapplyCellBrightness(HexCell cell) =>
            SetCellBrightness(cell, cell.FogState == FogState.Visible ? _visibleBrightness : _hiddenBrightness);

        /// <summary>Karonun üstündeki BULUT kapağını geçici renklendirir (t=0 → kendi rengi).
        /// Çöküş dalgası kullanır: sisli karolarda karo görünmez ama bulut görünür — dalga
        /// cephesi geçerken bulutun kendisi kızarır, yoksa boyama bulutun altında kaybolurdu.</summary>
        public void TintCloud(HexCoordinate coord, Color tint, float t)
        {
            if (!_caps.TryGetValue(coord, out Cap cap) || cap == null || cap.rends == null) return;
            if (cap.alpha <= 0.01f) return;                    // bulut zaten görünmez
            _block ??= new MaterialPropertyBlock();
            Color c = Color.Lerp(cap.baseColor, tint, Mathf.Clamp01(t));
            c.a = cap.alpha;
            foreach (var r in cap.rends)
            {
                if (r == null || !r.enabled) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(ColorId,     c);
                r.SetPropertyBlock(_block);
            }
        }

        // Karonun temel rengini parlaklık çarpanıyla _BaseColor'a yazar (materyali bozmadan, MPB).
        private void SetCellBrightness(HexCell cell, float brightness)
        {
            if (cell.MeshRenderer == null) return;

            Color c = cell.BaseColor * brightness;
            c.a = 1f;

            _block ??= new MaterialPropertyBlock();
            cell.MeshRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            cell.MeshRenderer.SetPropertyBlock(_block);
        }
    }
}
