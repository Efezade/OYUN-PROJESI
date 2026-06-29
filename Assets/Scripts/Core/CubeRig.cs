using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// DOKULU KÜP + KARINCA-KÜP geçişi + 90° dönüş. Aktif üst yüz = GERÇEK 3D hex grid (oynanış);
    /// 4 komşu + alt yüz = MapTextureBaker ile pişirilmiş DOKU (temiz). Yan yüze tıkla → Kam o
    /// kenara YÜRÜR → küp 90° DÖNER → hedef yüz üste, Kam karşı kenara biner; sonra gerçek yeni
    /// harita düz üste oturur. Dokular bir kez pişirilir (geçişte yükleme yok).
    /// </summary>
    public class CubeRig : MonoBehaviour
    {
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private CubeFaceManager  _faces;
        [SerializeField] private PlayerController _player;
        [Tooltip("Küp dönüş animasyonu süresi (sn).")]
        [SerializeField] private float _rotDuration = 0.45f;
        [SerializeField] private int   _texRes    = 512;
        [SerializeField] private Color _bodyColor = new(0.07f, 0.07f, 0.10f);
        [Tooltip("Doku içe bakıyorsa işaretle.")]
        [SerializeField] private bool  _flipFaces = false;
        [Tooltip("Placeholder karolar (pişirme için) — HexCell.prefab.")]
        [SerializeField] private GameObject _placeholderTile;

        private Transform       _root;
        private HexPathfinder   _pathfinder;
        private RenderTexture[] _rts = new RenderTexture[6];
        private bool _crossing;
        private int  _crossFace, _crossDir;
        private bool _baked;

        public bool IsBusy => _crossing;

        private static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _pathfinder = new HexPathfinder();
            StartCoroutine(BakeThenBuild());
        }

        private IEnumerator BakeThenBuild()
        {
            // URP-uyumlu async pişirme (kareyi bekler). Bitince küpü kur.
            yield return MapTextureBaker.BakeAllRoutine(_faces, _grid, _placeholderTile, _texRes, _bodyColor, _rts);
            _baked = true;
            Rebuild();
        }

        // ── GEÇİŞ ────────────────────────────────────────────────────────────
        public void StartCrossing(int face, int dir)
        {
            if (_crossing || _player == null || _grid == null || _player.IsMoving) return;
            _crossing = true; _crossFace = face; _crossDir = dir;

            if (IsOnEdge(_player.CurrentCoord, dir)) { DoCross(); return; }

            HexCell edge = FindEdgeCell(dir, LateralOf(_player.CurrentCoord, dir));
            if (edge == null || !_grid.TryGetCell(_player.CurrentCoord, out HexCell start))
            { _crossing = false; return; }

            List<HexCell> path = _pathfinder.FindPath(start, edge, _grid);
            if (path == null || path.Count < 2) { _crossing = false; return; }
            _player.MoveAlongPath(path);
            StartCoroutine(WaitArriveThenCross());
        }

        private IEnumerator WaitArriveThenCross()
        {
            yield return null;
            while (_player.IsMoving) yield return null;
            if (_crossing && IsOnEdge(_player.CurrentCoord, _crossDir)) DoCross();
            else _crossing = false;
        }

        private void DoCross()
        {
            int lateral = LateralOf(_player.CurrentCoord, _crossDir);
            StartCoroutine(RotateAndSwap(_crossFace, _crossDir, lateral));
        }

        // Tüm küpü (dokulu yan yüzler + gövde + üst doku kopyası + Kam) küp merkezinde 90° döndürür.
        private IEnumerator RotateAndSwap(int face, int dir, int lateral)
        {
            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float depth = Mathf.Max(ex, ez);
            Vector3 cubeCenter = new Vector3(c.x, topY - depth * 0.5f, c.z);
            (Vector3 axis, float angle) = AxisAngleFor(dir);

            RenderTopPanel(_faces.CurrentFace, c, ex, ez, topY);   // dönerken küpün üst dokusu
            Transform gridRoot = _grid.GridRoot;
            gridRoot.gameObject.SetActive(false);                  // gerçek grid'i gizle
            Transform   antParent = _player.transform.parent;
            Quaternion  antRot0   = _player.transform.rotation;
            _player.transform.SetParent(_root, true);

            Vector3 origPos = _root.position; Quaternion origRot = _root.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, _rotDuration);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)) * angle;
                _root.position = origPos; _root.rotation = origRot;
                _root.RotateAround(cubeCenter, axis, a);
                yield return null;
            }

            _root.position = origPos; _root.rotation = origRot;
            _player.transform.SetParent(antParent, true);
            gridRoot.gameObject.SetActive(true);

            _faces.SwitchToFace(face);
            HexCell entry = FindEdgeCell((dir + 2) % 4, lateral);
            if (entry != null) _player.Initialize(entry.Coordinate);
            _player.transform.rotation = antRot0;
            Rebuild();
            _crossing = false;
        }

        private (Vector3 axis, float angle) AxisAngleFor(int dir) => dir switch
        {
            0 => (Vector3.right,   -90f),
            1 => (Vector3.forward, +90f),
            2 => (Vector3.right,   +90f),
            _ => (Vector3.forward, -90f),
        };

        private bool IsOnEdge(HexCoordinate co, int dir)
        {
            int col = co.Q + (co.R >> 1);
            return dir switch
            {
                0 => co.R == _grid.Height - 1,
                1 => col  == _grid.Width  - 1,
                2 => co.R == 0,
                _ => col  == 0,
            };
        }

        private int LateralOf(HexCoordinate co, int dir) =>
            (dir == 1 || dir == 3) ? co.R : co.Q + (co.R >> 1);

        private HexCell FindEdgeCell(int dir, int lateral)
        {
            HexCell best = null, anyBest = null;
            int bD = int.MaxValue, bDAny = int.MaxValue;
            foreach (var kv in _grid.Cells)
            {
                if (!IsOnEdge(kv.Key, dir)) continue;
                int d = Mathf.Abs(LateralOf(kv.Key, dir) - lateral);
                if (d < bDAny) { bDAny = d; anyBest = kv.Value; }
                if (kv.Value.IsWalkable && kv.Value.FogState != FogState.Hidden && d < bD)
                { bD = d; best = kv.Value; }
            }
            return best ?? anyBest;
        }

        // ── KÜP GÖRSELİ (dokulu) ─────────────────────────────────────────────
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

        /// <summary>Üst = gerçek grid; 4 komşu + alt yüz dokulu quad + dolu gövde.</summary>
        public void Rebuild()
        {
            if (_grid == null || _faces == null || !_grid.HasCells) return;
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("CubeSides").transform;
            _root.SetParent(transform, false);

            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float depth = Mathf.Max(ex, ez);
            float maxX = c.x + ex * 0.5f, minX = c.x - ex * 0.5f;
            float maxZ = c.z + ez * 0.5f, minZ = c.z - ez * 0.5f;
            float midY = topY - depth * 0.5f;
            int   top  = _faces.CurrentFace;

            int n0 = _faces.Neighbor(top, 0), n1 = _faces.Neighbor(top, 1),
                n2 = _faces.Neighbor(top, 2), n3 = _faces.Neighbor(top, 3);
            SideQuad(n0, 0, new Vector3(c.x, midY, maxZ), Vector3.forward, ex, depth);
            SideQuad(n1, 1, new Vector3(maxX, midY, c.z), Vector3.right,   ez, depth);
            SideQuad(n2, 2, new Vector3(c.x, midY, minZ), Vector3.back,    ex, depth);
            SideQuad(n3, 3, new Vector3(minX, midY, c.z), Vector3.left,    ez, depth);
            SideQuad(OppositeFace(top, n0, n1, n2, n3), -1, new Vector3(c.x, topY - depth, c.z),
                     Vector3.down, ex, ez);

            BuildBody(c, ex, ez, topY);
        }

        // Bir yüzü dokulu quad olarak küpün ilgili yüzeyine koyar. dir>=0 ise tıklanabilir (geçiş).
        private void SideQuad(int faceNum, int dir, Vector3 faceCenter, Vector3 outward, float w, float h)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = $"Face_F{faceNum}";
            var qc = q.GetComponent<Collider>(); if (qc == null) qc = q.AddComponent<MeshCollider>();
            q.transform.SetParent(_root, false);
            q.transform.position   = faceCenter + outward * 0.03f;
            q.transform.rotation   = Quaternion.LookRotation(_flipFaces ? -outward : outward,
                                       outward == Vector3.down || outward == Vector3.up ? Vector3.forward : Vector3.up);
            q.transform.localScale = new Vector3(w, h, 1f);
            if (dir >= 0) { var cfp = q.AddComponent<CubeFacePanel>(); cfp.Face = faceNum; cfp.Dir = dir; }
            ApplyTexture(q.GetComponent<MeshRenderer>(), faceNum);
        }

        private void RenderTopPanel(int face, Vector3 c, float ex, float ez, float topY)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "TopCopy";
            var qc = q.GetComponent<Collider>(); if (qc != null) Destroy(qc);
            q.transform.SetParent(_root, false);
            q.transform.position   = new Vector3(c.x, topY + 0.03f, c.z);
            q.transform.rotation   = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            q.transform.localScale = new Vector3(ex, ez, 1f);
            ApplyTexture(q.GetComponent<MeshRenderer>(), face);
        }

        private void ApplyTexture(MeshRenderer mr, int faceNum)
        {
            RenderTexture rt = (faceNum >= 1 && faceNum <= 6) ? _rts[faceNum - 1] : null;
            var sh  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(sh);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // çift taraflı (dönerken arka yüz siyah olmasın)
            if (rt != null) { if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, rt); mat.mainTexture = rt; }
            else if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, _bodyColor);
            mr.sharedMaterial = mat;
        }

        private static int OppositeFace(int top, int a, int b, int c, int d)
        {
            for (int f = 1; f <= 6; f++)
                if (f != top && f != a && f != b && f != c && f != d) return f;
            return top;
        }

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
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, _bodyColor); else mat.color = _bodyColor;
            mr.sharedMaterial = mat;
        }
    }
}
