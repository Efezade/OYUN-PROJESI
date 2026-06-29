using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// 9 harita, 3×3 SNAKE dizilim (9 8 7 / 6 5 4 / 3 2 1). Dik komşuluk (çapraz yok).
    /// Oyuncu haritanın dışına tıklayınca, o yöndeki komşu harita varsa → Kam o kenara YÜRÜR →
    /// (Adım B: öz-top animasyonu) → komşu harita yüklenir, Kam karşı kenarda doğar.
    /// dir: 0=Kuzey(+Z) 1=Doğu(+X) 2=Güney(-Z) 3=Batı(-X).
    /// </summary>
    public class WorldGridManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private PlayerController _player;
        [Tooltip("9 harita (snake): index 0=Harita1 … 8=Harita9.")]
        [SerializeField] private TileMapSO[] _maps = new TileMapSO[9];
        [Tooltip("Geçiş kenarlarına konan fazladan DÜZ karo (HexCell.prefab) — tıklanınca geçilir.")]
        [SerializeField] private GameObject _flatTile;

        public int  CurrentMap { get; private set; } = 1;
        public bool IsBusy => _transitioning;

        private HexPathfinder _pathfinder;
        private bool _transitioning;
        private readonly Dictionary<HexCoordinate, int> _transitionDirs = new();
        private Transform _markersRoot;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _pathfinder = new HexPathfinder();
            StartCoroutine(DeferredMark());
        }

        private IEnumerator DeferredMark()
        {
            yield return null;   // grid + her şey hazır olsun
            MarkBorders();
        }

        // 3×3 snake komşuluğu. Harita yoksa 0 döner (dünya kenarı).
        public int Neighbor(int map, int dir)
        {
            if (map < 1 || map > 9) return 0;
            int r = 2 - (map - 1) / 3;   // grid satırı (0=üst,2=alt)
            int c = 2 - (map - 1) % 3;   // grid sütunu (0=sol,2=sağ)
            switch (dir)
            {
                case 0: c -= 1; break;   // Kuzey +Z (sol-üst) → grid SOL → Harita 2
                case 1: r -= 1; break;   // Doğu  +X (sağ-üst) → grid YUKARI → Harita 4
                case 2: c += 1; break;   // Güney -Z (sağ-alt) → grid sağ
                default: r += 1; break;  // Batı  -X (sol-alt) → grid aşağı
            }
            if (r < 0 || r > 2 || c < 0 || c > 2) return 0;
            return (2 - r) * 3 + (2 - c) + 1;
        }

        public void SwitchToMap(int n)
        {
            if (n < 1 || n > 9 || _maps == null || _maps.Length < 9) return;
            TileMapSO map = _maps[n - 1];
            if (map == null || _grid == null) return;
            CurrentMap = n;
            _grid.SetTileMap(map);
            if (_player != null) _player.Initialize(_player.CurrentCoord);
            MarkBorders();
        }

        // ── SİYAH ÇERÇEVE (geçiş karoları) ───────────────────────────────────
        // Komşusu olan her kenarın YÜRÜNÜR karolarını siyaha boyar + geçiş işaretler.
        public void MarkBorders()
        {
            _transitionDirs.Clear();
            if (_markersRoot != null) Destroy(_markersRoot.gameObject);
            if (_grid == null || !_grid.HasCells) return;
            _markersRoot = new GameObject("TransitionMarkers").transform;
            _markersRoot.SetParent(transform, false);

            ComputeBounds(out Vector3 _, out float ex, out float ez, out float __);
            float sx = ex / Mathf.Max(1, _grid.Width  - 1);
            float sz = ez / Mathf.Max(1, _grid.Height - 1);

            for (int dir = 0; dir < 4; dir++)
            {
                if (Neighbor(CurrentMap, dir) == 0) continue;   // o yönde harita yok (dünya kenarı)
                Vector3 outward = dir switch
                {
                    0 => new Vector3(0f, 0f,  sz),   // Kuzey +Z
                    1 => new Vector3( sx, 0f, 0f),   // Doğu  +X
                    2 => new Vector3(0f, 0f, -sz),   // Güney -Z
                    _ => new Vector3(-sx, 0f, 0f),   // Batı  -X
                };
                foreach (var kv in _grid.Cells)
                {
                    if (!IsOnEdge(kv.Key, dir)) continue;          // yürünür şartı yok — kenar = geçiş
                    _transitionDirs[kv.Key] = dir;
                    AddMarker(kv.Value, outward);
                }
            }
            Debug.Log($"[3x3] Harita {CurrentMap}: {_transitionDirs.Count} gecis karosu, flatTile={(_flatTile != null)}, cells={(_grid.Cells != null ? _grid.Cells.Count : 0)}");
        }

        // Kenar karosunun BİR HÜCRE DIŞINA (haritanın dışına) fazladan DÜZ karo koyar; tıklanınca geçilir.
        private void AddMarker(HexCell cell, Vector3 outward)
        {
            GameObject go;
            if (_flatTile != null)
            {
                go = Instantiate(_flatTile);
                go.transform.SetParent(_markersRoot, false);
                go.transform.position      = cell.WorldPosition + outward;   // kenarla aynı seviyede, düz
                go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.transform.SetParent(_markersRoot, false);
                go.transform.position   = cell.WorldPosition + outward + new Vector3(0f, cell.SurfaceHeight + 0.06f, 0f);
                go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = Vector3.one * 1.7f;
            }
            go.name = "TransitionTile";
            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();   // tıklanabilir
            go.AddComponent<TransitionMarker>().EdgeCoord = cell.Coordinate;
        }

        /// <summary>Geçiş (siyah) karosuysa yönü (0-3), değilse -1.</summary>
        public int IsTransitionCell(HexCoordinate coord) =>
            _transitionDirs.TryGetValue(coord, out int dir) ? dir : -1;

        /// <summary>Siyah geçiş karosuna tıklanınca: Kam oraya yürür → komşu haritaya geçer.</summary>
        public void StartTransition(HexCoordinate coord)
        {
            if (_transitioning || _player == null || _grid == null || _player.IsMoving) return;
            if (!_transitionDirs.TryGetValue(coord, out int dir)) return;
            int target = Neighbor(CurrentMap, dir);
            if (target == 0) return;
            StartCoroutine(TransitionRoutine(dir, target));   // en yakın yürünür kenara yürü → geç
        }

        // MapInputHandler harita DIŞINA tıklanınca çağırır: yön belirle, komşu varsa geç.
        public void HandleOffMapClick(Ray ray)
        {
            if (_transitioning || _player == null || _grid == null || _player.IsMoving) return;
            ComputeBounds(out Vector3 c, out float ex, out float ez, out float topY);

            var plane = new Plane(Vector3.up, new Vector3(0f, topY, 0f));
            if (!plane.Raycast(ray, out float enter)) return;
            Vector3 p = ray.GetPoint(enter);

            int dir = DirectionBeyond(p, c, ex, ez);
            if (dir < 0) return;
            int target = Neighbor(CurrentMap, dir);
            if (target == 0) return;                       // o yönde harita yok (dünya kenarı)
            StartCoroutine(TransitionRoutine(dir, target));
        }

        private IEnumerator TransitionRoutine(int dir, int target)
        {
            _transitioning = true;

            // 1) Kam o yöndeki kenara yürür (zaten kenardaysa atlar).
            if (!IsOnEdge(_player.CurrentCoord, dir))
            {
                HexCell edge = FindEdgeCell(dir, LateralOf(_player.CurrentCoord, dir));
                if (edge != null && _grid.TryGetCell(_player.CurrentCoord, out HexCell start))
                {
                    List<HexCell> path = _pathfinder.FindPath(start, edge, _grid);
                    if (path != null && path.Count >= 2)
                    {
                        _player.MoveAlongPath(path);
                        yield return null;
                        while (_player.IsMoving) yield return null;
                    }
                }
            }

            // (Adım B: burada Kam → öz-top → kaybol)

            // 2) Komşu haritayı yükle, Kam karşı kenarda doğar.
            int lateral = LateralOf(_player.CurrentCoord, dir);
            SwitchToMap(target);
            HexCell entry = FindEdgeCell((dir + 2) % 4, lateral);
            if (entry != null) _player.Initialize(entry.Coordinate);

            // (Adım C: burada harita oluşma efekti + öz-top → Kam)

            _transitioning = false;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────
        private static int DirectionBeyond(Vector3 p, Vector3 c, float ex, float ez)
        {
            float maxX = c.x + ex * 0.5f, minX = c.x - ex * 0.5f;
            float maxZ = c.z + ez * 0.5f, minZ = c.z - ez * 0.5f;
            float oE = p.x - maxX, oW = minX - p.x, oN = p.z - maxZ, oS = minZ - p.z;
            float m = Mathf.Max(Mathf.Max(oE, oW), Mathf.Max(oN, oS));
            if (m <= 0f) return -1;                        // harita içi (geçiş yok)
            if (m == oE) return 1;
            if (m == oW) return 3;
            if (m == oN) return 0;
            return 2;
        }

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
    }
}
