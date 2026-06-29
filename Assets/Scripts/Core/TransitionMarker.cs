using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Haritanın DIŞINDAki siyah geçiş işaretçisi — hangi kenar karosuna bağlı olduğunu tutar.
    /// Tıklayınca Kam o kenar karosuna yürür → komşu haritaya geçer.</summary>
    public class TransitionMarker : MonoBehaviour
    {
        public HexCoordinate EdgeCoord { get; set; }
    }
}
