using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Hex karolarının görünürlük durumunu yönetir.
    /// FogState artık HexCell'de tutulur; bu sınıf sadece mantığı uygular.
    /// </summary>
    [DefaultExecutionOrder(-50)] // HexGridManager'dan sonra, PlayerController'dan önce
    public class FogOfWarManager : MonoBehaviour
    {
        [Header("Bağımlılık")]
        [SerializeField] private HexGridManager _gridManager;

        [Header("Placeholder Materyaller")]
        [SerializeField] private Material _hiddenMaterial;
        [SerializeField] private Material _exploredMaterial;
        [SerializeField] private Material _visibleMaterial;

        // Awake'de çalışır: HexGridManager(-100) bittikten, PlayerController(0) başlamadan önce
        private void Awake()
        {
            InitializeFog();
        }

        private void InitializeFog()
        {
            foreach (var cell in _gridManager.Cells.Values)
            {
                cell.FogState = FogState.Hidden;
                ApplyFogVisual(cell);
            }
        }

        // ── Genel API ────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen merkez etrafında visionRange adımlık alanı Visible yapar.
        /// Önceden Visible olan karolar Explored'a (kalıcı keşif) düşer.
        /// </summary>
        public void RevealArea(HexCoordinate origin, int visionRange)
        {
            foreach (var cell in _gridManager.Cells.Values)
            {
                if (cell.FogState == FogState.Visible)
                {
                    cell.FogState = FogState.Explored;
                    ApplyFogVisual(cell);
                }
            }

            foreach (var coord in GetCoordsInRange(origin, visionRange))
            {
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                {
                    cell.FogState = FogState.Visible;
                    ApplyFogVisual(cell);
                }
            }
        }

        public FogState GetFogState(HexCoordinate coord) =>
            _gridManager.TryGetCell(coord, out HexCell c) ? c.FogState : FogState.Hidden;

        public bool IsVisible(HexCoordinate coord) =>
            GetFogState(coord) == FogState.Visible;

        public bool IsKnown(HexCoordinate coord) =>
            GetFogState(coord) != FogState.Hidden;

        // ── Yardımcı metodlar ─────────────────────────────────────────────

        private List<HexCoordinate> GetCoordsInRange(HexCoordinate origin, int range)
        {
            var result = new List<HexCoordinate>();
            for (int q = -range; q <= range; q++)
            {
                int rMin = Mathf.Max(-range, -q - range);
                int rMax = Mathf.Min(range, -q + range);
                for (int r = rMin; r <= rMax; r++)
                {
                    var coord = new HexCoordinate(origin.Q + q, origin.R + r);
                    if (_gridManager.IsInBounds(coord))
                        result.Add(coord);
                }
            }
            return result;
        }

        // HexCell'deki MeshRenderer referansını doğrudan kullanır — GetComponent çağrısı yok
        private void ApplyFogVisual(HexCell cell)
        {
            if (cell.MeshRenderer == null) return;

            cell.MeshRenderer.sharedMaterial = cell.FogState switch
            {
                FogState.Hidden   => _hiddenMaterial,
                FogState.Explored => _exploredMaterial,
                FogState.Visible  => _visibleMaterial,
                _                 => _hiddenMaterial
            };
        }
    }
}
