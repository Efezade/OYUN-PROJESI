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
        [Header("Başlangıç özleri (TEST için)")]
        [Tooltip("Bölüm 1'in gerçek özleri TAŞ ve DOĞA'dır. Ateş/Su/Toprak eski denemeden kalma " +
                 "(GAME_DESIGN §0) — tarifler hâlâ kullanabildiği için alanlar duruyor.")]
        [SerializeField, Min(0)] private int _startAtes;
        [SerializeField, Min(0)] private int _startSu;
        [SerializeField, Min(0)] private int _startToprak;
        [Tooltip("TEST kolaylığı: savaş ekranını denerken birim üretebilmek için bol başlangıç özü " +
                 "(kullanıcı isteği 2026-08-12). Gerçek dengede 0 olacak.")]
        [SerializeField, Min(0)] private int _startTas  = 500;
        [SerializeField, Min(0)] private int _startDoga = 500;

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
            _amounts[(int)EssenceType.Tas]    = Mathf.Max(0, _startTas);
            _amounts[(int)EssenceType.Doga]   = Mathf.Max(0, _startDoga);
            OnChanged?.Invoke();
        }

        /// <summary>Belirtilen türlerdeki HAM özü sıfırlar — bölüm kaybedilince harcanmamış öz
        /// kaybolur (TASK-007). Roster ve kalıcı ilerleme bundan ETKİLENMEZ: harcanan öz zaten
        /// kalıcı birime dönüştüğü için "banka" mekaniği gerekmiyor.</summary>
        public void ClearTypes(params EssenceType[] types)
        {
            if (types == null || types.Length == 0) return;
            foreach (var t in types) _amounts[(int)t] = 0;
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
