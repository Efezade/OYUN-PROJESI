using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// XCOM / Desperados 3 tarzı yol önizlemesi (overworld): karakterden tıklanan karoya
    /// uzanan şeffaf çizgi + yol üzerindeki her karonun hex çerçevesi. İlk tıkta gösterilir,
    /// ikinci tıkta (MapInputHandler onaylar) gizlenir ve karakter yürür.
    /// Menzil dışı hedef <see cref="_blockedColor"/> ile çizilir (yürünmez, sadece bilgi).
    /// Whitebox: LineRenderer'lar HAVUZLU — Instantiate/Destroy yok, gizlenip yeniden kullanılır.
    /// </summary>
    public class PathPreview : MonoBehaviour
    {
        [Header("Renk")]
        [Tooltip("Menzil içindeki (yürünebilir) yol.")]
        [SerializeField] private Color _reachableColor = new Color(0.40f, 0.92f, 1f, 0.75f);
        [Tooltip("Menzil DIŞI hedef — savaş sisi varken 2 karodan uzağı gösterir (yürünmez).")]
        [SerializeField] private Color _blockedColor   = new Color(1f, 0.35f, 0.28f, 0.70f);

        [Header("Biçim")]
        [Tooltip("Çizginin karo YÜZEYİNDEN yüksekliği (z-fighting olmasın).")]
        [SerializeField] private float _lift         = 0.09f;
        [SerializeField] private float _trailWidth   = 0.10f;
        [SerializeField] private float _outlineWidth = 0.05f;
        [Tooltip("Hedef karo çerçevesinin karo footprint'ine oranı (1 = karo kenarı).")]
        [SerializeField, Range(0.5f, 1f)] private float _footprint = 0.90f;

        private Transform    _root;
        private LineRenderer _trail;                                  // karakterden hedefe çizgi
        private readonly List<LineRenderer> _outlines = new();        // yol karolarının hex çerçeveleri
        private Material _mat;

        public void Show(IReadOnlyList<HexCell> path, bool reachable)
        {
            if (path == null || path.Count < 2) { Hide(); return; }
            EnsureBuilt();

            Color c = reachable ? _reachableColor : _blockedColor;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            if (_mat.HasProperty("_Color"))     _mat.SetColor("_Color",     c);

            // Çizgi: karakterin karosundan (path[0]) hedefe, her karo merkezinden geçerek.
            _trail.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++) _trail.SetPosition(i, Point(path[i]));
            _trail.startColor = _trail.endColor = c;
            _trail.gameObject.SetActive(true);

            // Çerçeveler: BAŞLANGIÇ karosu hariç (karakter zaten orada) her yol karosu.
            int need = path.Count - 1;
            EnsureOutlines(need);
            for (int i = 0; i < _outlines.Count; i++)
            {
                LineRenderer lr = _outlines[i];
                if (i >= need) { lr.gameObject.SetActive(false); continue; }

                Vector3 b = Point(path[i + 1]);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 corner = HexMetrics.Corners[k] * _footprint;
                    lr.SetPosition(k, b + new Vector3(corner.x, 0f, corner.z));
                }
                lr.startColor = lr.endColor = c;
                lr.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_trail != null) _trail.gameObject.SetActive(false);
            for (int i = 0; i < _outlines.Count; i++)
                if (_outlines[i] != null) _outlines[i].gameObject.SetActive(false);
        }

        // Karo yüzeyinin biraz üstü — köprü gibi yüksek karolarda da yola oturur.
        private Vector3 Point(HexCell cell) =>
            cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _lift);

        private void EnsureBuilt()
        {
            if (_root != null) return;

            _root = new GameObject("PathPreview").transform;
            _root.SetParent(transform, false);

            // Saydam Unlit — FogTile.mat ile aynı yöntem (alpha çalışsın, gölge/derinlik yazmasın).
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            _mat = new Material(sh);
            if (_mat.HasProperty("_Surface"))
            {
                _mat.SetFloat("_Surface",  1f);   // 0=opaque, 1=transparent
                _mat.SetFloat("_Blend",    0f);   // alpha blend
                _mat.SetFloat("_ZWrite",   0f);
                _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _trail = NewLine("Trail", _trailWidth, false);
            _trail.gameObject.SetActive(false);
        }

        private void EnsureOutlines(int count)
        {
            while (_outlines.Count < count)
                _outlines.Add(NewLine($"Outline_{_outlines.Count}", _outlineWidth, true));
        }

        private LineRenderer NewLine(string name, float width, bool hexLoop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = hexLoop;
            lr.positionCount     = hexLoop ? 6 : 0;
            lr.widthMultiplier   = width;
            lr.material          = _mat;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            go.SetActive(false);
            return lr;
        }
    }
}
