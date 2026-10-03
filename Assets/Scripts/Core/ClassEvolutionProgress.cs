using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// SINIF EVRİMLERİ (Efe'nin isteği 2026-10-03): her karakter sınıfının üç evrimi var, KİTAP'ın
    /// karakter sayfasında SIRAYLA (I → II → III) özle açılır. Açılan evrim, o sınıftan savaşa
    /// inen her birime işler (<see cref="TraitsFor"/> → <see cref="Unit.ApplyEvolution"/>).
    ///
    /// SINIF BAŞINA, BİRİM BAŞINA DEĞİL: Okçu'nun evrimi bir kez açılır, üretilen her okçu
    /// onu taşır. Kart seviyesi (Sv1-3) ayrı bir eksen olarak kalır.
    ///
    /// SIFIRLANMAZ: bölüm kaybında roster ve seviyeleri korunuyor (ChapterRunManager); evrim de
    /// sınıfın kalıcı gelişimi sayılır. Yetenek/karo ağaçları ise ölünce sıfırlanıyor — bu bilinçli
    /// bir fark, değişirse tek satır (<see cref="ResetProgress"/> çağrısı).
    /// </summary>
    public class ClassEvolutionProgress : MonoBehaviour
    {
        [SerializeField] private EssenceWallet _wallet;

        /// <summary>Bir evrim açıldı (KİTAP sayfası dinler).</summary>
        public event System.Action OnChanged;

        private readonly Dictionary<CharacterClassData, int> _levels = new();

        /// <summary>Sınıfın açık evrim sayısı (0-3).</summary>
        public int LevelOf(CharacterClassData data)
            => data != null && _levels.TryGetValue(data, out int lv) ? lv : 0;

        /// <summary>Sıradaki evrim (yoksa null — hepsi açık ya da sınıfın evrimi yok).</summary>
        public ClassEvolution NextOf(CharacterClassData data)
        {
            if (data == null) return null;
            int lv = LevelOf(data);
            return lv < data.Evolutions.Count ? data.Evolutions[lv] : null;
        }

        public bool CanAffordNext(CharacterClassData data)
        {
            ClassEvolution next = NextOf(data);
            return next != null && _wallet != null && _wallet.CanAfford(next.Cost);
        }

        /// <summary>Sıradaki evrimi açar. Sıra ATLANAMAZ: II, I açılmadan açılmaz.</summary>
        public bool TryUnlockNext(CharacterClassData data)
        {
            ClassEvolution next = NextOf(data);
            if (next == null || _wallet == null) return false;
            if (!_wallet.TrySpend(next.Cost)) return false;

            int lv = LevelOf(data) + 1;
            _levels[data] = lv;
            Debug.Log($"[Evrim] {data.ClassName} {lv}. evrim acildi: {next.Name} — {next.Description}");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Sınıfın AÇIK evrimlerinin toplamı (savaşa inişte birime verilir).</summary>
        public EvolutionTraits TraitsFor(CharacterClassData data)
        {
            var t = new EvolutionTraits();
            if (data == null) return t;
            int lv = LevelOf(data);
            for (int i = 0; i < lv && i < data.Evolutions.Count; i++)
            {
                ClassEvolution e = data.Evolutions[i];
                if (e != null) t.Add(e.Trait, e.Amount);
            }
            return t;
        }

        public void ResetProgress()
        {
            _levels.Clear();
            OnChanged?.Invoke();
        }
    }
}
