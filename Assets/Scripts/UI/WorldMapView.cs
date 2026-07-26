using UnityEngine;
using UnityEngine.UI;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HARİTA ekranında 3×3 SNAKE dünya düğümlerini <see cref="WorldGridManager.CurrentMap"/>'e göre
    /// CANLI vurgular: bulunulan ada altın, diğerleri sönük. <see cref="WorldGridManager.OnMapChanged"/>
    /// dinlenir (event-driven, CLAUDE.md) → ışınlanınca vurgu kendiliğinden güncellenir.
    ///
    /// <see cref="_nodeBackgrounds"/> dizisi ADA-NUMARASI sırasında: index 0 = Harita1 … 8 = Harita9
    /// (grid yerleşimi editör aracında kurulur; bu bileşen yalnız vurgudan sorumlu — tek sorumluluk).
    /// </summary>
    public class WorldMapView : MonoBehaviour
    {
        [SerializeField] private WorldGridManager _world;

        [Tooltip("9 düğüm zemini, ADA sırasında (index 0 = Harita1 … 8 = Harita9).")]
        [SerializeField] private Image[] _nodeBackgrounds;

        [SerializeField] private Color _currentColor = new Color(0.72f, 0.55f, 0.28f, 1f);
        [SerializeField] private Color _normalColor  = new Color(0.16f, 0.13f, 0.10f, 0.85f);

        private void OnEnable()
        {
            if (_world != null) _world.OnMapChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_world != null) _world.OnMapChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_nodeBackgrounds == null) return;
            int current = _world != null ? _world.CurrentMap : 1;
            for (int i = 0; i < _nodeBackgrounds.Length; i++)
            {
                if (_nodeBackgrounds[i] == null) continue;
                _nodeBackgrounds[i].color = (i + 1 == current) ? _currentColor : _normalColor;
            }
        }
    }
}
