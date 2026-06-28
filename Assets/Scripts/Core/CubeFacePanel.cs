using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>Bir küp yan panelinin hangi yüz + hangi yön olduğunu işaretler — tıklayınca o yöne geçilir.</summary>
    public class CubeFacePanel : MonoBehaviour
    {
        public int Face { get; set; }
        public int Dir  { get; set; } // 0=N 1=E 2=S 3=W
    }
}
