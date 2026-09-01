using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ROTA (DURAKLI YOL İŞARETİ) — harita ekranından konan duraklar ve onları sırayla gezen
    /// patikanın 3B haritadaki gösterimi (kullanıcı isteği 2026-09-01, ikinci tur).
    ///
    /// DIŞARIDAN ALINAN DESENLER (kullanıcı "internetten araştır" dedi):
    ///   • GOOGLE MAPS "durak ekle": duraklar SIRAYLA eklenir, her birinin numaralı işareti olur,
    ///     tek dokunuşla silinir ve sayıları sınırlıdır (Maps 9-10 ile kapıyor → burada
    ///     <see cref="_maxStops"/>). Rota, duraklar arası bacaklara bölünür.
    ///   • GPS SAATLERİNİN "breadcrumb" izi: yolu KESİK KESİK bir iz + ilerleme yönünü gösteren
    ///     ok (chevron) çizer. Ok işaretleri burada da her birkaç karoda bir konuluyor.
    ///   • OYUN UI'ı yaygın kuralı (waypoint tasarım kılavuzları): tek bir gösterge yetmez —
    ///     DÜNYA işareti (bayrak) + MİNİHARİTA simgesi + aradaki ÇİZGİ birlikte kullanılır.
    ///     Sıradaki durak PARLAK, sonrakiler SÖNÜK (Maps'in "aktif bacak" vurgusu).
    ///
    /// PATİKA KARO MERKEZLERİNDEN GEÇER (kullanıcı şikayeti: "ışın gibi düz gidiyor"):
    /// her bacak <see cref="HexPathfinder"/> ile gerçek yürünebilir yol olarak hesaplanır.
    ///
    /// SİS KURALI: yol yalnız KEŞFEDİLMİŞ karolardan planlanır. Hedef sisin içindeyse iz,
    /// bilinen arazide gidilebilecek EN YAKIN karoya kadar gerçek yolu izler, oradan sonrası
    /// "TAHMİNİ" olarak hex doğrusuyla (yine karo merkezlerinden) devam eder ve sönük çizilir.
    /// Böylece hem karo takip edilir hem de görülmemiş arazinin nehir/dağ dizilişi ele verilmez.
    /// Gerçek yolu sisin içinde de görmek isteyen <see cref="_planThroughUnknown"/>'u açar.
    ///
    /// NEDEN <see cref="PathPreview"/> DEĞİL: o, tıklanan karoya giden yolu gösterip İKİNCİ TIKTA
    /// YÜRÜTÜR; camgöbeği, kesiksiz ve her karoya hex çerçeve koyar. Bu ise hiçbir hareket
    /// başlatmaz, leylak, kesik kesik, bayraklı ve oklu — ikisi aynı anda ekranda olabilir.
    ///
    /// ÇİZİM TEK MESH: 100 karoluk bir rota için ayrı ayrı LineRenderer kullanmak yüzlerce
    /// çizim çağrısı demekti. Bütün iz (kesikler + oklar + durak halkaları + bayraklar) TEK
    /// mesh'e yazılıyor, iki alt-mesh (parlak/sönük) → 2 çizim çağrısı. Mesh yalnız rota ya da
    /// oyuncunun KAROSU değişince kurulur; her karede yapılan tek iş renk nabzı.
    /// </summary>
    public class RouteMarker : MonoBehaviour
    {
        [Header("Bağımlılıklar (boşsa sahnede aranır)")]
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private PlayerController _player;
        [Tooltip("Opsiyonel — rota yalnız KEŞFEDİLMİŞ karolardan planlansın diye gerekir.")]
        [SerializeField] private FogOfWarManager  _fog;
        [Tooltip("Opsiyonel — atanmışsa rota yalnız Overworld'de çizilir (savaşta gizlenir).")]
        [SerializeField] private GameStateManager _state;

        [Header("Duraklar")]
        [Tooltip("En fazla kaç durak konabilir. Google Maps 9-10 ile kapatıyor; okunabilirlik " +
                 "sınırı da benzer — 9 durak minihatitada hâlâ ayırt edilebiliyor.")]
        [SerializeField, Range(2, 12)] private int _maxStops = 9;
        [Tooltip("Sıradaki durağın karosuna basıldığında o durak DÜŞER (Maps'te varılan durağın " +
                 "listeden çıkması gibi). Kapatılırsa duraklar elle silinene kadar durur.")]
        [SerializeField] private bool _popStopOnArrival = true;

        [Header("Plan")]
        [Tooltip("AÇIK: yol sisin içinden de gerçek arazi üzerinden hesaplanır (keşfedilmemiş " +
                 "nehir/dağ dizilişi ele verilir). KAPALI (varsayılan): bilinen araziden " +
                 "gidilebildiği yere kadar gerçek yol, sonrası tahmini hex doğrusu.")]
        [SerializeField] private bool _planThroughUnknown;

        [Header("Renk")]
        [Tooltip("Yürüme önizlemesinin camgöbeğinden ve menzil dışı kırmızısından UZAK bir ton.")]
        [SerializeField] private Color _color = new(0.72f, 0.44f, 1f);
        [Tooltip("Bilinen arazideki izin saydamlığı.")]
        [SerializeField, Range(0.05f, 1f)] private float _alpha = 0.42f;
        [Tooltip("Sönük parçalar: tahmini kuyruk ve sıradaki olmayan duraklar.")]
        [SerializeField, Range(0.02f, 1f)] private float _faintAlpha = 0.18f;
        [Tooltip("Nefes alma genliği (0 = sabit).")]
        [SerializeField, Range(0f, 0.4f)] private float _pulse = 0.07f;
        [SerializeField, Min(0.2f)] private float _pulsePeriod = 2.6f;

        [Header("Patika")]
        [Tooltip("İzin karo YÜZEYİNDEN yüksekliği — 'yer patikası' gibi otursun diye alçak.")]
        [SerializeField, Min(0f)] private float _lift = 0.14f;
        [Tooltip("TAHMİNİ kuyruk bu yükseklikte süzülür: sisin içinde iz bulut modellerinin " +
                 "altında kalıp görünmez olmasın, ayrıca 'bu kısım varsayım' desin.")]
        [SerializeField, Min(0f)] private float _estimateLift = 0.95f;
        [SerializeField, Min(0.02f)] private float _dashWidth = 0.17f;
        [Tooltip("Her karo adımının iki ucundan kırpılan oran — kesikleri bu boşluk ayırır.")]
        [SerializeField, Range(0f, 0.45f)] private float _dashGap = 0.22f;
        [Tooltip("Kaç karoda bir yön oku konsun (0 = ok yok).")]
        [SerializeField, Range(0, 12)] private int _chevronEvery = 4;
        [SerializeField, Min(0.05f)] private float _chevronSize = 0.42f;

        [Header("Durak işareti")]
        [Tooltip("Durak halkasının karo footprint'ine oranı.")]
        [SerializeField, Range(0.3f, 1f)] private float _ringFootprint = 0.72f;
        [SerializeField, Min(0.2f)] private float _flagHeight = 1.6f;
        [SerializeField, Min(0.02f)] private float _poleWidth = 0.07f;
        [SerializeField, Min(0.1f)] private float _pennantLength = 0.75f;
        [SerializeField, Min(0.1f)] private float _pennantHeight = 0.5f;

        // ── Durum ────────────────────────────────────────────────────────────

        /// <summary>Planın bir karosu. <see cref="Estimated"/> = sisin ötesindeki TAHMİNİ kuyruk.</summary>
        public readonly struct Step
        {
            public readonly HexCoordinate Coord;
            public readonly bool          Estimated;
            public Step(HexCoordinate coord, bool estimated) { Coord = coord; Estimated = estimated; }
        }

        private readonly List<HexCoordinate> _stops = new();
        private readonly List<Step>          _plan  = new();

        /// <summary>Duraklar, konma sırasıyla.</summary>
        public IReadOnlyList<HexCoordinate> Stops => _stops;

        /// <summary>Oyuncudan başlayıp tüm durakları gezen karo dizisi (minihatita da bunu çizer).</summary>
        public IReadOnlyList<Step> Plan => _plan;

        public bool HasRoute      => _stops.Count > 0;
        public int  StopCount     => _stops.Count;
        public int  MaxStops      => _maxStops;
        public bool IsFull        => _stops.Count >= _maxStops;
        /// <summary>Planın bir kısmı sisin ötesinde TAHMİNİ mi?</summary>
        public bool HasEstimate   { get; private set; }
        /// <summary>Rotanın toplam karo sayısı (tahmini kuyruk dahil).</summary>
        public int  TotalTiles    => _plan.Count > 0 ? _plan.Count - 1 : 0;

        /// <summary>Duraklar ya da plan değişti — minihatita simgeleri bunu dinler.</summary>
        public event Action OnChanged;

        private HexPathfinder _pathfinder;
        private Camera        _camera;

        // Çizim
        private Transform     _root;
        private MeshFilter    _filter;
        private MeshRenderer  _renderer;
        private Mesh          _mesh;
        private Material      _matBright;
        private Material      _matFaint;
        private bool          _shown;

        private readonly List<Vector3>      _verts      = new();
        private readonly List<int>          _trisBright = new();
        private readonly List<int>          _trisFaint  = new();
        private readonly List<HexCoordinate> _lineBuffer = new();

        private HexCoordinate _planFrom;      // planın hesaplandığı oyuncu karosu
        private int           _planFogVersion = -1;
        private bool          _meshDirty;

        // ── Durak düzenleme (harita ekranı çağırır) ──────────────────────────

        /// <summary>Karonun kaçıncı durak olduğu; durak değilse -1.</summary>
        public int IndexOf(HexCoordinate coord)
        {
            for (int i = 0; i < _stops.Count; i++)
                if (_stops[i].Equals(coord)) return i;
            return -1;
        }

        /// <summary>Sona durak ekler (Google Maps "stop ekle": yeni durak listenin SONUNA gider).
        /// Zaten durak olan ya da liste dolu ise false döner.</summary>
        public bool AddStop(HexCoordinate coord)
        {
            if (IsFull || IndexOf(coord) >= 0) return false;
            _stops.Add(coord);
            Invalidate();
            return true;
        }

        public bool RemoveStop(HexCoordinate coord)
        {
            int i = IndexOf(coord);
            if (i < 0) return false;
            _stops.RemoveAt(i);
            Invalidate();
            return true;
        }

        /// <summary>Durak varsa siler, yoksa ekler — harita ekranındaki tek tık bunu çağırır.</summary>
        public bool Toggle(HexCoordinate coord)
            => IndexOf(coord) >= 0 ? RemoveStop(coord) : AddStop(coord);

        public void ClearAll()
        {
            if (_stops.Count == 0) return;
            _stops.Clear();
            Invalidate();
        }

        private void Invalidate()
        {
            Replan();
            OnChanged?.Invoke();
        }

        // ── Yaşam döngüsü ────────────────────────────────────────────────────

        private void Awake()
        {
            _pathfinder = new HexPathfinder();
            _camera     = Camera.main;

            // Kurulum bağlamayı atlamış olsa bile çalışsın: kritik bağ koddan da kurulur.
            if (_grid   == null) _grid   = FindFirstObjectByType<HexGridManager>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_fog    == null) _fog    = FindFirstObjectByType<FogOfWarManager>();
            if (_state  == null) _state  = FindFirstObjectByType<GameStateManager>();
        }

        private void OnEnable()
        {
            if (_player != null) _player.OnMoved += OnPlayerMoved;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnMoved -= OnPlayerMoved;
            Hide();
        }

        /// <summary>Sıradaki durağa VARILDIĞINDA o durak düşer (Maps'te varılan durağın listeden
        /// çıkması gibi). Sonraki duraklar durur, rota kendiliğinden bir sonraki bacağa geçer.</summary>
        private void OnPlayerMoved(HexCoordinate coord)
        {
            if (_popStopOnArrival && _stops.Count > 0 && _stops[0].Equals(coord))
            {
                _stops.RemoveAt(0);
                Invalidate();
                return;
            }
            // Yeni karodan yeni bacak: LateUpdate zaten _planFrom değişimini yakalıyor,
            // burada ikinci kez planlamak aynı işi iki kez yapmak olurdu.
        }

        private void LateUpdate()
        {
            if (_stops.Count == 0) { Hide(); return; }   // boşta iş yok (Hide kendi bayrağına bakar)

            if (!ShouldDraw()) { Hide(); return; }
            if (_grid == null || _player == null) { Hide(); return; }

            // Sis açıldıkça daha iyi (gerçek) yol bulunabilir → keşif sayacı değişince yeniden planla.
            int fogVersion = _fog != null ? _fog.ExplorationVersion : 0;
            if (fogVersion != _planFogVersion || !_planFrom.Equals(_player.CurrentCoord)) Replan();

            if (_meshDirty) RebuildMesh();
            if (_mesh == null || _mesh.vertexCount == 0) { Hide(); return; }

            Paint();
            if (!_shown) { _root.gameObject.SetActive(true); _shown = true; }
        }

        private bool ShouldDraw()
        {
            // Tam ekran menü (harita/çanta/kitap) açıkken iz zaten görünmezdi.
            if (MenuState.IsAnyOpen) return false;
            return _state == null || _state.State == GameState.Overworld;
        }

        private void Hide()
        {
            if (!_shown) return;
            if (_root != null) _root.gameObject.SetActive(false);
            _shown = false;
        }

        // ── Plan ─────────────────────────────────────────────────────────────

        /// <summary>Oyuncudan başlayıp durakları SIRAYLA gezen karo dizisini kurar.</summary>
        private void Replan()
        {
            _plan.Clear();
            HasEstimate = false;
            _meshDirty  = true;

            if (_grid == null || _player == null || _stops.Count == 0) return;

            _planFrom       = _player.CurrentCoord;
            _planFogVersion = _fog != null ? _fog.ExplorationVersion : 0;

            // Harita altından kayan durak (çöküş sildi) rotada tutulmaz.
            for (int i = _stops.Count - 1; i >= 0; i--)
                if (!_grid.TryGetCell(_stops[i], out _)) _stops.RemoveAt(i);
            if (_stops.Count == 0) return;

            _plan.Add(new Step(_planFrom, false));

            HexCoordinate from = _planFrom;
            for (int i = 0; i < _stops.Count; i++)
            {
                AppendLeg(from, _stops[i]);
                from = _stops[i];
            }
        }

        /// <summary>Bir bacak: önce bilinen arazide GERÇEK yol; olmuyorsa gidilebilen en uzak
        /// bilinen karoya kadar gerçek yol + kalanı TAHMİNİ hex doğrusu.</summary>
        private void AppendLeg(HexCoordinate from, HexCoordinate to)
        {
            if (from.Equals(to)) return;

            if (_grid.TryGetCell(from, out HexCell a) && _grid.TryGetCell(to, out HexCell b))
            {
                List<HexCell> path = _pathfinder.FindPath(a, b, _grid, _planThroughUnknown ? null : IsKnown);
                if (path != null && path.Count > 1) { AppendCells(path); return; }

                // Hedefe bilinen araziden gidilemiyor → nereye kadar gidebiliyorsak oraya.
                HexCell bridge = NearestReachableKnown(a, to);
                if (bridge != null && !bridge.Coordinate.Equals(from))
                {
                    List<HexCell> known = _pathfinder.FindPath(a, bridge, _grid, IsKnown);
                    if (known != null && known.Count > 1)
                    {
                        AppendCells(known);
                        from = bridge.Coordinate;
                    }
                }
            }

            AppendHexLine(from, to);   // tahmini kuyruk (arazi bilgisi KULLANMAZ)
        }

        private bool IsKnown(HexCell cell) => _fog == null || _fog.IsKnown(cell.Coordinate);

        private void AppendCells(List<HexCell> path)
        {
            for (int i = 1; i < path.Count; i++)          // ilk karo zaten planda
                _plan.Add(new Step(path[i].Coordinate, false));
        }

        /// <summary>TAHMİNİ kuyruk: iki karo arasındaki hex DOĞRUSU. Yol bulma yapmaz, yani
        /// görülmemiş arazi hakkında hiçbir şey söylemez; ama yine KARO MERKEZLERİNDEN geçer.</summary>
        private void AppendHexLine(HexCoordinate from, HexCoordinate to)
        {
            from.LineTo(to, _lineBuffer);
            for (int i = 1; i < _lineBuffer.Count; i++)
                _plan.Add(new Step(_lineBuffer[i], true));
            if (_lineBuffer.Count > 1) HasEstimate = true;
        }

        /// <summary>Bilinen+yürünebilir karolar arasında <paramref name="from"/>'dan gidilebilen,
        /// hedefe EN YAKIN karo (genişlik-öncelikli arama). Sis duvarına dayanan izin nerede
        /// tahmine döneceğini bu belirler.</summary>
        private HexCell NearestReachableKnown(HexCell from, HexCoordinate target)
        {
            var visited = new HashSet<HexCoordinate> { from.Coordinate };
            var queue   = new Queue<HexCell>();
            queue.Enqueue(from);

            HexCell best     = from;
            int     bestDist = from.Coordinate.DistanceTo(target);

            while (queue.Count > 0)
            {
                HexCell cell = queue.Dequeue();
                int dist = cell.Coordinate.DistanceTo(target);
                if (dist < bestDist) { bestDist = dist; best = cell; }

                foreach (HexCell n in _grid.GetNeighbors(cell.Coordinate))
                {
                    if (!n.IsWalkable || !IsKnown(n)) continue;
                    if (!visited.Add(n.Coordinate))   continue;
                    queue.Enqueue(n);
                }
            }
            return best;
        }

        // ── Çizim ────────────────────────────────────────────────────────────

        /// <summary>Karonun ÜSTÜNDEKİ nokta. Karo yoksa (okyanus / harita dışı) düzlem konumu
        /// kullanılır — tahmini kuyruk denizin üstünden geçebilir.</summary>
        private Vector3 Point(HexCoordinate coord, bool estimated)
        {
            float lift = estimated ? _estimateLift : _lift;
            if (_grid.TryGetCell(coord, out HexCell cell))
                return cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + lift);
            return coord.ToWorldPosition(_grid.HexSize) + Vector3.up * (HexMetrics.TileHeight + lift);
        }

        private void RebuildMesh()
        {
            _meshDirty = false;
            EnsureBuilt();

            _verts.Clear(); _trisBright.Clear(); _trisFaint.Clear();

            Vector3 camRight = _camera != null
                ? Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized
                : Vector3.right;
            if (camRight.sqrMagnitude < 0.01f) camRight = Vector3.right;

            // 1) İZ — her karo adımı için ucundan kırpılmış bir kesik.
            for (int i = 1; i < _plan.Count; i++)
            {
                Step s0 = _plan[i - 1], s1 = _plan[i];
                Vector3 p0 = Point(s0.Coord, s0.Estimated);
                Vector3 p1 = Point(s1.Coord, s1.Estimated);
                bool faint = s1.Estimated;

                Vector3 a = Vector3.Lerp(p0, p1, _dashGap);
                Vector3 b = Vector3.Lerp(p0, p1, 1f - _dashGap);
                AddRibbon(a, b, Perp(p1 - p0), _dashWidth, faint);

                // 2) YÖN OKU — GPS "breadcrumb" kuralı: iz + gidiş yönünü söyleyen chevron.
                if (_chevronEvery > 0 && i % _chevronEvery == 0) AddChevron(p0, p1, faint);
            }

            // 3) DURAKLAR — halka + bayrak. Sıradaki durak PARLAK, sonrakiler sönük.
            for (int i = 0; i < _stops.Count; i++)
            {
                bool faint = i > 0;
                Vector3 ground = Point(_stops[i], false);
                AddRing(ground, faint);
                AddFlag(ground, camRight, faint);
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(_trisBright, 0);
            _mesh.SetTriangles(_trisFaint,  1);
            _mesh.RecalculateBounds();
        }

        private static Vector3 Perp(Vector3 dir)
        {
            Vector3 flat = new(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 0.000001f) return Vector3.right;
            return Vector3.Cross(flat.normalized, Vector3.up);
        }

        private void AddChevron(Vector3 p0, Vector3 p1, bool faint)
        {
            Vector3 dir  = (p1 - p0); dir.y = 0f;
            if (dir.sqrMagnitude < 0.000001f) return;
            dir.Normalize();

            Vector3 side = Vector3.Cross(dir, Vector3.up);
            Vector3 tip  = Vector3.Lerp(p0, p1, 0.5f) + dir * (_chevronSize * 0.5f);
            Vector3 back = tip - dir * _chevronSize;

            Vector3 armL = back + side * (_chevronSize * 0.8f);
            Vector3 armR = back - side * (_chevronSize * 0.8f);
            AddRibbon(tip, armL, Perp(armL - tip), _dashWidth * 0.8f, faint);
            AddRibbon(tip, armR, Perp(armR - tip), _dashWidth * 0.8f, faint);
        }

        private void AddRing(Vector3 center, bool faint)
        {
            for (int k = 0; k < 6; k++)
            {
                float scale = _ringFootprint * (_grid != null ? _grid.HexSize : 1f);
                Vector3 c0 = HexMetrics.Corners[k]           * scale;
                Vector3 c1 = HexMetrics.Corners[(k + 1) % 6] * scale;
                Vector3 a  = center + new Vector3(c0.x, 0f, c0.z);
                Vector3 b  = center + new Vector3(c1.x, 0f, c1.z);
                AddRibbon(a, b, Perp(b - a), _dashWidth * 0.8f, faint);
            }
        }

        /// <summary>Bayrak: dikey direk + üstünde üçgen flama. Kamera sabit izometrik açıda
        /// olduğu için yüzler kameranın sağ vektörüne göre kuruluyor (billboard'a gerek yok).</summary>
        private void AddFlag(Vector3 ground, Vector3 camRight, bool faint)
        {
            Vector3 top = ground + Vector3.up * _flagHeight;
            AddRibbon(ground, top, camRight, _poleWidth, faint);

            Vector3 tipA = top;
            Vector3 tipB = top - Vector3.up * _pennantHeight;
            Vector3 tipC = top + camRight * _pennantLength - Vector3.up * (_pennantHeight * 0.5f);
            AddTriangle(tipA, tipB, tipC, faint);
        }

        /// <summary>a→b arasında, <paramref name="sideDir"/> yönünde genişleyen bir şerit (quad).</summary>
        private void AddRibbon(Vector3 a, Vector3 b, Vector3 sideDir, float width, bool faint)
        {
            Vector3 half = sideDir.normalized * (width * 0.5f);
            int i0 = _verts.Count;
            _verts.Add(a - half); _verts.Add(a + half); _verts.Add(b + half); _verts.Add(b - half);

            List<int> tris = faint ? _trisFaint : _trisBright;
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, bool faint)
        {
            int i0 = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            List<int> tris = faint ? _trisFaint : _trisBright;
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }

        private void Paint()
        {
            float wave = _pulse * Mathf.Sin(Time.time * (Mathf.PI * 2f / _pulsePeriod));
            SetColor(_matBright, Mathf.Clamp01(_alpha      + wave));
            SetColor(_matFaint,  Mathf.Clamp01(_faintAlpha + wave * 0.5f));
        }

        private void SetColor(Material mat, float alpha)
        {
            if (mat == null) return;
            Color c = _color; c.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            var go = new GameObject("RouteMarker");
            go.transform.SetParent(transform, false);
            _root = go.transform;

            _mesh = new Mesh { name = "RouteTrail" };
            _mesh.MarkDynamic();

            _filter   = go.AddComponent<MeshFilter>();
            _renderer = go.AddComponent<MeshRenderer>();
            _filter.sharedMesh = _mesh;

            _matBright = NewMaterial();
            _matFaint  = NewMaterial();
            _renderer.sharedMaterials  = new[] { _matBright, _matFaint };
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows    = false;

            go.SetActive(false);
        }

        private void OnDestroy()
        {
            // Runtime'da new'lenen mesh ve materyaller sahne kapanınca kendiliğinden gitmez.
            if (_mesh      != null) Destroy(_mesh);
            if (_matBright != null) Destroy(_matBright);
            if (_matFaint  != null) Destroy(_matFaint);
        }

        private Material NewMaterial()
        {
            // PathPreview ile AYNI yöntem: saydam URP/Unlit. Proje içi kendi shader'ımız YOK —
            // Shader.Find yalnız URP'nin her zaman dahil edilen shader'larında güvenli.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface",  1f);   // 0=opaque, 1=transparent
                mat.SetFloat("_Blend",    0f);   // alpha blend
                mat.SetFloat("_ZWrite",   0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            // ÇİFT TARAFLI: üçgen sarım yönünü yanlış hesaplamak, izin sessizce görünmez olması
            // demekti. Culling kapalıyken yön hiç önemli değil.
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            return mat;
        }
    }
}
