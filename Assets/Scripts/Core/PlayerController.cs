using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Oyuncu karakterini hex karolar üzerinde hareket ettirir.
    /// A* yoluyla ilerler, her adımda FogOfWar'ı günceller.
    /// Kule (Watchtower) karosuna girildiğinde geniş alan açılır.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager  _gridManager;
        [SerializeField] private FogOfWarManager _fogManager;

        [Header("Hareket")]
        [SerializeField] private float _moveSpeed     = 8f;
        [SerializeField] private float _heightOffset  = 0.15f;
        // Yüzey ışınının başladığı yükseklik (karoların üstünden aşağı bakar).
        [SerializeField] private float _rayStartHeight = 50f;

        [Header("Görüş / Kule")]
        [SerializeField] private int _visionRange          = 3;
        [SerializeField] private int _watchtowerRevealRange = 5;

        [Header("Başlangıç Koordinatı")]
        [SerializeField] private HexCoordinate _startCoord;

        public HexCoordinate CurrentCoord { get; private set; }
        public bool          IsMoving     { get; private set; }

        // Faz 1.4 AP/Zaman motoru bu event'i dinleyecek
        public event Action<HexCoordinate> OnMoved;

        private void Start()
        {
            if (_gridManager == null) { Debug.LogError("[PlayerController] _gridManager NULL! Faz 1.3'ü yeniden çalıştır."); return; }
            if (_fogManager  == null) { Debug.LogError("[PlayerController] _fogManager NULL! Faz 1.3'ü yeniden çalıştır."); return; }
            Initialize(_startCoord);
        }

        public void Initialize(HexCoordinate startCoord)
        {
            CurrentCoord = startCoord;

            if (_gridManager.TryGetCell(startCoord, out HexCell cell))
                transform.position = GroundedPosition(cell.WorldPosition, cell);
            else
                Debug.LogWarning($"[PlayerController] Başlangıç koordinatı {startCoord} grid'de bulunamadı!");

            _fogManager.RevealArea(CurrentCoord, _visionRange);
        }

        public void MoveAlongPath(List<HexCell> path)
        {
            if (IsMoving || path == null || path.Count < 2) return;
            StartCoroutine(MoveCoroutine(path));
        }

        private IEnumerator MoveCoroutine(List<HexCell> path)
        {
            IsMoving = true;

            for (int i = 1; i < path.Count; i++)
            {
                HexCell target   = path[i];
                Vector3 targetXZ = target.WorldPosition; // hedef yatay konum (y=0)

                // Yatay olarak hedefe ilerle; Y'yi HER KARE yüzeyden ışınla al →
                // engebe/köprü konturunu takip eder (kemerde çıkar, çukurda iner).
                while (HorizontalSqrDistance(transform.position, targetXZ) > 0.0001f)
                {
                    Vector3 curXZ = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 nextXZ = Vector3.MoveTowards(curXZ, targetXZ, _moveSpeed * Time.deltaTime);

                    transform.position = GroundedPosition(nextXZ, target);
                    yield return null;
                }

                transform.position = GroundedPosition(targetXZ, target);
                CurrentCoord       = target.Coordinate;

                _fogManager.RevealArea(CurrentCoord, _visionRange);
                OnMoved?.Invoke(CurrentCoord);

                if (target.CellType == CellType.Watchtower)
                    HandleWatchtower(target);
            }

            IsMoving = false;
        }

        private void HandleWatchtower(HexCell cell)
        {
            _fogManager.RevealArea(cell.Coordinate, _watchtowerRevealRange);
            Debug.Log($"[Player] Kule kesfedildi: {cell.Coordinate} — {_watchtowerRevealRange} menzillik alan acildi.");
        }

        // (x,z) konumunda yüzeyin üstüne oturmuş dünya pozisyonu.
        // Yüzey Y'si ışınla bulunur → düz karoda sabit, köprü/engebede kontur takibi.
        private Vector3 GroundedPosition(Vector3 xz, HexCell fallbackCell)
        {
            float fallback  = fallbackCell != null
                            ? fallbackCell.WorldPosition.y + fallbackCell.SurfaceHeight
                            : 0f;
            float surfaceY  = SampleSurfaceY(xz.x, xz.z, fallback);
            float clearance = _heightOffset - HexMetrics.TileHeight; // karakterin yüzeye göre ofseti
            return new Vector3(xz.x, surfaceY + clearance, xz.z);
        }

        // (x,z)'de en üstteki karo yüzeyinin dünya Y'si. Karakterin kendi collider'ı atlanır.
        private float SampleSurfaceY(float x, float z, float fallback)
        {
            Vector3 origin = new Vector3(x, _rayStartHeight, z);
            var hits = Physics.RaycastAll(origin, Vector3.down, _rayStartHeight + 5f);

            float best = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue; // kendini atla
                if (hits[i].point.y > best) best = hits[i].point.y;
            }
            return best > float.NegativeInfinity ? best : fallback;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
