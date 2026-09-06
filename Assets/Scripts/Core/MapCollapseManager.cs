using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kıyamet Sayacı — 1 GÜN ÖNCEDEN UYARILI çöküş, ADA-BAĞIMSIZ (3x3 dünya):
    ///   • Bir gün başında, o günün SONUNDA çökecek karolar AKTİF adada seçilir + İŞARETLENİR
    ///     (kırmızı kenar çizgisi + üstünde "kalan AP" sayısı).
    ///   • Gün bitince (AP=0 → yeni gün) TÜM adalardaki işaretli karolar çöker. Oyuncu hangi
    ///     adadaysa orada BÖLGESEL deprem + görsel silme; DİĞER adalarda VERİ olarak silinir
    ///     (o adaya dönünce kalıcı çökmüş gelir) ve aktif adadaki karolar UYARI için titrer.
    /// Durum ada başına saklanır (<see cref="_mapStates"/>), böylece ışınlanmak sayacı SIFIRLAMAZ.
    /// Oyuncu üstündeki karo / kule / PORTAL karoları korunur (portal = adalar arası tek geçiş).
    /// Sadece Overworld'de işler (savaş grid'inde uygulanmaz).
    /// </summary>
    public class MapCollapseManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _gridManager;
        [SerializeField] private ActionPointManager _apManager;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private CollapseConfig     _config;
        [SerializeField] private Camera             _camera;
        [Tooltip("Yalnız Overworld'de collapse uygula (savaş grid'inde değil). Atanmazsa hep uygulanır.")]
        [SerializeField] private GameStateManager   _state;
        [Tooltip("Çöküş anında kırmızı su-dalgası efekti (göle taş atma). Atanmazsa dalga çizilmez " +
                 "(çöküş yine olur).")]
        [SerializeField] private CollapseWaveEffect _wave;
        [Tooltip("SİSLİ bölgedeki uyarı görünsün diye: işaretli karonun BULUTU kızıl yanar. " +
                 "Atanmazsa uyarı yalnız yüzeydeki kırmızı çerçeveyle kalır (sisin altında görünmez).")]
        [SerializeField] private FogOfWarManager    _fog;

        [Header("Çöküş Görseli")]
        [SerializeField] private Material _collapsedMaterial;

        [Header("Uyarı (kırmızı çizgi + AP)")]
        [SerializeField] private Color  _outlineColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private float  _outlineWidth = 0.08f;
        [SerializeField] private float  _outlineLift  = 0.06f;
        [SerializeField] private Color  _labelColor   = new Color(1f, 0.35f, 0.25f);

        [Header("Kam'ı itme (çöken karodan kaçış)")]
        [Tooltip("Ayağının altındaki karo çökerken kaç halka uzağa kadar güvenli karo aranır. " +
                 "1 = yalnız komşular. Büyütmek Kam'ı daha uzağa ışınlanmış gibi gösterir.")]
        [SerializeField] private int _escapeRings = 3;

        [Header("Deprem (bölgesel sarsıntı — aktif adada silinen karo)")]
        [SerializeField] private float _shakeDuration  = 0.7f;
        [SerializeField] private float _shakeMagnitude = 0.12f;

        // ── Çöküşten MUAF karolar (TASK-007) ────────────────────────────────
        // Zorunlu görev karoları buraya girer (ChapterNodeManager bildirir): bunlar silinirse bölüm
        // bitirilemez hale gelirdi. Kule ve portal muafiyeti ayrıca PickDoomed içinde.
        private readonly HashSet<HexCoordinate> _protectedTiles = new();

        /// <summary>Çöküşten muaf karoları bildir (zorunlu görevler). Her yeni haritada yenilenir.</summary>
        public void SetProtectedTiles(IEnumerable<HexCoordinate> coords)
        {
            _protectedTiles.Clear();
            if (coords != null) foreach (var c in coords) _protectedTiles.Add(c);
        }

        public int  TotalRemovedTiles { get; private set; }
        public bool IsCollapseActive  { get; private set; }
        public event Action<int, int> OnTileCollapsed;

        /// <summary>Bir karo çöküşten GERİ GETİRİLDİ (madde 10). Minihatita ve HUD bunu dinler.</summary>
        public event Action<HexCoordinate> OnTileRestored;

        private int _lastProcessedDay = 0;

        // ── Çöküş durumu (1 bölüm = 1 harita → tek durum) ────────────────────
        // _doomed    = işaretlenmiş karolar + HANGİ GÜN silinecekleri. Karo, silineceği günden
        //              CollapseConfig.TelegraphDays kadar ÖNCE işaretlenir → oyuncu görmeden
        //              bir karo ASLA kaybolmaz (TASK-007: "sessiz silinme yok").
        // _collapsed = kalıcı silinmiş karolar (savaştan dönünce yeniden uygulanır)
        private readonly List<(HexCoordinate coord, int removeDay)> _doomed = new();
        private readonly HashSet<HexCoordinate> _collapsed = new();

        // ÇÖKÜŞ ÖNCESİ HÂL (madde 10 — geri getirme): karo çökerken tipi ve materyali ÜZERİNE
        // yazılıyor. Geri getirme bunları geri koyamazsa karo "yürünebilir ama çökmüş görünen"
        // bir hayalete dönerdi; o yüzden silmeden önce burada saklanıyor.
        private readonly Dictionary<HexCoordinate, (CellType type, Material mat, bool combat)> _preCollapse = new();

        private bool IsDoomed(HexCoordinate c)
        {
            for (int i = 0; i < _doomed.Count; i++) if (_doomed[i].coord.Equals(c)) return true;
            return false;
        }

        private bool InOverworld => _state == null || _state.State == GameState.Overworld;

        /// <summary>Silinecek karo işaretlenirken kaç gün önceden uyarılır.</summary>
        private int TelegraphDays => _config != null ? _config.TelegraphDays : 2;

        // Seçildi ama henüz AÇIKLANMADI (dalga+yıldırım bekliyor) → çerçeve/sayaç gizli.
        private readonly HashSet<HexCoordinate> _pendingReveal = new();

        // Aktif adanın kırmızı uyarı çizgileri (yalnız görünen ada için tutulur).
        private readonly Dictionary<HexCoordinate, LineRenderer> _outlines = new();
        private Transform _outlineRoot;
        private Material  _lineMat;
        private GUIStyle  _labelStyle;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            // Kurulum atlanmış eski sahnelerde de alarm çalışsın (CLAUDE.md: kritik bağ koddan da).
            if (_fog == null) _fog = FindFirstObjectByType<FogOfWarManager>();
            _outlineRoot = new GameObject("CollapseWarnings").transform;
            _outlineRoot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            // Savaştan dönüş / harita yeniden üretimi → çöküş durumunu yeniden uygula.
            if (_gridManager != null) _gridManager.OnGridRegenerated += ApplyCollapseStateForCurrentMap;
            if (_state       != null) _state.OnStateChanged          += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
            if (_gridManager != null) _gridManager.OnGridRegenerated -= ApplyCollapseStateForCurrentMap;
            if (_state       != null) _state.OnStateChanged          -= HandleStateChanged;
        }

        private void Start() => ApplyCollapseStateForCurrentMap();

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Overworld) ApplyCollapseStateForCurrentMap();
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (slot != 0) return;                 // yalnız gün sınırında
            if (day <= _lastProcessedDay) return;
            _lastProcessedDay = day;
            StartCoroutine(DayBoundaryRoutine(day));
        }

        private IEnumerator DayBoundaryRoutine(int day)
        {
            // 0) Vadesi GELMİŞ (removeDay <= bugün) işaretleri topla ve listeden düş. Uyarı süresi
            //    TelegraphDays gün olduğu için bunlar en az o kadar gündür kırmızı duruyordu.
            var todays = new List<HexCoordinate>();
            for (int i = _doomed.Count - 1; i >= 0; i--)
                if (_doomed[i].removeDay <= day) { todays.Add(_doomed[i].coord); _doomed.RemoveAt(i); }
            todays.Reverse();                        // işaretlenme sırası korunsun

            // 0.5) KAM ÇÖKEN KARODA KALMASIN (B2, 2026-09-03). Güvenlik SİLME ANINDA kurulur:
            //      PickDoomed'un "oyuncunun 2 karo çevresi muaf" kuralı SEÇİM anında çalışır, ama
            //      işaret 2 gün önceden konduğu için oyuncu o karoya sonradan yürüyebiliyor.
            //      Kaçacak yer bulunamazsa karo bugün SİLİNMEZ (kapana kısılmaktansa ertelenir).
            if (todays.Count > 0 && InOverworld && !ShovePlayerToSafety(todays, day))
            {
                HexCoordinate stuck = _player.CurrentCoord;
                todays.Remove(stuck);
            }

            if (todays.Count > 0) ClearOutlines();   // çökenlerin eski kırmızı çerçeveleri

            // 1) İLERİDE (day + TelegraphDays) çökecek YENİ karoları ŞİMDİ seç — veri hemen
            //    kesinleşir. GÖRSEL açıklama (kırmızı çerçeve + sayaç) hemen DEĞİL: dalga cephesi
            //    karonun üstünden geçerken YILDIRIMLA gelir (PlayWave → RevealDoomedTile).
            int targetDay = day + TelegraphDays;
            var collapsingNow = new HashSet<HexCoordinate>(todays);
            int count = _config != null ? _config.GetRemovalCount(targetDay) : 0;
            List<HexCell> newDoomed = (count > 0 && InOverworld)
                ? PickDoomed(count, targetDay, collapsingNow)
                : null;
            bool revealsAssigned = false;

            // İlk çıkan dalga açıklamaları taşır; diğerleri sade dalga.
            void PlayWave(Vector3 c, float d)
            {
                if (_wave == null) return;
                if (!revealsAssigned && newDoomed != null && newDoomed.Count > 0)
                { _wave.PlayWithReveals(c, d, newDoomed, RevealDoomedTile); revealsAssigned = true; }
                else _wave.Play(c, d);
            }

            // 2) Çöküşler: deprem + görsel silme + karodan dalga.
            if (todays.Count > 0 && InOverworld)
            {
                IsCollapseActive = true;
                foreach (var coord in todays)
                {
                    _collapsed.Add(coord);
                    if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                    {
                        PlayWave(cell.WorldPosition, 0f);
                        StartCoroutine(ShakeAndRemove(cell));
                    }
                    yield return new WaitForSeconds(0.12f);
                }
                yield return new WaitForSeconds(_shakeDuration);
                IsCollapseActive = false;
            }
            else
            {
                // Savaştayken gün döndüyse: veri işlensin, görseli overworld'e dönünce uygulanır.
                foreach (var coord in todays)
                {
                    if (!_collapsed.Add(coord)) continue;
                    TotalRemovedTiles++;
                    OnTileCollapsed?.Invoke(1, TotalRemovedTiles);
                }
            }

            // 3) Hiç dalga çıkmadıysa (örn. İLK uyarı günü — henüz çöküş yok) yıldırımlar
            //    dalgasız, art arda çakarak yeni işaretleri açıklar.
            if (newDoomed != null && newDoomed.Count > 0 && !revealsAssigned)
            {
                if (_wave != null) _wave.StrikeSeries(newDoomed, RevealDoomedTile);
                else foreach (var c in newDoomed) RevealDoomedTile(c);
            }

            // 4) HÂLÂ İŞARETLİ olan (vadesi gelmemiş) karoların çerçevesini geri koy.
            //    (2) numaralı adımdaki ClearOutlines TÜM çerçeveleri siliyor — yalnız çökenlerin
            //    değil. Geri konmazsa dün işaretlenmiş ama yarın düşecek karo uyarısını KAYBEDER;
            //    oyuncuya "eski işaretler kayboldu, sayaç yeni karolara geçti" gibi görünür
            //    (2026-09-02 hata raporu: "countdown'u yeni karolar sahipleniyor").
            RestoreRemainingOutlines();
        }

        /// <summary>Vadesi gelmemiş her işaretli karonun çerçevesi dursun. Yıldırımı henüz
        /// çakmamış olanlar (<see cref="_pendingReveal"/>) atlanır — onların açıklaması
        /// dalgayla gelecek.</summary>
        private void RestoreRemainingOutlines()
        {
            if (!InOverworld || _gridManager == null) return;

            foreach (var (coord, _) in _doomed)
            {
                if (_pendingReveal.Contains(coord)) continue;
                if (_outlines.ContainsKey(coord))   continue;
                if (_gridManager.TryGetCell(coord, out HexCell cell)) CreateOutline(cell);
            }
        }

        // ── KAM'I ÇÖKEN KARODAN İTME (B2, 2026-09-03) ───────────────────────

        /// <summary>
        /// Oyuncu bugün çökecek bir karonun üstündeyse yanındaki GÜVENLİ karoya iter.
        ///
        /// NEDEN SEÇİMDE DEĞİL DE BURADA: <see cref="PickDoomed"/> zaten oyuncunun
        /// <see cref="CollapseConfig.MinPlayerDistance"/> halkasını muaf tutuyor — ama o kural
        /// karo SEÇİLİRKEN işler. Uyarı <see cref="TelegraphDays"/> gün önceden konduğu için
        /// oyuncu işaretli karoya sonradan yürüyebiliyor ve karo altından çekiliyordu (kullanıcı
        /// raporu 2026-09-02: "bir an boşlukta duruyor gibi").
        /// </summary>
        /// <returns>false = kaçacak yer YOK; karo bugün silinmemeli (çağıran listeden düşürür).</returns>
        private bool ShovePlayerToSafety(List<HexCoordinate> falling, int day)
        {
            if (_player == null || _gridManager == null) return true;

            HexCoordinate at = _player.CurrentCoord;
            if (!falling.Contains(at)) return true;                 // oyuncu zaten güvende

            // Önce TERTEMİZ karo aranır (işaretsiz); bulunamazsa işaretli ama BUGÜN düşmeyecek
            // karo da kabul edilir — iki gün sonra düşecek bir karo, hiç kaçamamaktan iyidir.
            if (!TryFindEscapeTile(at, falling, false, out HexCell escape) &&
                !TryFindEscapeTile(at, falling, true,  out escape))
            {
                // Ada tamamen kapalı: karoyu bugün SİLME, bir gün ertele. Kıyametin beklemesi,
                // oyuncuyu boşlukta bırakmaktan ucuz.
                // (Zaten çökmüş karoda sıkışmışsa yeniden işaretlemenin anlamı yok — o karo
                //  bir daha silinemez, yalnız geri getirme kurtarır.)
                if (!_collapsed.Contains(at)) _doomed.Add((at, day + 1));
                Debug.LogWarning($"[Collapse] {at} cokecekti ama Kam'in kacacak yeri yok — " +
                                 "silme 1 gun ertelendi.");
                return false;
            }

            // İtilme oyuncunun HAMLESİ DEĞİL → AP yazmasın (kural AP yöneticisinde kalsın).
            if (_apManager != null) _apManager.GrantForcedMove();
            _player.ForceShiftTo(escape);
            Debug.Log($"[Collapse] Kam coken karodan itildi: {at} -> {escape.Coordinate}");
            return true;
        }

        /// <summary>Oyuncunun çevresinde halka halka güvenli karo arar (yakın halka önce).
        /// Arama YÜRÜNÜR karolardan geçer — su/dağ üstünden atlayıp karşı kıyıya konmasın.</summary>
        private bool TryFindEscapeTile(HexCoordinate from, List<HexCoordinate> falling,
                                       bool allowDoomed, out HexCell result)
        {
            result = null;
            var visited  = new HashSet<HexCoordinate> { from };
            var frontier = new List<HexCoordinate> { from };
            var ring     = new List<HexCoordinate>();

            for (int r = 1; r <= EscapeRings; r++)
            {
                ring.Clear();
                foreach (HexCoordinate c in frontier)
                    for (int d = 0; d < 6; d++)
                    {
                        HexCoordinate n = c.GetNeighbor(d);
                        if (!visited.Add(n)) continue;
                        if (!_gridManager.TryGetCell(n, out HexCell cell) || !cell.IsWalkable) continue;
                        ring.Add(n);
                    }

                // Halka içinde RASTGELE: Kam hep aynı yöne itilip desen olmasın.
                for (int i = ring.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (ring[i], ring[j]) = (ring[j], ring[i]);
                }

                foreach (HexCoordinate c in ring)
                {
                    if (falling.Contains(c))        continue;
                    if (_collapsed.Contains(c))     continue;
                    if (!allowDoomed && IsDoomed(c)) continue;
                    if (!_gridManager.TryGetCell(c, out HexCell cell)) continue;
                    result = cell;
                    return true;
                }

                frontier.Clear();
                frontier.AddRange(ring);
            }
            return false;
        }

        private int EscapeRings => Mathf.Max(1, _escapeRings);

        // Yeni işaretlenecek karoları SEÇER (veri: _doomed + _pendingReveal). Kırmızı çerçeve /
        // sayaç BURADA ÇİZİLMEZ — dalga cephesi karonun üstünden geçerken yıldırımla açıklanır
        // (RevealDoomedTile). alsoExclude = şu an çökmekte olanlar (yeniden seçilmesinler).
        // removeDay = bu karoların SİLİNECEĞİ gün (bugün + uyarı süresi).
        private List<HexCell> PickDoomed(int count, int removeDay, HashSet<HexCoordinate> alsoExclude)
        {
            bool          hasPlayer   = _player != null;
            HexCoordinate playerCoord = hasPlayer ? _player.CurrentCoord : default;

            // BASKI KURALI (kullanıcı isteği 2026-09-02): çöküş TÜM haritadan rastgele seçilince
            // çökenlerin çoğu oyuncunun görmediği sisli bölgede kalıyor ve kıyamet hiç
            // HİSSEDİLMİYORDU ("bazen oluyor bazen olmuyor" şikayetinin ikinci yarısı).
            // Artık iki havuz var: YAKIN (oyuncunun çevresi) ve UZAK. Seçim her iki havuzda da
            // rastgele — değişen tek şey, günün payının bir kısmının garantiyle dipte olması.
            var near = new List<HexCell>();
            var far  = new List<HexCell>();

            foreach (HexCell cell in _gridManager.Cells.Values)
            {
                if (!cell.IsWalkable)                        continue;
                if (cell.Coordinate == playerCoord)          continue;
                if (cell.CellType == CellType.Watchtower)    continue;
                if (IsDoomed(cell.Coordinate))               continue;
                if (_collapsed.Contains(cell.Coordinate))    continue;
                if (alsoExclude != null && alsoExclude.Contains(cell.Coordinate)) continue;
                // ZORUNLU GÖREV karoları asla silinmez (TASK-007) — bölüm bitirilemez hale gelmesin.
                if (_protectedTiles.Contains(cell.Coordinate)) continue;

                if (!hasPlayer) { far.Add(cell); continue; }

                int dist = playerCoord.DistanceTo(cell.Coordinate);
                // Dipteki halka muaf: oyuncunun bastığı karonun komşuları da çökerse oyuncu
                // kapana kısılırdı (hiçbir yöne yürüyemez, bölüm sert kesime kadar donar).
                if (dist <= MinPlayerDistance) continue;
                if (dist <= NearPlayerRadius) near.Add(cell); else far.Add(cell);
            }

            var picked = new List<HexCell>();
            int nearWanted = Mathf.RoundToInt(count * NearPlayerShare);

            // Havuzlardan biri yetmezse eksik pay ÖBÜRÜNDEN tamamlanır — günün karo sayısı
            // (CollapseConfig eğrisi) her hâlükârda tutmalı, yoksa "bazen olmuyor" geri gelir.
            TakeRandom(near, nearWanted,            removeDay, picked);
            TakeRandom(far,  count - picked.Count,  removeDay, picked);
            TakeRandom(near, count - picked.Count,  removeDay, picked);

            return picked;
        }

        private float NearPlayerShare   => _config != null ? _config.NearPlayerShare   : 0.6f;
        private int   NearPlayerRadius  => _config != null ? _config.NearPlayerRadius  : 7;
        private int   MinPlayerDistance => _config != null ? _config.MinPlayerDistance : 2;

        /// <summary>Havuzdan rastgele <paramref name="wanted"/> karo çeker, işaretler ve
        /// <paramref name="into"/>'ya ekler. Çekilen karo havuzdan düşer (aynı karo iki kez seçilmez).</summary>
        private void TakeRandom(List<HexCell> pool, int wanted, int removeDay, List<HexCell> into)
        {
            for (int i = 0; i < wanted && pool.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                HexCell cell = pool[idx];
                pool.RemoveAt(idx);
                _doomed.Add((cell.Coordinate, removeDay));
                _pendingReveal.Add(cell.Coordinate);
                into.Add(cell);
            }
        }

        // Dalga cephesi işaretli karonun üstünden geçti → yıldırım çaktı → çerçeve + sayaç
        // ANCAK ŞİMDİ görünür olur (CollapseWaveEffect callback'i).
        private void RevealDoomedTile(HexCell cell)
        {
            if (cell == null) return;
            if (!_pendingReveal.Remove(cell.Coordinate)) return;   // harita değişti / zaten açıklandı
            if (!InOverworld) return;
            CreateOutline(cell);
        }

        /// <summary>Overworld'e dönünce: kalıcı silinmişleri grid'e uygula + işaretli karoların
        /// kırmızı çizgilerini yeniden çiz. Savaş grid'inde çalışmaz.</summary>
        public void ApplyCollapseStateForCurrentMap()
        {
            if (_gridManager == null || _gridManager.Cells == null) return;
            ClearOutlines();
            _pendingReveal.Clear();   // dalga yarıda kaldıysa: işaretleri direkt çiz
            if (!InOverworld) return;                 // savaş grid'ine overworld çöküşünü uygulama

            foreach (var coord in _collapsed)
                if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                    RemoveTile(cell);

            foreach (var (coord, _) in _doomed)
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                    CreateOutline(cell);

            // SAVAŞTA GÜN DÖNDÜYSE karo VERİ olarak silinmiş, oyuncu da hâlâ onun üstünde olabilir
            // (yukarıdaki döngü karoyu şimdi yürünemez yaptı). Dönüşte Kam çukurun içinde durmasın.
            if (_player != null && _gridManager.TryGetCell(_player.CurrentCoord, out HexCell standing)
                && !standing.IsWalkable)
            {
                var here = new List<HexCoordinate> { _player.CurrentCoord };
                ShovePlayerToSafety(here, _apManager != null ? _apManager.CurrentDay : 0);
            }
        }

        /// <summary>Yeni bölüm/harita başlarken çöküş durumunu tamamen sıfırlar (TASK-007 retry).</summary>
        public void ResetCollapse()
        {
            _doomed.Clear();
            _collapsed.Clear();
            _preCollapse.Clear();
            _pendingReveal.Clear();
            TotalRemovedTiles = 0;
            _lastProcessedDay = 0;
            ClearOutlines();
            // Bulut önbelleği harita değişse de duruyor → eski haritanın alarmları taşınmasın.
            if (_fog != null) _fog.ClearCloudAlarms();
        }

        // ── Kırmızı hex çizgisi ──────────────────────────────────────────────
        private void CreateOutline(HexCell cell)
        {
            // Aynı karoya ikinci çerçeve çizilmesin: sözlüğe üzerine yazmak eski LineRenderer'ı
            // sahnede ÖKSÜZ bırakır (görünmeye devam eder, kimse silmez).
            if (_outlines.ContainsKey(cell.Coordinate)) return;

            if (_lineMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _lineMat = new Material(sh);
                if (_lineMat.HasProperty("_BaseColor")) _lineMat.SetColor("_BaseColor", _outlineColor);
                if (_lineMat.HasProperty("_Color"))     _lineMat.SetColor("_Color",     _outlineColor);
            }

            var go = new GameObject($"Warn_{cell.Coordinate}");
            go.transform.SetParent(_outlineRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace  = true;
            lr.loop           = true;
            lr.positionCount  = 6;
            lr.widthMultiplier = _outlineWidth;
            lr.material       = _lineMat;
            lr.startColor = lr.endColor = _outlineColor;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 baseP = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _outlineLift);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i] * 0.95f; // karo footprint'i (0.95)
                lr.SetPosition(i, baseP + new Vector3(c.x, 0f, c.z));
            }
            _outlines[cell.Coordinate] = lr;

            // Çerçeve karonun YÜZEYİNDE; sisli bölgede bulutun altında kalıyor. Uyarıyı görünen
            // katmana da yaz: o karonun bulutu kızıl yansın (2026-09-02 hata raporu).
            if (_fog != null) _fog.SetCloudAlarm(cell.Coordinate, true);
        }

        private void ClearOutlines()
        {
            foreach (var kv in _outlines)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
                if (_fog != null) _fog.SetCloudAlarm(kv.Key, false);
            }
            _outlines.Clear();
        }

        // ── Bölgesel deprem + silme (aktif ada) ──────────────────────────────
        private IEnumerator ShakeAndRemove(HexCell cell)
        {
            Transform vis = cell.Visual != null ? cell.Visual.transform : null;

            // TABAN KONUM DALGADAN SORULUR: deprem başladığı anda karo, aynı anda koşan bir çöküş
            // dalgası tarafından kaldırılmış olabilir. O konumu "taban" saysaydık karo sarsıntı
            // bitince havada kalırdı — karolar farklı yüksekliklerde donuyor, Kam'ın modeli de
            // yarısı gömülü duruyordu (2026-09-02 hata raporu).
            Vector3 basePos = vis == null ? cell.WorldPosition
                            : _wave != null ? _wave.BasePositionOf(vis)
                            : vis.position;

            float t = 0f;
            while (t < _shakeDuration && vis != null)
            {
                t += Time.deltaTime;
                float damp = 1f - (t / _shakeDuration);              // sönümlenen sarsıntı
                Vector3 j = new Vector3(
                    (UnityEngine.Random.value - 0.5f),
                    (UnityEngine.Random.value - 0.5f) * 0.6f,
                    (UnityEngine.Random.value - 0.5f)) * (_shakeMagnitude * damp);
                vis.position = basePos + j;
                yield return null;
            }
            if (vis != null) vis.position = basePos;

            RemoveTile(cell);
            TotalRemovedTiles++;
            OnTileCollapsed?.Invoke(1, TotalRemovedTiles);
            Debug.Log($"[Collapse] Karo silindi: {cell.Coordinate} | Toplam: {TotalRemovedTiles}");
        }

        private void RemoveTile(HexCell cell)
        {
            // Çöküş öncesi hâli SAKLA (madde 10). Yalnız gerçekten ayakta olan karo için: bu metot
            // savaştan dönüşte de çağrılıyor (ApplyCollapseStateForCurrentMap) ve orada karo
            // yeniden üretilmiş, yani ORİJİNAL hâlinde oluyor — çökmüş hâli kaydetmiş olmayalım.
            if (cell.IsWalkable)
                _preCollapse[cell.Coordinate] =
                    (cell.CellType,
                     cell.MeshRenderer != null ? cell.MeshRenderer.sharedMaterial : null,
                     cell.CanEnterCombat);

            cell.IsWalkable = false;
            cell.CellType   = CellType.Obstacle;
            // Çökmüş karoda savaş kapısı KALMAMALI: menzilden görev açan tarama (MissionManager)
            // karonun bayrağına bakıyor, çukura girilmesin.
            cell.CanEnterCombat = false;

            if (cell.MeshRenderer != null && _collapsedMaterial != null)
                cell.MeshRenderer.sharedMaterial = _collapsedMaterial;
            else if (cell.Visual != null)
                cell.Visual.SetActive(false);
        }

        // ── GERİ GETİRME (madde 10) ─────────────────────────────────────────

        /// <summary>Bu karo kalıcı olarak çökmüş mü?</summary>
        public bool IsCollapsed(HexCoordinate coord) => _collapsed.Contains(coord);

        /// <summary>Kalıcı çökmüş karoların listesi (geri getirme ekranı bunu gezer).</summary>
        public IReadOnlyCollection<HexCoordinate> CollapsedTiles => _collapsed;

        /// <summary>
        /// Çökmüş bir karoyu GERİ GETİRİR (madde 10): yürünebilirlik, karo tipi, savaş bayrağı ve
        /// materyali çöküş öncesi hâline döner. Çökmemiş ya da hâlâ İŞARETLİ (kırmızı, düşmek
        /// üzere) bir karo geri getirilemez — işaretliyi geri getirmek "iki gün sonra yine
        /// düşecek" bir karoyu satmak olurdu.
        /// </summary>
        /// <returns>false = karo çökmemiş, işaretli ya da grid'de yok.</returns>
        public bool RestoreTile(HexCoordinate coord)
        {
            if (!_collapsed.Contains(coord)) return false;
            if (IsDoomed(coord))             return false;
            if (_gridManager == null || !_gridManager.TryGetCell(coord, out HexCell cell)) return false;

            _collapsed.Remove(coord);
            TotalRemovedTiles = Mathf.Max(0, TotalRemovedTiles - 1);

            if (_preCollapse.TryGetValue(coord, out var before))
            {
                cell.CellType       = before.type;
                cell.CanEnterCombat = before.combat;
                if (cell.MeshRenderer != null && before.mat != null)
                    cell.MeshRenderer.sharedMaterial = before.mat;
                _preCollapse.Remove(coord);
            }
            else
            {
                // Kayıt yoksa (eski kayıttan yüklenmiş durum) en güvenli varsayım: sıradan karo.
                cell.CellType = CellType.Normal;
            }

            cell.IsWalkable = true;
            if (cell.Visual != null && !cell.Visual.activeSelf) cell.Visual.SetActive(true);

            OnTileRestored?.Invoke(coord);
            OnTileCollapsed?.Invoke(0, TotalRemovedTiles);   // HUD sayacı tazelensin
            Debug.Log($"[Collapse] Karo geri getirildi: {coord} | Kalan silinmis: {TotalRemovedTiles}");
            return true;
        }

        // ── Karo üstü "kalan AP" etiketi (işaretli karolar) ─────────────────
        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_camera == null || _apManager == null || !InOverworld) return;
            if (_doomed.Count == 0) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            _labelStyle.normal.textColor = _labelColor;

            // Sanal 1920x1080 ekrana çiz → sayaç yazısı her çözünürlükte aynı oranda.
            using var _scale = HudScale.Scaled();

            // HER KARO KENDİ SAYACINI GÖSTERİR (2026-09-02 düzeltmesi). Önceden bütün işaretli
            // karolara AYNI sayı (bugünün kalan AP'si) yazılıyordu: yarın düşecek karo ile üç gün
            // sonra düşecek karo aynı sayıyı taşıyor, gün dönünce hepsi birden başa sarıyordu.
            // Oyuncuya "sayaç bitti ama karo gitmedi, yeniden saymaya başladı" gibi görünüyordu.
            int today    = _apManager.CurrentDay;
            int apToday  = _apManager.APRemainingToday;
            int apPerDay = Mathf.Max(1, _apManager.SlotsPerDay * _apManager.MaxAP);

            foreach (var (coord, removeDay) in _doomed)
            {
                if (_pendingReveal.Contains(coord)) continue;   // yıldırım çakana dek sayaç gizli
                if (!_gridManager.TryGetCell(coord, out HexCell cell)) continue;

                // Karo, removeDay'in BAŞINDA (gün sınırında) düşer → o ana kadarki toplam AP.
                int ap = apToday + Mathf.Max(0, removeDay - today - 1) * apPerDay;
                Vector3 world = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.5f);
                Vector3 sp = _camera.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;                            // kamera arkası
                // WorldToScreenPoint gerçek PİKSEL verir → ölçekli GUI uzayına çevir.
                Vector2 g = HudScale.ToGui(sp);
                var rect = new Rect(g.x - 24f, g.y - 12f, 48f, 24f);
                GUI.Label(rect, ap.ToString(), _labelStyle);
            }
        }
    }
}
