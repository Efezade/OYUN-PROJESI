using UnityEngine;
namespace TacticalRPG.Core
{
    /// <summary>Representative ground colour for the baked texture's minimap appearance.</summary>
    public sealed class BiomeTileAppearance : MonoBehaviour
    {
        [SerializeField] private Color _groundColor = Color.white;
        public Color GroundColor => _groundColor;
    }
}
