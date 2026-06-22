using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Çok-tipli öz hazinesi (ÖZ DEPOSU). Her öz türü için ayrı sayaç tutar.
    /// Tek-havuz EssenceManager'ın yerini alır. Haritadan toplanan öz buraya girer,
    /// tarif/üretim buradan harcar. Davranış event'le yayılır (UI dinler).
    /// </summary>
    public class EssenceWallet : MonoBehaviour
    {
        [Header("Başlangıç özleri (test için)")]
        [SerializeField, Min(0)] private int _startAtes;
        [SerializeField, Min(0)] private int _startSu;
        [SerializeField, Min(0)] private int _startToprak;

        private readonly int[] _amounts = new int[Enum.GetValues(typeof(EssenceType)).Length];

        /// <summary>Herhangi bir öz miktarı değişti (UI yenilensin).</summary>
        public event Action OnChanged;

        public int Get(EssenceType t) => _amounts[(int)t];

        public int Total
        {
            get { int s = 0; foreach (var a in _amounts) s += a; return s; }
        }

        private void Start()
        {
            _amounts[(int)EssenceType.Ates]   = Mathf.Max(0, _startAtes);
            _amounts[(int)EssenceType.Su]     = Mathf.Max(0, _startSu);
            _amounts[(int)EssenceType.Toprak] = Mathf.Max(0, _startToprak);
            OnChanged?.Invoke();
        }

        public void Gain(EssenceType t, int amount)
        {
            if (amount <= 0) return;
            _amounts[(int)t] += amount;
            OnChanged?.Invoke();
        }

        public bool CanAfford(IReadOnlyList<EssenceAmount> cost)
        {
            if (cost == null) return true;
            foreach (var c in cost)
                if (_amounts[(int)c.type] < c.amount) return false;
            return true;
        }

        public bool TrySpend(IReadOnlyList<EssenceAmount> cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost != null)
                foreach (var c in cost)
                    _amounts[(int)c.type] -= c.amount;
            OnChanged?.Invoke();
            return true;
        }
    }
}
