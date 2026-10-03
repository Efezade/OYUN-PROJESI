using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// DEĞİŞTİRİLEBİLİR KAROLAR AĞACINDAKİ İLERLEME (2026-10-03): hangi karo açık, kaçıncı
    /// seviyede. Ağacın şekli <see cref="AugmentTreeSO"/>'da, çizimi KİTAP'ın KAROLAR sekmesinde.
    ///
    /// DAVULA BAKAN YÜZ: <see cref="CombatDrumManager"/> karo kartı seçerken
    /// <see cref="IsInPool"/> ile süzer ve kartı <see cref="Scaled"/> kopyasıyla sunar —
    /// büyü ağacıyla aynı kural: ağaç havuzun KALİTESİNİ değiştirir, sürprizi değil.
    ///
    /// SEVİYE NE BÜYÜTÜR: etkinin büyüklüğü (işaret karttan gelir — eksi etkili karo daha da
    /// eksiye gider), yarıçap ve arazi kartlarında karo sayısı. MANA BEDELİ DEĞİŞMEZ.
    ///
    /// KATALOG EZİLMEZ: seviye <see cref="AugmentCatalog"/>'un statik girdisine yazılmaz; her
    /// seferinde kopya üretilir (statik veri savaşlar arası taşınır, geri alınamazdı).
    /// </summary>
    public class AugmentTreeProgress : UpgradeTreeProgress
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private AugmentTreeSO _tree;

        public override UpgradeTreeSO Tree => _tree;

        protected override string LogLabel => "Karo";

        public override string NameOf(string id)
        {
            AugmentCatalog.Entry e = AugmentCatalog.Get(id);
            return e != null ? e.Name : id;
        }

        public override string DescribeCurrent(string id)
        {
            AugmentCatalog.Entry e = AugmentCatalog.Get(id);
            if (e == null) return "Bu düğüm için katalog girdisi bulunamadı.";
            AugmentCatalog.Entry shown = IsUnlocked(id) ? Scaled(e) : e;
            return $"{shown.Description}\n{AugmentCatalog.AreaLabel(shown)}  ·  {shown.ManaCost} mana";
        }

        // ── Davul draftına bakan yüz ─────────────────────────────────────────

        /// <summary>Ağaç atanmışsa yalnız AÇIK karolar draftta çıkar. Ağaç yoksa (eski sahne)
        /// her karo çıkabilir — eski davranış.</summary>
        public bool IsInPool(string id) => _tree == null || IsUnlocked(id);

        /// <summary>Karonun bu seviyedeki KOPYASI. Seviye 1 (ya da ağaçta yoksa) = katalog girdisi.</summary>
        public AugmentCatalog.Entry Scaled(AugmentCatalog.Entry e)
        {
            if (e == null || _tree == null) return e;
            UpgradeTreeSO.Node n = _tree.Find(e.Id);
            int steps = Mathf.Max(0, LevelOf(e.Id) - 1);
            if (n == null || steps == 0) return e;

            // Büyüklük kartın işaretiyle büyür: "−2 hareket" seviyede "−3 hareket" olur.
            int sign      = e.Magnitude < 0 ? -1 : 1;
            int magnitude = e.Magnitude + sign * n.MagnitudePerLevel * steps;
            int radius    = e.Radius    + n.RadiusPerLevel * steps;
            int tiles     = e.TileCount + n.TilesPerLevel  * steps;

            // Açıklama SÖZLEŞMEDİR (kartta yazan = yapılan): metin yeniden yazılmaz, altına
            // seviye satırı eklenir — taban değer ile fark açıkça okunur.
            int level = steps + 1;
            string extra = $"\n\nSEVİYE {level}"
                         + Delta("güç",  e.Magnitude,     magnitude)
                         + Delta("çap",  e.Radius * 2 + 1, radius * 2 + 1, " karo")
                         + Delta("karo", e.TileCount,     tiles);

            return new AugmentCatalog.Entry
            {
                Id                = e.Id,
                Name              = e.Name,
                Description       = e.Description + extra,
                Group             = e.Group,
                Target            = e.Target,
                Effect            = e.Effect,
                Trigger           = e.Trigger,
                Magnitude         = magnitude,
                Radius            = radius,
                TileCount         = tiles,
                VisualId          = e.VisualId,
                OneShot           = e.OneShot,
                FuseRounds        = e.FuseRounds,
                RequiresClass     = e.RequiresClass,
                NeedsRangedSystem = e.NeedsRangedSystem,
                ManaCost          = e.ManaCost      // MANA SEVİYEYLE DEĞİŞMEZ
            };
        }
    }
}
