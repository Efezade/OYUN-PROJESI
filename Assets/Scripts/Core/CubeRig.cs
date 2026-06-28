using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Küp illüzyonu + PLANET dönüş mekaniği: aktif üst yüzün 4 komşusunu kenarlardan katlanmış
    /// panel olarak render eder (üst + yanlar). Bir yan panele TIKLAYINCA o yüz üste gelir →
    /// harita değişir, Kam o yüze geçer (gezegen-küp gibi). Arkada dolu gövde.
    /// (Dönüş ANİMASYONU + kenar kesimi sıradaki adım; şimdilik tıkla → anında geçiş.)
    /// </summary>
    public class CubeRig : MonoBehaviour
    {
        [SerializeField] private HexGridManager  _grid;
        [SerializeField] private CubeFaceManager _faces;
        [Tooltip("Yan panellerin aşağı katlanma açısı (90 = dik aşağı; küçültürsen daha açılı).")]
        [SerializeField] private float _foldAngle = 90f;
        [Tooltip("Placeholder (prefabsız) karolar için görsel — HexCell.prefab.")]
        [SerializeField] private GameObject _placeholderTile;

        private Transform _root;
        private Camera    _cam;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _cam = Camera.main;
            Rebuild();
        }

        // Yan yüze tıkla → o yüz üste gelsin (harita değişir, Kam o yüze geçer).
        // Üst grid'e tıklamak hareket içindir (MapInputHandler) — orada CubeFacePanel yok, geçmez.
        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 500f))
            {
                CubeFacePanel panel = hit.collider.GetComponentInParent<CubeFacePanel>();
                if (panel != null)
                {
                    _faces.SwitchToFace(panel.Face);
                    Rebuild();
                }
            }
        }

        /// <summary>Aktif üst yüzün 4 komşusunu kenarlardan katlanmış panel olarak yeniden çizer.</summary>
        public void Rebuild()
        {
            if (_grid == null || _faces == null || !_grid.HasCells) return;
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("CubeSidePanels").transform;
            _root.SetParent(transform, false);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue, topY = 0f;
            foreach (var kv in _grid.Cells)
            {
                Vector3 p = kv.Value.WorldPosition;
                if (p.x < minX) minX = p.x;  if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;  if (p.z > maxZ) maxZ = p.z;
                topY = p.y;
            }
            Vector3 c  = new Vector3((minX + maxX) * 0.5f, topY, (minZ + maxZ) * 0.5f);
            float   ex = maxX - minX, ez = maxZ - minZ;
            int     top = _faces.CurrentFace;

            // dir: 0=N(+Z) 1=E(+X) 2=S(-Z) 3=W(-X). Komşu haritayı bitişik render et, paylaşılan
            // kenardan RotateAround ile aşağı katla. Panel CubeFacePanel ile o yüze işaretlenir.
            int n0 = _faces.Neighbor(top, 0), n1 = _faces.Neighbor(top, 1),
                n2 = _faces.Neighbor(top, 2), n3 = _faces.Neighbor(top, 3);
            BuildSide(n0, c, c + new Vector3(0, 0,  ez), new Vector3(c.x, topY, maxZ), Vector3.right,   +_foldAngle);
            BuildSide(n1, c, c + new Vector3( ex, 0, 0), new Vector3(maxX, topY, c.z), Vector3.forward, -_foldAngle);
            BuildSide(n2, c, c + new Vector3(0, 0, -ez), new Vector3(c.x, topY, minZ), Vector3.right,   -_foldAngle);
            BuildSide(n3, c, c + new Vector3(-ex, 0, 0), new Vector3(minX, topY, c.z), Vector3.forward, +_foldAngle);

            BuildBody(c, ex, ez, topY); // dolu küp gövdesi (boşlukları kapatır → sağlam küp)
        }

        private void BuildSide(int faceNum, Vector3 center, Vector3 panelPos,
                               Vector3 edgeMid, Vector3 edgeAxis, float angle)
        {
            TileMapSO map = _faces.GetFace(faceNum);
            if (map == null) return;
            var panel = new GameObject($"Panel_F{faceNum}").transform;
            panel.SetParent(_root, false);
            panel.gameObject.AddComponent<CubeFacePanel>().Face = faceNum;
            RenderFaceInto(map, panel, center);
            panel.position = panelPos;                     // komşuya bitişik (düz)
            panel.RotateAround(edgeMid, edgeAxis, angle);  // paylaşılan kenardan aşağı katla
        }

        private void RenderFaceInto(TileMapSO map, Transform parent, Vector3 center)
        {
            TilePaletteSO palette = _grid.TilePalette;
            foreach (var kv in _grid.Cells)
            {
                Vector3 local = kv.Value.WorldPosition - center;
                TilePaletteSO.TileEntry entry = FindEntry(palette, map.GetTileId(kv.Key));
                GameObject prefab = entry != null && entry.prefab != null ? entry.prefab : _placeholderTile;
                if (prefab == null) continue;

                GameObject go = Instantiate(prefab, parent);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.identity;
                if (entry != null && entry.prefab == null) Tint(go, entry.editorColor);
            }
        }

        // Hex panellerin arkasına koyu, dolu küp gövdesi: hexler arası boşluklar bunu gösterir,
        // küp sağlam görünür. Görsel + collidersız (oyuncu ışınına/tıklamaya karışmaz).
        private void BuildBody(Vector3 center, float ex, float ez, float topY)
        {
            float depth = Mathf.Max(ex, ez);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CubeBody";
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            body.transform.SetParent(_root, false);
            body.transform.position   = new Vector3(center.x, topY - depth * 0.5f - 0.05f, center.z);
            body.transform.localScale = new Vector3(ex, depth, ez);

            var mr  = body.GetComponent<MeshRenderer>();
            var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            Color dark = new Color(0.07f, 0.07f, 0.10f);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, dark); else mat.color = dark;
            mr.sharedMaterial = mat;
        }

        private static TilePaletteSO.TileEntry FindEntry(TilePaletteSO pal, string id)
        {
            if (pal == null || pal.tiles == null) return null;
            string key = string.IsNullOrEmpty(id) ? "default" : id;
            TilePaletteSO.TileEntry def = null;
            foreach (var t in pal.tiles)
            {
                if (t.id == key)       return t;
                if (t.id == "default") def = t;
            }
            return def ?? (pal.tiles.Count > 0 ? pal.tiles[0] : null);
        }

        private void Tint(GameObject go, Color color)
        {
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, color);
            r.SetPropertyBlock(mpb);
        }
    }
}
