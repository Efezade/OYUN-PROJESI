using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// BÜYÜ HEDEF GÖSTERGESİ — fareyle birlikte hareket eden şeffaf etki alanı (LoL'deki
    /// yer hedefli büyülerin göstergesi).
    ///
    /// Kullanıcı kuralı 2026-08-13: "skillerin kapladığı karolar şeffaf renkte bir alanla mouse
    /// ile birlikte hareket etsin ... mouse ile eş zamanlı". Bu yüzden gösterge KAROYA OTURUR
    /// (hangi hexlerin vurulacağı tartışmasız görünür) ama merkezi FARENİN altındaki hexe her
    /// karede yeniden bağlanır.
    ///
    /// İki katman:
    ///   • DOLGU — etkilenen her hexin üstünde şeffaf altıgen (tek birleştirilmiş mesh).
    ///   • ÇEMBER — alanın dış sınırını çizen halka; tahtanın dışına taşan kısımda da görünür,
    ///     böylece kenara nişan alırken alanın ne kadarının boşa gittiği anlaşılır.
    ///
    /// Mesh yalnız MERKEZ ya da YARIÇAP değişince yeniden kurulur — fare her karede oynasa da
    /// aynı hexin üstündeyken tek bir mesh yeniden üretimi olmaz (çöp yok).
    /// </summary>
    public class SkillAreaIndicator : MonoBehaviour
    {
        [SerializeField] private HexGridManager _grid;

        [Header("Görünüm")]
        [Tooltip("Dolgunun saydamlığı (0 = görünmez, 1 = opak).")]
        [SerializeField, Range(0f, 1f)] private float _fillAlpha = 0.34f;
        [Tooltip("Altıgen dolgunun karo üstünden yüksekliği.")]
        [SerializeField] private float _lift = 0.08f;
        [Tooltip("Altıgen dolgu ölçeği (1 = karo tam kaplanır).")]
        [SerializeField, Range(0.5f, 1.05f)] private float _tileScale = 0.94f;
        [Tooltip("Dış çemberin kalınlığı.")]
        [SerializeField] private float _ringWidth = 0.14f;
        [Tooltip("Nabız hızı — gösterge canlı dursun (0 = sabit).")]
        [SerializeField] private float _pulseSpeed = 3.2f;
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmount = 0.16f;

        private Transform     _root;
        private MeshFilter    _fillFilter;
        private MeshRenderer  _fillRenderer;
        private LineRenderer  _ring;
        private Material      _fillMat, _ringMat;
        private Mesh          _fillMesh;

        private HexCoordinate _center;
        private int           _radius = -1;
        private bool          _hasCenter;
        private Color         _color = Color.white;

        /// <summary>Gösterge açık mı?</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>Şu an nişan alınan merkez (geçerliyse true).</summary>
        public bool TryGetCenter(out HexCoordinate center)
        {
            center = _center;
            return _hasCenter;
        }

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (_root != null) return;

            _root = new GameObject("SkillAreaIndicator").transform;
            _root.SetParent(transform, false);

            _fillMat = TransparentMaterial();
            _ringMat = TransparentMaterial();

            var fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
            fill.transform.SetParent(_root, false);
            _fillFilter   = fill.GetComponent<MeshFilter>();
            _fillRenderer = fill.GetComponent<MeshRenderer>();
            _fillRenderer.sharedMaterial    = _fillMat;
            _fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fillRenderer.receiveShadows    = false;

            var ring = new GameObject("Ring");
            ring.transform.SetParent(_root, false);
            _ring = ring.AddComponent<LineRenderer>();
            _ring.useWorldSpace     = true;
            _ring.loop              = true;
            _ring.widthMultiplier   = _ringWidth;
            _ring.sharedMaterial    = _ringMat;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows    = false;

            _root.gameObject.SetActive(false);
        }

        /// <summary>Göstergeyi aç (renk + yarıçap büyüden gelir).</summary>
        public void Show(Color color, int radius)
        {
            EnsureBuilt();
            _color  = color;
            _radius = radius;
            _hasCenter = false;
            _root.gameObject.SetActive(true);
            ApplyColor(1f);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _hasCenter = false;
        }

        /// <summary>Merkezi fare altındaki hexe taşı. Aynı hexteyse iş yapmaz.</summary>
        public void MoveTo(HexCoordinate center)
        {
            EnsureBuilt();
            if (_hasCenter && center == _center) return;

            _center    = center;
            _hasCenter = true;
            RebuildFill();
            RebuildRing();
        }

        private void Update()
        {
            if (!IsVisible || _pulseSpeed <= 0.01f) return;
            // Nabız: saydamlık hafifçe iner çıkar → gösterge "canlı" durur, donmuş bir boya değil.
            float k = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            ApplyColor(1f - _pulseAmount + _pulseAmount * k);
        }

        // ── Geometri ─────────────────────────────────────────────────────────

        private void RebuildFill()
        {
            if (_grid == null) return;

            Mesh proto = HexMetrics.CreateHexMesh(_tileScale);
            Vector3[] pv = proto.vertices;
            int[]     pt = proto.triangles;
            Destroy(proto);

            var verts = new List<Vector3>();
            var tris  = new List<int>();
            float size = _grid.HexSize;

            // Yarıçap içindeki HER hex — tahtada karşılığı olmayanlar da çizilir. Sebep: kenara
            // nişan alırken alanın ne kadarının tahta dışına taştığını görmek KARARIN parçası.
            for (int dq = -_radius; dq <= _radius; dq++)
                for (int dr = Mathf.Max(-_radius, -dq - _radius); dr <= Mathf.Min(_radius, -dq + _radius); dr++)
                {
                    var c = new HexCoordinate(_center.Q + dq, _center.R + dr);
                    Vector3 origin = c.ToWorldPosition(size);
                    float   y      = _grid.TryGetCell(c, out HexCell cell)
                                   ? cell.SurfaceHeight + _lift
                                   : HexMetrics.TileHeight + _lift;

                    int baseIndex = verts.Count;
                    for (int i = 0; i < pv.Length; i++)
                    {
                        Vector3 v = pv[i];
                        // Yalnız ÜST yüz lazım: prizmanın yan yüzleri şeffaf katmanda çift boyama
                        // yapıp göstergeyi koyulaştırırdı. Tepe düzlemine indiriyoruz.
                        v.y = 0f;
                        verts.Add(origin + v + Vector3.up * y);
                    }
                    for (int i = 0; i < pt.Length; i++) tris.Add(baseIndex + pt[i]);
                }

            if (_fillMesh == null) _fillMesh = new Mesh { name = "SkillAreaFill" };
            _fillMesh.Clear();
            _fillMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _fillMesh.SetVertices(verts);
            _fillMesh.SetTriangles(tris, 0);
            _fillMesh.RecalculateNormals();
            _fillMesh.RecalculateBounds();
            _fillFilter.sharedMesh = _fillMesh;
        }

        private void RebuildRing()
        {
            if (_grid == null || _ring == null) return;

            const int segments = 56;
            float size   = _grid.HexSize;
            float radius = (_radius + 0.5f) * Mathf.Sqrt(3f) * size;
            Vector3 c    = _center.ToWorldPosition(size);
            float   y    = (_grid.TryGetCell(_center, out HexCell cell) ? cell.SurfaceHeight : HexMetrics.TileHeight)
                         + _lift + 0.02f;

            _ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * radius, c.y + y, c.z + Mathf.Sin(a) * radius));
            }
        }

        private void ApplyColor(float mul)
        {
            Color fill = _color; fill.a = _fillAlpha * mul;
            Color ring = _color; ring.a = Mathf.Clamp01(0.85f * mul);

            SetMatColor(_fillMat, fill);
            SetMatColor(_ringMat, ring);
            if (_ring != null) _ring.startColor = _ring.endColor = ring;
        }

        private static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        }

        /// <summary>URP'de saydamlık yalnız bu bayrak setiyle açılır (projede kanıtlanmış reçete —
        /// CollapseWaveEffect / AugmentFeedback ile aynı).</summary>
        public static Material TransparentMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface",  1f);
                m.SetFloat("_Blend",    0f);
                m.SetFloat("_ZWrite",   0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
