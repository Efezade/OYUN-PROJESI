using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kıyamet Sayacı: 4. günden itibaren her gün sonunda rastgele hex karolar yok olur.
    /// ActionPointManager.OnTimeAdvanced event'ini dinler; gün bitişinde collapse tetiklenir.
    /// Oyuncu üstündeki karo hiçbir zaman silinmez.
    /// </summary>
    public class MapCollapseManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager      _gridManager;
        [SerializeField] private ActionPointManager  _apManager;
        [SerializeField] private PlayerController    _player;
        [SerializeField] private CollapseConfig      _config;

        [Header("Çöküş Görseli")]
        [SerializeField] private Material _collapsedMaterial;

        // Kıyamet istatistikleri
        public int TotalRemovedTiles { get; private set; }
        public bool IsCollapseActive { get; private set; }

        public event Action<int, int> OnTileCollapsed; // (removedCount, totalRemoved)

        private int _lastProcessedDay = 0;

        private void OnEnable()
        {
            if (_apManager != null)
                _apManager.OnTimeAdvanced += HandleTimeAdvanced;
        }

        private void OnDisable()
        {
            if (_apManager != null)
                _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            int slotsPerDay = _apManager != null ? 6 : 6;
            // Sadece gün değişiminde (slot 0'a dönüşte) ve günün son diliminde tetikle
            // slot=0 → yeni gün başladı → bir önceki günün sonu demek
            if (slot != 0) return;
            if (day <= _lastProcessedDay) return;

            _lastProcessedDay = day;
            int removalCount = _config != null ? _config.GetRemovalCount(day - 1) : 0;
            if (removalCount <= 0) return;

            IsCollapseActive = true;
            StartCoroutine(CollapseRoutine(removalCount));
        }

        private IEnumerator CollapseRoutine(int removalCount)
        {
            yield return new WaitForSeconds(0.5f); // Görsel fark edilsin

            var candidates = BuildCandidateList();
            int removed = 0;

            for (int i = 0; i < removalCount && candidates.Count > 0; i++)
            {
                int idx  = UnityEngine.Random.Range(0, candidates.Count);
                HexCell cell = candidates[idx];
                candidates.RemoveAt(idx);

                RemoveTile(cell);
                removed++;
                TotalRemovedTiles++;

                OnTileCollapsed?.Invoke(removed, TotalRemovedTiles);
                Debug.Log($"[Collapse] Karo silindi: {cell.Coordinate} | Toplam: {TotalRemovedTiles}");

                yield return new WaitForSeconds(0.15f); // Her silme arası kısa bekleme
            }

            Debug.Log($"[Collapse] Bu tur {removed} karo yok oldu.");
        }

        private List<HexCell> BuildCandidateList()
        {
            HexCoordinate playerCoord = _player != null ? _player.CurrentCoord : default;
            var candidates = new List<HexCell>(_gridManager.Cells.Count);

            foreach (HexCell cell in _gridManager.Cells.Values)
            {
                if (!cell.IsWalkable)                    continue; // Zaten engel
                if (cell.Coordinate == playerCoord)      continue; // Oyuncu üstü güvenli
                if (cell.CellType == CellType.Watchtower) continue; // Watchtower korunur
                candidates.Add(cell);
            }

            return candidates;
        }

        private void RemoveTile(HexCell cell)
        {
            cell.IsWalkable = false;
            cell.CellType   = CellType.Obstacle;

            if (cell.MeshRenderer != null && _collapsedMaterial != null)
                cell.MeshRenderer.sharedMaterial = _collapsedMaterial;
            else if (cell.Visual != null)
                cell.Visual.SetActive(false);
        }
    }
}
