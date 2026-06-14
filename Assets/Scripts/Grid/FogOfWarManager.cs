using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    public enum FogState { Hidden, Explored, Visible }

    /// <summary>
    /// Her hex karosunun görünürlük durumunu tutar ve görsel materyali günceller.
    /// Başlangıçta tüm harita Hidden durumundadır.
    /// </summary>
    public class FogOfWarManager : MonoBehaviour
    {
        [Header("Bağımlılık")]
        [SerializeField] private HexGridManager _gridManager;

        [Header("Placeholder Materyaller")]
        [SerializeField] private Material _hiddenMaterial;
        [SerializeField] private Material _exploredMaterial;
        [SerializeField] private Material _visibleMaterial;

        private Dictionary<HexCoordinate, FogState> _fogStates;

        private void Start()
        {
            InitializeFog();
        }

        private void InitializeFog()
        {
            _fogStates = new Dictionary<HexCoordinate, FogState>(_gridManager.Cells.Count);

            foreach (var kvp in _gridManager.Cells)
            {
                _fogStates[kvp.Key] = FogState.Hidden;
                ApplyFogVisual(kvp.Value, FogState.Hidden);
            }
        }

        // ── Genel API ────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen koordinat etrafında visionRange yarıçaplı alanı açar.
        /// Önceki Visible karolar Explored'a düşer (kalıcı hafıza).
        /// </summary>
        public void RevealArea(HexCoordinate origin, int visionRange)
        {
            // Önceki Visible → Explored
            foreach (HexCoordinate coord in new List<HexCoordinate>(_fogStates.Keys))
            {
                if (_fogStates[coord] == FogState.Visible)
                {
                    _fogStates[coord] = FogState.Explored;
                    if (_gridManager.TryGetCell(coord, out HexCell cell))
                        ApplyFogVisual(cell, FogState.Explored);
                }
            }

            // origin etrafındaki karoları Visible yap
            foreach (HexCoordinate coord in GetCoordsInRange(origin, visionRange))
            {
                _fogStates[coord] = FogState.Visible;
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                    ApplyFogVisual(cell, FogState.Visible);
            }
        }

        public FogState GetFogState(HexCoordinate coord) =>
            _fogStates.TryGetValue(coord, out FogState state) ? state : FogState.Hidden;

        public bool IsVisible(HexCoordinate coord) =>
            GetFogState(coord) == FogState.Visible;

        public bool IsKnown(HexCoordinate coord) =>
            GetFogState(coord) != FogState.Hidden;

        // ── Yardımcı metodlar ─────────────────────────────────────────────

        // Axial range ile merkez dahil tüm koordinatları döner
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

        private void ApplyFogVisual(HexCell cell, FogState state)
        {
            if (cell.Visual == null) return;

            Renderer rend = cell.Visual.GetComponent<Renderer>();
            if (rend == null) return;

            rend.material = state switch
            {
                FogState.Hidden   => _hiddenMaterial,
                FogState.Explored => _exploredMaterial,
                FogState.Visible  => _visibleMaterial,
                _                 => _hiddenMaterial
            };
        }
    }
}
