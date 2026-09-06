using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ZORUNLU GÖREV FENERİ (B1, 2026-09-03): açık her zorunlu görevin karosunda duran ALTIN GÖK
    /// SÜTUNU — dünyada, kalıcı, sisten bağımsız.
    ///
    /// NEDEN VAR (kullanıcı raporu 2026-09-02): yeni görev düşerken oynayan
    /// <see cref="MandatoryQuestFallEffect"/> BİR KEZ oynar ve menzili vardır. Görüş dışında
    /// düşerse oyuncu hiç haberdar olmaz, ekranın ucunda düşerse ne olduğunu anlamaz. Miniharita
    /// ikonu ile üstteki zincir barı ise yalnız UI'A BAKANI bilgilendirir; istenen ise oyuncunun
    /// ana haritada yürürken UI'a bakmadan haberdar olması. Fener bu boşluğu dünyada kapatır;
    /// ekran DIŞINDA kalan feneri de <see cref="TacticalRPG.UI.QuestBeaconCompassHUD"/> pusulası
    /// gösterir (aynı veriden beslenir).
    ///
    /// İKİ KADEME — biri haber verir, öbürü yol gösterir:
    ///   • YENİ (düştüğü günden <see cref="_freshDays"/> gün sonrasına kadar): kalın sütun,
    ///     nabız gibi atar. "Bir şey oldu, oraya bak."
    ///   • SAKİN (görev bitene kadar): ince ve sabit. "Şurada hâlâ bir zorunlu görev var."
    ///     Beş görev birden açıkken ekranı altına boğmasın diye incelir.
    ///
    /// Tamamen PROSEDÜREL (prefab/asset istemez) — <see cref="MandatoryQuestFallEffect"/> ile
    /// aynı desen; ışık sütunu zaten oradan tanıdık, fener onun kalıcı hâli gibi okunur.
    ///
    /// SORUMLULUK: burası yalnız ÇİZER. Hangi görevin açık olduğunu <see cref="ChapterNodeManager"/>,
    /// ne zaman doğduğunu <see cref="MandatoryQuestDirector"/> bilir.
    /// </summary>
    [DefaultExecutionOrder(-60)]   // ChapterNodeManager(-80) düğümleri kurduktan SONRA
    public class MandatoryQuestBeacon : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ChapterNodeManager _nodes;
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private ActionPointManager _ap;
        [Tooltip("Yeni harita üretilince eski fenerler söndürülür (karo yükseklikleri değişir).")]
        [SerializeField] private ChapterMapGenerator _map;
        [Tooltip("Savaş/yerleştirme sırasında fenerler gizlenir (o an başka bir grid çizilir).")]
        [SerializeField] private GameStateManager   _state;

        [Header("Sütun ölçüleri")]
        [Tooltip("Sütunun yüksekliği. Kameranın ufkundan görülebilmesi için haritanın en yüksek " +
                 "karosundan çok daha uzun olmalı.")]
        [SerializeField] private float _height       = 42f;
        [Tooltip("YENİ görevin sütun yarıçapı (kalın, uzaktan fark edilir).")]
        [SerializeField] private float _freshRadius  = 0.55f;
        [Tooltip("Eskiyen görevin sütun yarıçapı (ince, yalnız yön gösterir).")]
        [SerializeField] private float _calmRadius   = 0.16f;

        [Header("Kademe")]
        [Tooltip("Görev düştükten kaç GÜN boyunca 'yeni' sayılsın (kalın + nabızlı).")]
        [SerializeField] private int _freshDays = 2;

        [Header("Renk / ışık")]
        [Tooltip("Zorunlu görev işaretiyle AYNI altın (ChapterNodeManager.MarkerColors).")]
        [SerializeField] private Color _color         = new(1.00f, 0.85f, 0.20f);
        [SerializeField] private float _freshEmission = 5.0f;
        [SerializeField] private float _calmEmission  = 1.6f;
        [SerializeField] private float _pulseSpeed    = 2.4f;

        /// <summary>Tek bir fener. Pusula HUD'ı bu listeyi okur (kendi arama yapmaz).</summary>
        public class Beacon
        {
            public HexCoordinate Coord;
            /// <summary>Karonun yüzeyi — pusula oku ve mesafe bunu hedefler.</summary>
            public Vector3 Ground;
            /// <summary>Zorunlu görevin kademesi (1-tabanlı, ödül eğrisi ile aynı sayı).</summary>
            public int  Tier;
            /// <summary>Hâlâ "yeni düştü" kademesinde mi (kalın + nabızlı + pusulada yazılı).</summary>
            public bool Fresh;

            internal Transform  Pillar;
            internal Renderer   Rend;
            internal int        DropDay;
            internal float      Phase;      // nabızlar aynı anda atmasın
        }

        private readonly List<Beacon> _beacons = new();

        /// <summary>Şu an dünyada duran fenerler (salt okunur).</summary>
        public IReadOnlyList<Beacon> Beacons => _beacons;

        private Transform _root;
        private Material  _material;
        private bool      _anyFresh;

        private void Awake()
        {
            _root = new GameObject("QuestBeacons").transform;
            _root.SetParent(transform, false);

            // Sessiz kalmasın: sıfır yükseklikli sütun ÇİZİLİR ama görünmez — "fener çalışmıyor"
            // diye saatlerce aranacak bir hata. Böyle bir sahne kurulumu bozuktur, söylensin.
            if (_height <= 0.01f)
                Debug.LogWarning("[Fener] _height 0 — bilesen bozuk serilesmis. " +
                                 "TacticalRPG > Bolum - Zorunlu Gorev Zincirini Kur ile tazele.");
        }

        private void OnEnable()
        {
            if (_nodes != null) _nodes.OnNodesChanged += Sync;
            if (_ap    != null) _ap.OnTimeAdvanced    += HandleTimeAdvanced;
            if (_state != null) _state.OnStateChanged += HandleStateChanged;
            if (_map   != null) _map.OnMapGenerated   += HandleMapGenerated;
        }

        private void OnDisable()
        {
            if (_nodes != null) _nodes.OnNodesChanged -= Sync;
            if (_ap    != null) _ap.OnTimeAdvanced    -= HandleTimeAdvanced;
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
            if (_map   != null) _map.OnMapGenerated   -= HandleMapGenerated;
        }

        /// <summary>Yeni harita → eski sütunlar KOŞULSUZ söner. Koordinat aynı kalsa bile karo
        /// yüksekliği ve görevin kademesi değişmiş olabilir; ayakta bırakmak bayat fener demek.</summary>
        private void HandleMapGenerated()
        {
            for (int i = 0; i < _beacons.Count; i++)
                if (_beacons[i].Pillar != null) Destroy(_beacons[i].Pillar.gameObject);
            _beacons.Clear();
            Sync();
        }

        private void Start() => Sync();

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (slot != 0) return;      // kademe GÜN sınırında değişir, her dilimde değil
            RefreshStages();
        }

        private void HandleStateChanged(GameState s)
        {
            if (_root != null) _root.gameObject.SetActive(s == GameState.Overworld);
            if (s == GameState.Overworld) Sync();   // savaşta biten görevin feneri sönsün
        }

        // ── Fener listesi ────────────────────────────────────────────────────

        /// <summary>
        /// Fenerleri açık zorunlu görevlerle eşitler: biteni söndürür, yeni düşene sütun diker.
        /// Düğüm listesi DEĞİŞİNCE çalışır (Update'te değil — CLAUDE.md §6).
        /// </summary>
        private void Sync()
        {
            if (_nodes == null || _grid == null || _root == null) return;

            // 1) Bitmiş / haritadan kalkmış görevlerin fenerini söndür.
            for (int i = _beacons.Count - 1; i >= 0; i--)
            {
                if (IsOpenMandatory(_beacons[i].Coord)) continue;
                if (_beacons[i].Pillar != null) Destroy(_beacons[i].Pillar.gameObject);
                _beacons.RemoveAt(i);
            }

            // 2) Feneri olmayan açık zorunlu göreve sütun dik.
            IReadOnlyList<ChapterNodeManager.MapNode> nodes = _nodes.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ChapterNodeManager.MapNode n = nodes[i];
                if (n.Type != MapNodeType.Mandatory || n.Completed) continue;
                if (Find(n.Coord) != null) continue;
                Add(n);
            }

            RefreshStages();
        }

        private bool IsOpenMandatory(HexCoordinate c)
        {
            ChapterNodeManager.MapNode n = _nodes.NodeAt(c);
            return n != null && n.Type == MapNodeType.Mandatory && !n.Completed;
        }

        private Beacon Find(HexCoordinate c)
        {
            for (int i = 0; i < _beacons.Count; i++)
                if (_beacons[i].Coord.Equals(c)) return _beacons[i];
            return null;
        }

        private void Add(ChapterNodeManager.MapNode n)
        {
            if (!_grid.TryGetCell(n.Coord, out HexCell cell)) return;

            Vector3 ground = cell.WorldPosition + Vector3.up * cell.SurfaceHeight;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"QuestBeacon_{n.Coord.Q}_{n.Coord.R}";
            go.transform.SetParent(_root, false);

            // Efekt tıklamayı/ışını engellemesin: harita tıklaması karoya düşmeli.
            Collider col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

            go.transform.position   = ground + Vector3.up * (_height * 0.5f);
            go.transform.localScale = new Vector3(_calmRadius, _height * 0.5f, _calmRadius);

            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial  = EnsureMaterial();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _beacons.Add(new Beacon
            {
                Coord   = n.Coord,
                Ground  = ground,
                Tier    = n.Tier,
                Pillar  = go.transform,
                Rend    = rend,
                DropDay = _ap != null ? _ap.CurrentDay : 1,
                Phase   = _beacons.Count * 0.7f
            });
        }

        /// <summary>Sütunların kalınlığını ve parlaklığını kademeye göre tazeler.</summary>
        private void RefreshStages()
        {
            int today = _ap != null ? _ap.CurrentDay : 1;
            _anyFresh = false;

            for (int i = 0; i < _beacons.Count; i++)
            {
                Beacon b = _beacons[i];
                b.Fresh = today < b.DropDay + Mathf.Max(0, _freshDays);
                if (b.Fresh) _anyFresh = true;

                if (b.Pillar == null) continue;
                float r = b.Fresh ? _freshRadius : _calmRadius;
                b.Pillar.localScale = new Vector3(r, _height * 0.5f, r);
                if (!b.Fresh) SetEmission(b, _calmEmission);   // sakin sütun sabit yanar
            }
        }

        // Yalnız YENİ fenerlerin nabzı için çalışır; hepsi eskidiyse hiçbir şey yapmaz
        // (sakin sütunların parlaklığı RefreshStages'te bir kez yazılır).
        private void Update()
        {
            if (!_anyFresh) return;

            for (int i = 0; i < _beacons.Count; i++)
            {
                Beacon b = _beacons[i];
                if (!b.Fresh || b.Rend == null) continue;
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * _pulseSpeed + b.Phase);
                SetEmission(b, _freshEmission * pulse);
            }
        }

        // ── Prosedürel malzeme ───────────────────────────────────────────────

        private Material EnsureMaterial()
        {
            if (_material != null) return _material;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _material = new Material(sh) { name = "QuestBeaconMat" };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", _color);
            if (_material.HasProperty("_Color"))     _material.SetColor("_Color",     _color);

            // Keyword MaterialPropertyBlock ile AÇILAMAZ → paylaşılan malzemede bir kez açılır,
            // parlaklık sonra fener başına MPB ile yazılır (malzeme kopyası üretilmez).
            _material.EnableKeyword("_EMISSION");
            _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _material.SetColor("_EmissionColor", _color * _calmEmission);
            return _material;
        }

        // ALAN BAŞLATICISINDA ÜRETİLEMEZ: MaterialPropertyBlock'un yapıcısı motor tarafına iner ve
        // Unity bunu MonoBehaviour'un alan başlatıcısında/statik yapıcısında YASAKLAR ("CreateImpl
        // is not allowed…" → tip başlatıcı patlar, bileşen eklenemez). Bu yüzden ilk kullanımda
        // tembel üretilir (2026-09-03'te batch kurulumunda yakalandı).
        private static MaterialPropertyBlock _mpb;

        private void SetEmission(Beacon b, float intensity)
        {
            if (b.Rend == null) return;
            _mpb ??= new MaterialPropertyBlock();
            b.Rend.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", _color * Mathf.Max(0f, intensity));
            _mpb.SetColor("_BaseColor",     _color);
            b.Rend.SetPropertyBlock(_mpb);
        }
    }
}
