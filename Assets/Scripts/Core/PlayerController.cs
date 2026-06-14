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
        [SerializeField] private float _moveSpeed    = 8f;
        [SerializeField] private float _heightOffset = 0.15f;

        [Header("Görüş / Kule")]
        [SerializeField] private int _visionRange          = 3;
        [SerializeField] private int _watchtowerRevealRange = 5;

        [Header("Başlangıç Koordinatı")]
        [SerializeField] private HexCoordinate _startCoord;

        public HexCoordinate CurrentCoord { get; private set; }
        public bool          IsMoving     { get; private set; }

        // Faz 1.4 AP/Zaman motoru bu event'i dinleyecek
        public event Action<HexCoordinate> OnMoved;

        private void Start() => Initialize(_startCoord);

        public void Initialize(HexCoordinate startCoord)
        {
            CurrentCoord = startCoord;

            if (_gridManager.TryGetCell(startCoord, out HexCell cell))
                transform.position = cell.WorldPosition + Vector3.up * _heightOffset;

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
                HexCell target    = path[i];
                Vector3 targetPos = target.WorldPosition + Vector3.up * _heightOffset;

                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position, targetPos, _moveSpeed * Time.deltaTime);
                    yield return null;
                }

                transform.position = targetPos;
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
    }
}
