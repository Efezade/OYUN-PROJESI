namespace TacticalRPG.Grid
{
    /// <summary>Kam'ın davulda kazandığı BÜYÜ türü. Çözümlemesi <c>KamSkillCaster</c>'da,
    /// gösterisi <c>KamSkillVfx</c>'te.</summary>
    public enum KamSkillEffect
    {
        Meteor,    // gökten alev topu — alana hasar
        Heal,      // alandaki HERKESİ iyileştirir (düşman dahil)
        Push,      // alandakileri dışa fırlatır
        Petrify,   // alanı taşlaştırır + herkesi sersemletir
        Pull       // alandakileri merkeze toplar
    }

    /// <summary>
    /// KAM'IN BÜYÜ KARTLARI — davul vuruşunda karo kartlarının yanında çıkan aktif yetenekler.
    ///
    /// KARO ile BÜYÜ arasındaki fark bilinçli bir tasarım gerilimi:
    ///   • KARO  → yalnız Kam'ın 3 karo çevresine konur. Kalıcıdır, tahtayı değiştirir, Kam'ı
    ///             ileri çıkmaya (risk almaya) zorlar.
    ///   • BÜYÜ  → haritanın HERHANGİ bir yerine atılır, bir kez patlar, iz bırakmaz.
    ///             Kam'ı riske sokmaz ama tek kullanımlıktır.
    /// İkisi aynı draftta yarıştığı için her vuruşta "şimdi mi patlatayım, tahtayı mı kurayım"
    /// sorusu doğar. Kullanıcı kuralı 2026-08-13: HER draftta en az bir büyü GARANTİ çıkar.
    ///
    /// Yarıçap okuması: kullanıcı "5 karo çapında" dedi → yarıçap 2 (2+1+2 = 5 karo enine).
    /// Şifa için "5 karo yarıçapında" dendi → yarıçap 5 (91 hex; küçük arenada neredeyse tüm
    /// tahta). Bilinçli: iki tarafı da iyileştirdiği için geniş olması onu güçlü değil, RİSKLİ
    /// yapar. Tek sayı — dengede kısılmak istenirse <see cref="Entry.Radius"/> yeter.
    ///
    /// UnityEngine'e bağımlı DEĞİL (renk float üçlüsü) — denge/tarama araçları Unity açmadan
    /// derleyebilsin diye, <see cref="AugmentCatalog"/> ile aynı gerekçe.
    /// </summary>
    public static class KamSkillCatalog
    {
        public sealed class Entry
        {
            public string         Id;
            public string         Name;
            public string         Description;    // kartta yazan = davranışın sözleşmesi
            public KamSkillEffect Effect;
            public int            Radius;         // hex yarıçapı (0 = tek karo)
            public int            Magnitude;      // hasar / iyileştirme miktarı
            public int            PushDistance;   // alan dışına ek itme (Push)
            public int            StunTurns;      // sersemletme (Petrify)
            public float          R, G, B;        // tema rengi: kart şeridi + hedef göstergesi
            /// <summary>Etki alanı kaç hex kaplar (kartta yazar).</summary>
            public int HexCount => 3 * Radius * Radius + 3 * Radius + 1;
        }

        private static Entry E(string id, string name, string desc, KamSkillEffect effect,
                               int radius, int magnitude, float r, float g, float b,
                               int push = 0, int stun = 0)
            => new Entry { Id = id, Name = name, Description = desc, Effect = effect,
                           Radius = radius, Magnitude = magnitude, PushDistance = push,
                           StunTurns = stun, R = r, G = g, B = b };

        public static readonly Entry[] All =
        {
            E("gok_atesi", "Gök Ateşi",
              "Gökyüzünden devasa bir alev topu düşer. 5 karo çapındaki alandaki HERKES 8 hasar alır.",
              KamSkillEffect.Meteor, 2, 8, 1.00f, 0.42f, 0.12f),

            E("umay_sifasi", "Umay'ın Şifası",
              "Ak ışık iner: 11 karo çapındaki alandaki HERKES 6 can yeniler — düşman dahil.",
              KamSkillEffect.Heal, 5, 6, 1.00f, 0.92f, 0.62f),

            E("yel_ata", "Yel Ata",
              "Sert bir rüzgâr içten dışa patlar: 5 karo çapındaki HERKES alanın dışına, " +
              "oradan 2 karo daha savrulur.",
              KamSkillEffect.Push, 2, 0, 0.62f, 0.86f, 0.95f, 2),

            E("tas_kesilme", "Taş Kesilme",
              "5 karo çapındaki alan taşa döner, altından sarmaşıklar çıkar: içerideki HERKES " +
              "1 tur sersemler.",
              KamSkillEffect.Petrify, 2, 0, 0.72f, 0.70f, 0.66f, 0, 1),

            E("kara_kasirga", "Kara Kasırga",
              "5 karo çapında bir kasırga: alandaki HERKES merkeze doğru çekilir — dost, düşman fark etmez.",
              KamSkillEffect.Pull, 2, 0, 0.55f, 0.48f, 0.72f),
        };

        public static Entry Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>Kart altında görünen etki alanı satırı (karo kartlarıyla aynı dil).</summary>
        public static string AreaLabel(Entry e)
            => $"Etki: {e.HexCount} hex (çap {e.Radius * 2 + 1} karo)";
    }
}
