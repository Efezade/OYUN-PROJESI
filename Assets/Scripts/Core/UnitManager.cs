using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Sahnedeki birimlerin kayıt defteri. Birimler kendini kaydeder (OnEnable/OnDisable).
    /// Hedefleme için "şu koordinatta birim var mı?" sorgusunu sağlar.
    /// </summary>
    public class UnitManager : MonoBehaviour
    {
        private readonly List<Unit> _units = new();

        public IReadOnlyList<Unit> Units => _units;

        public void Register(Unit unit)
        {
            if (unit != null && !_units.Contains(unit)) _units.Add(unit);
        }

        public void Unregister(Unit unit) => _units.Remove(unit);

        /// <summary>Belirtilen koordinattaki yaşayan birimi döndürür (yoksa null).</summary>
        public Unit GetUnitAt(HexCoordinate coord)
        {
            foreach (var u in _units)
                if (u != null && u.IsAlive && u.Coordinate == coord) return u;
            return null;
        }

        /// <summary>İlk düşman birimi (test HUD'u için pratik).</summary>
        public Unit GetFirstEnemy()
        {
            foreach (var u in _units)
                if (u != null && u.Team == UnitTeam.Enemy) return u;
            return null;
        }
    }
}
