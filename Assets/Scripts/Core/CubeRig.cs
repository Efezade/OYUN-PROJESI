using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Küp illüzyonu + KARINCA-KÜP geçişi + 90° DÖNÜŞ animasyonu.
    /// Aktif üst yüzün 4 komşusunu kenarlardan katlanmış panel olarak render eder + dolu gövde.
    /// Yan yüze tıklanınca Kam o kenara YÜRÜR; varınca küp 90° DÖNER (küp merkezinde) → hedef yüz
    /// üste gelir, Kam karşı kenara binerek geçer; sonra gerçek yeni harita düz üste oturur (dikişsiz).
    /// </summary>
    public class CubeRig : MonoBehaviour
    {
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private CubeFaceManager  _faces;
        [SerializeField] private PlayerController _player;
        [Tooltip("Yan panellerin aşağı katlanma açısı (90 = dik aşağı).")]
        [SerializeField] private float _foldAngle = 90f;
        [Tooltip("Küp dönüş animasyonu süresi (sn).")]
        [SerializeField] private float _rotDuration = 0.45f;
        [Tooltip("Placeholder (prefabsız) karolar için görsel — HexCell.prefab.")]
        [SerializeField] private GameObject _placeholderTile;

        private Transform     _root;
        private HexPathfinder _pathfinder;
        private bool _crossing;
        private int  _crossFace, _crossDir;

        /// <summary>Geçiş/dönüş sürerken true — bu sırada harita girişi yok sayılmalı.</summary>
        public bool IsBusy => _crossing;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _pathfinder = new HexPathfinder();
            Rebuild();
        }

        // ── KARINCA-KÜP GEÇİŞİ ───────────────────────────────────────────────
        // MapInputHandler yan yüze tıklanınca çağırır. dir: 0=N 1=E 2=S 3=W.
        public void StartCrossing(int face, int dir)
        {
            if (_crossing || _player == null || _grid == null || _player.IsMoving)
            { Debug.Log($"[Cross] ATLANDI (crossing={_crossing} moving={(_player != null && _player.IsMoving)})"); return; }
            _crossing = true; _crossFace = face; _crossDir = dir;

            if (IsOnEdge(_player.CurrentCoord, dir)) { Debug.Log("[Cross] zaten kenarda -> don"); DoCross(); return; }

            HexCell edge = FindEdgeCell(dir, LateralOf(_player.CurrentCoord, dir));
            if (edge == null || !_grid.TryGetCell(_player.CurrentCoord, out HexCell start))
            { Debug.Log("[Cross] kenar yok -> iptal"); _crossing = false; return; }

            List<HexCell> path = _pathfinder.FindPath(start, edge, _grid);
            if (path == null || path.Count < 2)
            { Debug.Log($"[Cross] yol yok -> iptal (kenar {edge.Coordinate})"); _crossing = false; return; }
            Debug.Log($"[Cross] Kam kenara yuruyor: {edge.Coordinate} (dir={dir})");
            _player.MoveAlongPath(path);
            StartCoroutine(WaitArriveThenCross());
        }

        private IEnumerator WaitArriveThenCross()
        {
            yield return null;
            while (_player.IsMoving) yield return null;
            if (_crossing && IsOnEdge(_player.CurrentCoord, _crossDir)) DoCross();
            else { Debug.Log("[Cross] kenara varilamadi -> iptal"); _crossing = false; }
        }

        private void DoCross()
        {
            int lateral = LateralOf(_player.CurrentCoord, _crossDir);
            Debug.Log($"[Cross] DONUS basliyor: yuz {_crossFace} uste (dir={_crossDir})");
            StartCoroutine(RotateAndSwap(_crossFace, _crossDir, lateral));
        }

        // Tüm küpü (üst kopya + yan paneller + Kam) küp merkezinde 90° döndürür, sonra gerçek
        // yeni haritayı düz üste oturtup sıfırlar — dikişsiz.
        private IEnumerator RotateAndSwap(int face, int dir, int lateral)
        {
            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float depth = Mathf.Max(ex, ez);
            Vector3 cubeCenter = new Vector3(c.x, topY - depth * 0.5f, c.z);
            (Vector3 axis, float angle) = AxisAngleFor(dir);

            RenderTopPanel(_faces.GetFace(_faces.CurrentFace), c);  // dönerken küpün üstü
            Transform gridRoot  = _grid.GridRoot;
            gridRoot.gameObject.SetActive(false);                  // gerçek grid'i gizle
            Transform   antParent = _player.transform.parent;
            Quaternion  antRot0   = _player.transform.rotation;
            _player.transform.SetParent(_root, true);              // Kam küple dönsün

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

            _faces.SwitchToFace(face);                             // yeni harita düz üste
            HexCell entry = FindEdgeCell((dir + 2) % 4, lateral);
            if (entry != null) _player.Initialize(entry.Coordinate);
            _player.transform.rotation = antRot0;                  // Kam dik dursun
            Rebuild();
            _crossing = false;
        }

        // Mevcut üst yüzün haritasını düz bir kopya panel olarak _root'a çizer (dönüş için).
        private void RenderTopPanel(TileMapSO map, Vector3 c)
        {
            if (map == null) return;
            var panel = new GameObject("TopPanelCopy").transform;
            panel.SetParent(_root, false);
            panel.position = c;
            RenderFaceInto(map, panel, c);
        }

        // dir yüzünü üste getirecek dönüş: küp merkezi etrafında eksen + açı.
        private (Vector3 axis, float angle) AxisAngleFor(int dir) => dir switch
        {
            0 => (Vector3.right,   -90f), // Kuzey: +Z -> +Y
            1 => (Vector3.forward, +90f), // Doğu : +X -> +Y
            2 => (Vector3.right,   +90f), // Güney: -Z -> +Y
            _ => (Vector3.forward, -90f), // Batı : -X -> +Y
        };

        // ── Kenar / yön yardımcıları ─────────────────────────────────────────
        private bool IsOnEdge(HexCoordinate co, int dir)
        {
            int col = co.Q + (co.R >> 1);
            return dir switch
            {
                0 => co.R == _grid.Height - 1, // Kuzey
                1 => col  == _grid.Width  - 1, // Doğu
                2 => co.R == 0,                // Güney
                _ => col  == 0,                // Batı
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

        // ── KÜP GÖRSELİ ──────────────────────────────────────────────────────
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

        /// <summary>Aktif üst yüzün 4 komşusunu kenarlardan katlanmış panel olarak yeniden çizer.</summary>
        public void Rebuild()
        {
            if (_grid == null || _faces == null || !_grid.HasCells) return;
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("CubeSidePanels").transform;
            _root.SetParent(transform, false);

            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);
            float maxX = c.x + ex * 0.5f, minX = c.x - ex * 0.5f;
            float maxZ = c.z + ez * 0.5f, minZ = c.z - ez * 0.5f;
            int   top  = _faces.CurrentFace;

            int n0 = _faces.Neighbor(top, 0), n1 = _faces.Neighbor(top, 1),
                n2 = _faces.Neighbor(top, 2), n3 = _faces.Neighbor(top, 3);
            BuildSide(n0, 0, c, c + new Vector3(0, 0,  ez), new Vector3(c.x, topY, maxZ), Vector3.right,   +_foldAngle);
            BuildSide(n1, 1, c, c + new Vector3( ex, 0, 0), new Vector3(maxX, topY, c.z), Vector3.forward, -_foldAngle);
            BuildSide(n2, 2, c, c + new Vector3(0, 0, -ez), new Vector3(c.x, topY, minZ), Vector3.right,   -_foldAngle);
            BuildSide(n3, 3, c, c + new Vector3(-ex, 0, 0), new Vector3(minX, topY, c.z), Vector3.forward, +_foldAngle);

            BuildBody(c, ex, ez, topY);
        }

        private void BuildSide(int faceNum, int dir, Vector3 center, Vector3 panelPos,
                               Vector3 edgeMid, Vector3 edgeAxis, float angle)
        {
            TileMapSO map = _faces.GetFace(faceNum);
            if (map == null) return;
            var panel = new GameObject($"Panel_F{faceNum}").transform;
            panel.SetParent(_root, false);
            var cfp = panel.gameObject.AddComponent<CubeFacePanel>();
            cfp.Face = faceNum;
            cfp.Dir  = dir;
            RenderFaceInto(map, panel, center);
            panel.position = panelPos;
            panel.RotateAround(edgeMid, edgeAxis, angle);
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
