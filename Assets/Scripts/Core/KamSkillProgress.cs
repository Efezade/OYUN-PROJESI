using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN YETENEK AĞACINDAKİ İLERLEME (2026-09-04): hangi büyü açık, kaçıncı seviyede.
    /// Ağacın şekli <see cref="KamSkillTreeSO"/>'da, çizimi KİTAP ekranında
    /// (<see cref="TacticalRPG.UI.UpgradeTreeView"/>), açma/yükseltme kuralı ortak tabanda
    /// (<see cref="UpgradeTreeProgress"/>), büyüye özgü olan (seviyeli kopya) burada.
    ///
    /// İKİ TÜKETİCİ, TEK KAYNAK:
    ///   • KİTAP ekranı — açma/yükseltme (öz harcar).
    ///   • <see cref="CombatDrumManager"/> — draft havuzunu buradan sorar ve kartı SEVİYELİ
    ///     kopyayla sunar. Böylece ağaç, draftın sürprizini değil KALİTESİNİ değiştirir
    ///     (Efe'nin kararı 2026-09-04).
    ///
    /// SIFIRLANMA: Efe'nin kuralı — **ağaç ölünce sıfırlanır**. Bölüm kaybedilip yeniden
    /// başlatılınca <see cref="ChapterRunManager.RestartChapter"/> buradaki ilerlemeyi siler;
    /// kalıcı avantaj (roguelike meta ekonomi) AYRI bir katman olacak, burası onu bilmez.
    ///
    /// KATALOG EZİLMEZ: seviye, <see cref="KamSkillCatalog"/>'un STATİK girdisine yazılmaz —
    /// statik veri savaş arası taşınır ve bir daha geri alınamazdı. Seviyeli kart her seferinde
    /// <see cref="Scaled"/> ile KOPYA olarak üretilir.
    /// </summary>
    public class KamSkillProgress : UpgradeTreeProgress
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private KamSkillTreeSO _tree;
        [Tooltip("Bölümün kural seti ağacı değiştirebilir (map'e özgü dallar). Atanmazsa hep _tree.")]
        [SerializeField] private ChapterProgress _progress;

        /// <summary>Aktif ağaç: bölümün kural seti seçtiyse o (map'e özgü dallar), yoksa varsayılan.
        /// İlerleme id → seviye sözlüğünde tutulduğu için ağaç değişse de açılmış büyüler kalır.</summary>
        public override UpgradeTreeSO Tree
            => _progress != null && _progress.CurrentRules != null && _progress.CurrentRules.SkillTree != null
               ? _progress.CurrentRules.SkillTree : _tree;

        protected override string LogLabel => "Yetenek";

        public override string NameOf(string id)
        {
            KamSkillCatalog.Entry e = KamSkillCatalog.Get(id);
            return e != null ? e.Name : id;
        }

        public override string DescribeCurrent(string id)
        {
            KamSkillCatalog.Entry e = IsUnlocked(id) ? Scaled(id) : KamSkillCatalog.Get(id);
            if (e == null) return "Bu düğüm için katalog girdisi bulunamadı.";
            return $"{e.Description}\n{KamSkillCatalog.AreaLabel(e)}  ·  {e.ManaCost} mana";
        }

        // ── Davul draftına bakan yüz ─────────────────────────────────────────

        /// <summary>
        /// Draft havuzu: AÇIK büyülerin SEVİYELİ kopyaları. Ağaç atanmamışsa (eski sahne) boş
        /// döner ve davul eski davranışına — katalogdaki her büyüye — geri düşer.
        /// </summary>
        public void FillUnlockedPool(List<KamSkillCatalog.Entry> into)
        {
            if (into == null) return;
            into.Clear();
            if (Tree == null) return;

            foreach (var n in Tree.Nodes)
            {
                if (n == null || !IsUnlocked(n.Id)) continue;
                KamSkillCatalog.Entry e = KamSkillCatalog.Get(n.Id);
                if (e != null) into.Add(Scaled(e, n, LevelOf(n.Id)));
            }
        }

        /// <summary>Katalog girdisinin bu seviyedeki KOPYASI. Seviye 1 = katalog değerleri.</summary>
        public KamSkillCatalog.Entry Scaled(string skillId)
        {
            KamSkillCatalog.Entry e = KamSkillCatalog.Get(skillId);
            UpgradeTreeSO.Node    n = Tree != null ? Tree.Find(skillId) : null;
            return e == null ? null : Scaled(e, n, Mathf.Max(1, LevelOf(skillId)));
        }

        private static KamSkillCatalog.Entry Scaled(KamSkillCatalog.Entry e,
                                                    UpgradeTreeSO.Node n, int level)
        {
            int steps = Mathf.Max(0, level - 1);
            if (n == null || steps == 0) return e;      // seviye 1 → katalog girdisi aynen

            int magnitude = e.Magnitude    + n.MagnitudePerLevel * steps;
            int radius    = e.Radius       + n.RadiusPerLevel    * steps;
            int push      = e.PushDistance + n.PushPerLevel      * steps;
            int stun      = e.StunTurns    + n.StunPerLevel      * steps;

            // Açıklama metni ELLE yazılmış ve içinde sayılar geçiyor (kartta yazan = davranışın
            // sözleşmesi). Metni yeniden üretmek yerine seviye satırı EKLENİR: eski cümle
            // katalogdaki taban değeri anlatmaya devam eder, fark açıkça altında durur.
            string extra = $"\n\nSEVİYE {level}";
            if (n.MagnitudePerLevel != 0) extra += $" · güç {e.Magnitude} → {magnitude}";
            if (n.RadiusPerLevel    != 0) extra += $" · çap {e.Radius * 2 + 1} → {radius * 2 + 1} karo";
            if (n.PushPerLevel      != 0) extra += $" · itme {e.PushDistance} → {push} karo";
            if (n.StunPerLevel      != 0) extra += $" · sersemletme {e.StunTurns} → {stun} tur";

            return new KamSkillCatalog.Entry
            {
                Id           = e.Id,
                Name         = e.Name,
                Description  = e.Description + extra,
                Effect       = e.Effect,
                Radius       = radius,
                Magnitude    = magnitude,
                PushDistance = push,
                StunTurns    = stun,
                R = e.R, G = e.G, B = e.B,
                ManaCost     = e.ManaCost       // MANA SEVİYEYLE DEĞİŞMEZ — bütçe kuralı delinmesin
            };
        }
    }
}
