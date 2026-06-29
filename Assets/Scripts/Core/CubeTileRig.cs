using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// GERÇEK KARO küp (Adım 1 — statik görünüm testi): 6 yüze gerçek 3D karolar basar, her yüzü
    /// KENDİ yerel çerçevesinde KENARDAN KESER (TileClip shader) → düz kenar, çıkıntı yok, karo
    /// YÜKSEKLİĞİ korunur (PNG değil). Dönüş + oynanış (üst yüz gerçek grid) sonraki adım.
    /// _clipExtra ile kenarın nereden kesileceği ayarlanır.
    /// </summary>
    public class CubeTileRig : MonoBehaviour
    {
        [SerializeField] private HexGridManager  _grid;
        [SerializeField] private CubeFaceManager _faces;
        [SerializeField] private GameObject _placeholderTile;
        [SerializeField] private Color _bodyColor = new(0.07f, 0.07f, 0.10f);
        [Tooltip("Kenarı karo merkez hattından ne kadar DIŞARIDAN kessin (büyük=daha çok karo görünür).")]
        [SerializeField] private float _clipExtra = 0.35f;
        [Tooltip("Görünüm testi: gerçek grid'i gizle (oynanış sonra).")]
        [SerializeField] private bool _hideRealGrid = true;

        private Transform _cube;
        private Shader    _clipShader;
        private static readonly int BaseMapId    = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");
        private static readonly int ClipW2LId    = Shader.PropertyToID("_TileClipW2L");
        private static readonly int ClipExtentId = Shader.PropertyToID("_TileClipExtent");

        private void Start() => Build();

        public void Build()
        {
            if (_grid == null || _faces == null || !_grid.HasCells) return;
            _clipShader = Shader.Find("TacticalRPG/TileClip");
            if (_clipShader == null) { Debug.LogError("[CubeTile] TileClip shader bulunamadi!"); _clipShader = Shader.Find("Universal Render Pipeline/Lit"); }
            if (_cube != null) Destroy(_cube.gameObject);

            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float   E          = Mathf.Max(ex, ez);
            Vector3 cubeCenter = new Vector3(c.x, topY - E * 0.5f, c.z); // üst yüz grid seviyesinde

            _cube = new GameObject("CubeTiles").transform;
            _cube.SetParent(transform, false);
            _cube.position = cubeCenter;

            BuildBody(E);

            float hx = ex * 0.5f, hz = ez * 0.5f;
            Vector3 extent = new Vector3(hx + _clipExtra, E, hz + _clipExtra); // yanal kes, yükseklik serbest

            //         yüz  normal           yönelim (+Y = yüz normali)         extent (yüze göre)
            BuildFace(1, Vector3.up,      Quaternion.identity,       E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));
            BuildFace(2, Vector3.right,   Quaternion.Euler(0,0,-90), E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));
            BuildFace(3, Vector3.back,    Quaternion.Euler(-90,0,0), E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));
            BuildFace(4, Vector3.left,    Quaternion.Euler(0,0,90),  E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));
            BuildFace(5, Vector3.forward, Quaternion.Euler(90,0,0),  E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));
            BuildFace(6, Vector3.down,    Quaternion.Euler(180,0,0), E, c, new Vector3(hx + _clipExtra, E, hz + _clipExtra));

            if (_hideRealGrid && _grid.GridRoot != null) _grid.GridRoot.gameObject.SetActive(false);
        }

        // Bir yüzü kur: konumla/yönlendir → karoları yerel düzlemde bas (taban Y=0, yükseklik +Y) → kenardan kes.
        private void BuildFace(int face, Vector3 normal, Quaternion rot, float E, Vector3 gridCenter, Vector3 extent)
        {
            TileMapSO map = _faces.GetFace(face);
            if (map == null) return;

            var fc = new GameObject($"Face{face}").transform;
            fc.SetParent(_cube, false);
            fc.localPosition = normal * (E * 0.5f);
            fc.localRotation = rot;

            Matrix4x4 w2l = fc.worldToLocalMatrix;
            TilePaletteSO pal = _grid.TilePalette;
            foreach (var kv in _grid.Cells)
            {
                Vector3 wp = kv.Value.WorldPosition;
                Vector3 local = new Vector3(wp.x - gridCenter.x, 0f, wp.z - gridCenter.z);
                TilePaletteSO.TileEntry entry = FindEntry(pal, map.GetTileId(kv.Key));
                GameObject prefab = entry != null && entry.prefab != null ? entry.prefab : _placeholderTile;
                if (prefab == null) continue;

                GameObject go = Instantiate(prefab, fc);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.identity;
                bool placeholder = entry == null || entry.prefab == null;
                Color phCol = entry != null ? entry.editorColor : Color.gray;
                ApplyClip(go, w2l, extent, placeholder, phCol);
            }
        }

        private void ApplyClip(GameObject tile, Matrix4x4 w2l, Vector3 extent, bool placeholder, Color phColor)
        {
            foreach (Renderer r in tile.GetComponentsInChildren<Renderer>())
            {
                Material orig = r.sharedMaterial;
                var mat = new Material(_clipShader);
                if (orig != null)
                {
                    if (orig.HasProperty(BaseMapId))   mat.SetTexture(BaseMapId, orig.GetTexture(BaseMapId));
                    if (orig.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, orig.GetColor(BaseColorId));
                }
                if (placeholder) mat.SetColor(BaseColorId, phColor);
                mat.SetMatrix(ClipW2LId, w2l);
                mat.SetVector(ClipExtentId, extent);
                r.sharedMaterial = mat;
            }
        }

        private void BuildBody(float E)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            var col = body.GetComponent<Collider>(); if (col != null) Destroy(col);
            body.transform.SetParent(_cube, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale    = Vector3.one * E * 0.96f;
            var mr  = body.GetComponent<MeshRenderer>();
            var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, _bodyColor); else mat.color = _bodyColor;
            mr.sharedMaterial = mat;
        }

        private void ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            topY = 0f;
            foreach (var kv in _grid.Cells)
            {
                Vector3 p = kv.Value.WorldPosition;
                if (p.x < minX) minX = p.x;  if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;  if (p.z > maxZ) maxZ = p.z;
                topY = p.y;
            }
            c  = new Vector3((minX + maxX) * 0.5f, topY, (minZ + maxZ) * 0.5f);
            ex = maxX - minX; ez = maxZ - minZ;
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
    }
}
