using System;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaş formüllerinin TEK yeri. UnityEngine'e bağımlı DEĞİL — denge taraması Unity açmadan
    /// bu kodun aynısını derleyip koşturabilsin diye (terrain üreticisiyle aynı gerekçe).
    ///
    /// NEDEN DEĞİŞTİ (2026-08-12): eski formül düz çıkarmaydı — <c>max(0, ATK − DEF)</c>. Bu, küçük
    /// sayılarda çöküyordu: Goblin (ATK 3) → Savaşçı (DEF 3) = **0 hasar**, yani seviye 1 Savaşçı
    /// goblinlere karşı ölümsüzdü. Ayrıca "+1 hasar" veren bir karo bazı eşleşmelerde %20, bazılarında
    /// sonsuz iyileştirme demek oluyordu (0 → 1) — davul karolarının etki değerleri bu zeminde
    /// hesaplanamazdı.
    ///
    /// YENİ: savunma AZALAN GETİRİLİ bir yüzde indirimi.
    ///   hasar = max(MinimumDamage, yuvarla( ATK × 100 / (100 + DEF × DefenseScale) ))
    /// DefenseScale = 15 ile DEF 1 ≈ %13, DEF 3 ≈ %31, DEF 5 ≈ %43 indirim.
    /// Böylece her ATK ve DEF değeri anlamlı kalır, ±1 modifiyeler her eşleşmede iş görür.
    ///
    /// Sayılar koda GÖMÜLÜ DEĞİL: <c>CombatFormulaSO</c> açılışta <see cref="Configure"/> ile besler
    /// (CLAUDE.md §3). Config verilmezse buradaki varsayılanlar geçerlidir.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Savunmanın azalan getiri katsayısı. Büyük = savunma daha değerli.</summary>
        public static int DefenseScale { get; private set; } = 15;

        /// <summary>Hasarın düşemeyeceği taban. 1 = "her vuruş bir şey yapar".</summary>
        public static int MinimumDamage { get; private set; } = 1;

        /// <summary>Kritik vuruş çarpanı (yüzde). 150 = %50 fazla hasar.</summary>
        public static int CritPercent { get; private set; } = 150;

        /// <summary>Ayarları config'ten yükle (oyun açılışında bir kez).</summary>
        public static void Configure(int defenseScale, int minimumDamage, int critPercent)
        {
            DefenseScale  = Math.Max(0, defenseScale);
            MinimumDamage = Math.Max(0, minimumDamage);
            CritPercent   = Math.Max(100, critPercent);
        }

        /// <summary>Ham saldırı gücünün savunmadan sonra kaç hasara döndüğü.</summary>
        public static int Damage(int attack, int defense)
        {
            if (attack <= 0) return 0;                       // 0 saldırı = 0 hasar (taban uygulanmaz)
            double reduced = attack * 100.0 / (100.0 + Math.Max(0, defense) * (double)DefenseScale);
            int result = (int)Math.Round(reduced, MidpointRounding.AwayFromZero);
            return Math.Max(MinimumDamage, result);
        }

        /// <summary>Kritik vuruşun hasarı.</summary>
        public static int CritDamage(int attack, int defense)
            => Math.Max(MinimumDamage, Damage(attack, defense) * CritPercent / 100);

        /// <summary>Kaç vuruşta ölür (denge aracı / UI ipucu için).</summary>
        public static int HitsToKill(int attack, int defense, int hp)
        {
            int dmg = Damage(attack, defense);
            return dmg <= 0 ? int.MaxValue : (hp + dmg - 1) / dmg;
        }
    }
}
