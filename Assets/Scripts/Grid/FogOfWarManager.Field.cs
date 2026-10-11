using UnityEngine;

namespace TacticalRPG.Grid
{
    public partial class FogOfWarManager
    {
        private Texture2D _fogField;
        private Color[] _fieldPixels;
        private int _fieldMinQ, _fieldMinR, _fieldWidth, _fieldHeight;
        private bool _fieldDirty, _volumeRendering;
        public Texture2D FogField => _fogField;
        public Vector4 FogFieldBounds => new Vector4(_fieldMinQ, _fieldMinR, _fieldWidth, _fieldHeight);
        public float FieldHexSize => _gridManager != null ? _gridManager.HexSize : 1;
        public bool VolumeRendering => _volumeRendering;
        public bool CanRenderWorldModel(HexCoordinate coordinate) => _volumeRendering || IsVisuallyRevealed(coordinate);

        /// <summary>Presentation only; IsKnown/route/minimap and permanent exploration are unchanged.</summary>
        public void SetVolumeRendering(bool active)
        {
            if (_volumeRendering == active) return;
            _volumeRendering = active;
            BumpExploration();
            foreach (var cap in _caps.Values) ApplyCapAlpha(cap);
        }

        public float VisualOpacity(HexCoordinate coord)
            => _fogEnabled && _caps.TryGetValue(coord, out var cap) ? cap.alpha : 0;

        private void ReleaseFogField()
        {
            if (_fogField != null) Destroy(_fogField);
            _fogField = null;
        }

        private void RebuildFogField()
        {
            if (_gridManager == null || _gridManager.Cells.Count == 0) return;
            int minQ = int.MaxValue, minR = int.MaxValue, maxQ = int.MinValue, maxR = int.MinValue;
            foreach (var coord in _gridManager.Cells.Keys)
            {
                minQ = Mathf.Min(minQ, coord.Q); maxQ = Mathf.Max(maxQ, coord.Q);
                minR = Mathf.Min(minR, coord.R); maxR = Mathf.Max(maxR, coord.R);
            }
            _fieldMinQ = minQ - 2; _fieldMinR = minR - 2;
            _fieldWidth = maxQ - minQ + 5; _fieldHeight = maxR - minR + 5;
            ReleaseFogField();
            _fogField = new Texture2D(_fieldWidth, _fieldHeight, TextureFormat.RGBAHalf, false, true)
            { name = "Continuous world fog", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _fieldPixels = new Color[_fieldWidth * _fieldHeight];
            _fieldDirty = true;
        }

        private void LateUpdate()
        {
            if (!_fieldDirty || _fogField == null) return;
            _fieldDirty = false;
            System.Array.Clear(_fieldPixels, 0, _fieldPixels.Length);
            foreach (var cell in _gridManager.Cells.Values)
            {
                if (!_caps.TryGetValue(cell.Coordinate, out var cap)) continue;
                Color tint = CapBaseColor(cap);
                // The texture stores a small tint offset, not model colour. Day/night mist
                // lighting comes from the compositor, so fog cannot blacken the whole world.
                tint.a = _fogEnabled ? cap.alpha : 0;
                int x = cell.Coordinate.Q - _fieldMinQ, y = cell.Coordinate.R - _fieldMinR;
                _fieldPixels[y * _fieldWidth + x] = tint;
            }
            _fogField.SetPixels(_fieldPixels);
            _fogField.Apply(false, false);
        }
    }
}
