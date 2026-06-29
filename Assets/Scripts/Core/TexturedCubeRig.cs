using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// DOKULU KÜP (RTT): 6 yüz haritasını MapTextureBaker ile texture'a pişirir + dolu bir küpün
    /// 6 yüzüne basar → kusursuz temiz küp. Bu sürüm STATİK (sadece görünüm doğrulaması).
    /// Dönüş animasyonu + Kam yürüyerek geçiş + oynanan üst yüz gerçek 3D grid → sonraki adım.
    /// Yüz→küp yüzeyi eşlemesi ve oryantasyon deneye-ayarla (gerekirse yön çevrilir).
    /// </summary>
    public class TexturedCubeRig : MonoBehaviour
    {
        [SerializeField] private HexGridManager  _grid;
        [SerializeField] private CubeFaceManager _faces;
        [SerializeField] private GameObject _placeholderTile;
        [SerializeField] private int   _texRes    = 512;
        [SerializeField] private Color _bodyColor = new(0.07f, 0.07f, 0.10f);
        [Tooltip("Doku yüzü içe bakıyorsa işaretle (oryantasyon çevirir).")]
        [SerializeField] private bool _flipFaces = false;
        [Tooltip("Görünüm testi: gerçek grid'i gizle (oynanış sonra; şimdilik sadece küpe bak).")]
        [SerializeField] private bool _hideRealGrid = true;

        private Transform       _cube;
        private RenderTexture[] _rts = new RenderTexture[6];
        private static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start() => Build();

        public void Build()
        {
            if (_grid == null || _faces == null || !_grid.HasCells) return;

            for (int n = 1; n <= 6; n++)
            {
                if (_rts[n - 1] != null) { _rts[n - 1].Release(); _rts[n - 1] = null; }
                _rts[n - 1] = MapTextureBaker.Bake(_faces.GetFace(n), _grid, _placeholderTile, _texRes, _bodyColor);
            }

            // Görünüm testi: gerçek grid küple çakışmasın diye gizle (oynanış entegrasyonu sonra).
            if (_hideRealGrid && _grid.GridRoot != null) _grid.GridRoot.gameObject.SetActive(false);

            BuildCube();
        }

        private void BuildCube()
        {
            if (_cube != null) Destroy(_cube.gameObject);

            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float   E      = Mathf.Max(ex, ez);
            Vector3 center = new Vector3(c.x, topY - E * 0.5f, c.z);

            _cube = new GameObject("TexturedCube").transform;
            _cube.SetParent(transform, false);
            _cube.position = center;

            // Dolu gövde (yüzler bunun hemen dışında).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            var bcol = body.GetComponent<Collider>(); if (bcol != null) Destroy(bcol);
            body.transform.SetParent(_cube, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale    = Vector3.one * E * 0.99f;
            ApplyColor(body.GetComponent<MeshRenderer>(), _bodyColor);

            // 6 yüz (yüz no → küp yüzeyi). Eşleme/oryantasyon sonra dönüşe göre düzeltilecek.
            AddFace(1, Vector3.up,      E);
            AddFace(2, Vector3.right,   E);
            AddFace(3, Vector3.back,    E);
            AddFace(4, Vector3.left,    E);
            AddFace(5, Vector3.forward, E);
            AddFace(6, Vector3.down,    E);
        }

        private void AddFace(int face, Vector3 outward, float E)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = $"Face{face}";
            var qc = q.GetComponent<Collider>(); if (qc != null) Destroy(qc);
            q.transform.SetParent(_cube, false);
            q.transform.localPosition = outward * (E * 0.5f + 0.02f);
            q.transform.localRotation = Quaternion.LookRotation(_flipFaces ? -outward : outward);
            q.transform.localScale    = Vector3.one * E;

            var mr  = q.GetComponent<MeshRenderer>();
            var sh  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(sh);
            RenderTexture rt = _rts[face - 1];
            if (rt != null)
            {
                if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, rt);
                mat.mainTexture = rt;
            }
            else ApplyColorMat(mat, _bodyColor);
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

        private void ApplyColor(MeshRenderer mr, Color col)
        {
            var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            ApplyColorMat(mat, col);
            mr.sharedMaterial = mat;
        }

        private void ApplyColorMat(Material mat, Color col)
        {
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, col); else mat.color = col;
        }
    }
}
