using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Davul vuruşunda çıkan kartın grubu. Draft normalde her gruptan bir tane sunar —
    /// üçü de artı olsaydı seçim "bedava güç" olur, karar kalmazdı.</summary>
    public enum AugmentGroup
    {
        Kut,        // yalnız YANDAŞA artı
        Kargis,     // yalnız DÜŞMANA eksi
        Notr,       // HERKESE etki (iki ağızlı) ya da arazi değişimi
        Patlayici,  // tetiklenince bir kez patlar
        Sinifsal    // nadir — belirli bir sınıfa özel
    }

    /// <summary>Karonun ne yaptığı. Çözümlemesi savaş sistemlerinde; burada yalnız TANIM var.</summary>
    public enum AugmentEffect
    {
        None,
        Initiative,     // sıra sıralamasında öne/geriye (+/-)
        Damage,         // saldırı gücü (+/-)
        Defense,        // savunma (+/-)
        Move,           // hareket menzili (+/-)
        Regen,          // tur başı can (+/-)
        Accuracy,       // isabet yüzdesi (+/-)  [menzil sistemi gelince etkinleşir]
        Range,          // saldırı menzili (+)   [menzil sistemi gelince etkinleşir]
        ExtraAction,    // o tur fazladan aksiyon
        Stun,           // üstüne gelen birim 1 tur aksiyon yapamaz
        EntryDamage,    // karoya girişte hasar
        Explode,        // tetiklenince alan hasarı, sonra karo tükenir
        Impassable,     // geçilemez (duvar/boşluk)
        BlockSight      // görüş hattını keser [menzil sistemi gelince etkinleşir]
    }

    /// <summary>Kimi etkiler.</summary>
    public enum AugmentTarget { Allies, Enemies, Everyone }

    /// <summary>
    /// Davul vuruşunda Kam'a sunulan KARO KARTLARI havuzu.
    ///
    /// Havuz boyutu 24: savaş başına 2-5 vuruş × 3 kart = 6-15 kart görülür, bölüm başına ~200.
    /// 24 tip hem tekrar yorgunluğu yaratmaz hem öğrenilebilir kalır (TFT/Balatro aralığı 20-40).
    ///
    /// YARIÇAP KURALI — güç × alan ≈ sabit. 80 karoluk arenada r0 = %1.2, r1 = %8.5, r2 = %23
    /// kaplar; onun için sert etkiler (sersemletme, +2 hasar, patlama) r0'da, orta etkiler
    /// (hareket, savunma, sıra) r1'de, zayıf-geniş etkiler r2'de durur.
    ///
    /// Karolar SINIFA ÖZEL DEĞİLDİR (kullanıcı kuralı 2026-08-12) — tek istisna nadir
    /// <see cref="AugmentGroup.Sinifsal"/> kartlar, o da yalnız sahada o sınıf varken çıkar.
    ///
    /// UnityEngine'e bağımlı değil — denge taraması Unity açmadan koşabilsin diye.
    /// </summary>
    public static class AugmentCatalog
    {
        public sealed class Entry
        {
            public string        Id;
            public string        Name;
            public string        Description;   // karta yazılan açıklama
            public AugmentGroup  Group;
            public AugmentTarget Target;
            public AugmentEffect Effect;
            public int           Magnitude;     // etkinin büyüklüğü (işaretli)
            public int           Radius;        // 0 = tek hex, 1 = 7 hex, 2 = 19 hex
            public int           TileCount = 1; // kaç karo yerleştirilir (Duvar 3, Kutsal Zemin 3)
            public string        VisualId;      // TileCatalog id'si (görsel)
            /// <summary>Sadece bu sınıf sahadayken çıkar (Sinifsal kartlar). null = herkese açık.</summary>
            public string        RequiresClass;
            /// <summary>Menzil/görüş hattı sistemi gelmeden ETKİSİ ÇALIŞMAZ — draft'tan elenir.</summary>
            public bool          NeedsRangedSystem;
        }

        private static Entry E(string id, string name, string desc, AugmentGroup g, AugmentTarget t,
                               AugmentEffect e, int mag, int radius, string visual,
                               int tiles = 1, string cls = null, bool needsRanged = false)
            => new Entry { Id = id, Name = name, Description = desc, Group = g, Target = t, Effect = e,
                           Magnitude = mag, Radius = radius, VisualId = visual, TileCount = tiles,
                           RequiresClass = cls, NeedsRangedSystem = needsRanged };

        public static readonly Entry[] All =
        {
            // ── KUT (yandaşa +) ──────────────────────────────────────────────
            E("ata_tasi",    "Ata Taşı",     "Üstündeki yandaş sırada ÖNE GEÇER (+3 inisiyatif).",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Initiative, +3, 1, TileCatalog.Dikilitas),
            E("kalkan_tasi", "Kalkan Taşı",  "Üstündeki yandaş +2 savunma kazanır.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Defense, +2, 0, TileCatalog.KayaYigini),
            E("ruzgar_tasi", "Rüzgâr Taşı",  "Üstündeki yandaş +1 hareket kazanır.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Move, +1, 1, TileCatalog.UzunOt),
            E("ocak",        "Ocak",         "Üstündeki yandaş her tur başında 2 can yeniler.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Regen, +2, 1, TileCatalog.KaynakGolu),
            E("ofke_tasi",   "Öfke Taşı",    "Üstündeki yandaş +2 hasar verir. Tek karo — dar ama sert.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Damage, +2, 0, TileCatalog.ObsidyenTarla),

            // ── KARGIŞ (düşmana −) ───────────────────────────────────────────
            E("tuzak_tasi",  "Tuzak Taşı",   "Üstüne gelen düşman SERSEMLER — o tur hamle yapamaz.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Stun, 1, 0, TileCatalog.KadimKapi),
            E("camur",       "Çamur",        "İçindeki düşmanın hareketi 2 azalır.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Move, -2, 1, TileCatalog.Bataklik),
            E("korku_sisi",  "Korku Sisi",   "İçindeki düşman %30 daha az isabet eder.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Accuracy, -30, 1, TileCatalog.SisPerdesi,
              1, null, true),
            E("diken",       "Diken Tarlası","İçine giren düşman 3 hasar alır.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.EntryDamage, 3, 1, TileCatalog.DikenliCalilik),
            E("agirlik",     "Ağırlık Taşı", "İçindeki düşman sırada GERİYE DÜŞER (−3 inisiyatif).",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Initiative, -3, 1, TileCatalog.DevKaya),

            // ── NÖTR (herkese / arazi) ───────────────────────────────────────
            E("sarsinti",    "Sarsıntı Hattı","Üstündeki HERKES −2 savunma. İki ağızlı: seni de keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.Defense, -2, 1, TileCatalog.Krater),
            E("ruh_kapisi",  "Ruh Kapısı",   "Üstünde tur başlatan HERKES +1 aksiyon kazanır.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.ExtraAction, +1, 0, TileCatalog.Sunak),
            E("duvar",       "Taş Duvar",    "3 karoluk geçilemez duvar örer; görüşü de keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.Impassable, 0, 0, TileCatalog.Kayalik, 3),
            E("bosluk",      "Boşluk",       "Karoyu uçuruma çevirir: geçilemez, görüşü keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.BlockSight, 0, 0, TileCatalog.Ucurum),

            // ── PATLAYICI (bir kez tetiklenir) ───────────────────────────────
            E("ates_ficisi", "Ateş Fıçısı",  "Saldırı alınca patlar: çevresine 5 hasar.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Explode, 5, 1, TileCatalog.VolkanikKaya),
            E("buz_kabugu",  "Buz Kabuğu",   "İlk giren birim DONAR (1 tur), sonra karo kırılır.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Stun, 1, 0, TileCatalog.BuzGolu),
            E("ruh_bombasi", "Ruh Bombası",  "2 tur sonra kendiliğinden patlar: geniş alana 4 hasar.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Explode, 4, 2, TileCatalog.IsikCukuru),
            E("cig_tasi",    "Çığ Taşı",     "Kırılınca komşu karolara moloz yayar (siper oluşturur).",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Impassable, 0, 1, TileCatalog.DusmusDev),

            // ── SINIFSAL (nadir — yalnız o sınıf sahadaysa) ──────────────────
            E("nisan_kayasi","Nişan Kayası", "OKÇU bu karodan +2 menzille ve İKİ OK atar.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Range, +2, 0, TileCatalog.SutunKaya,
              1, "Okcu", true),
            E("kalkan_duvari","Kalkan Duvarı","SAVAŞÇI burada komşu yandaşlara da +2 savunma verir.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Defense, +2, 1, TileCatalog.MermerDamari,
              1, "Savasci"),
            E("ley_damari",  "Ley Damarı",   "BÜYÜCÜ burada büyü maliyetini yarıya indirir.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.ExtraAction, +1, 0, TileCatalog.TasDaire,
              1, "Buyucu"),
            E("kutsal_zemin","Kutsal Zemin", "RAHİP'in iyileştirmesi bu geniş alanda iki katına çıkar.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Regen, +2, 2, TileCatalog.Lavanta,
              3, "Rahip"),
            E("golge_yarigi","Gölge Yarığı", "SERSERİ burada görünmez olur; ilk vuruşu kritiktir.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Damage, +3, 1, TileCatalog.KaranlikOrman,
              1, "Serseri"),
            E("davul_tasi",  "Davul Taşı",   "KAM burada +3 mana kazanır; davul bir tur ERKEN çalar.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.ExtraAction, +1, 1, TileCatalog.Harabe,
              1, "Kam"),
        };

        /// <summary>Yarıçapın kaç hex kapladığı (kart açıklamasında gösterilir).</summary>
        public static int HexCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        /// <summary>Kart altında görünecek etki alanı satırı — kullanıcı isteği: yarıçap bilgisi
        /// kartın altında YAZSIN.</summary>
        public static string AreaLabel(Entry e)
        {
            string area = e.Radius == 0 ? "tek karo" : $"{HexCount(e.Radius)} hex (yarıçap {e.Radius})";
            return e.TileCount > 1 ? $"Etki: {area}  ·  {e.TileCount} karo yerleştirilir" : $"Etki: {area}";
        }

        public static Entry Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        public static List<Entry> InGroup(AugmentGroup g)
        {
            var list = new List<Entry>();
            foreach (var e in All) if (e.Group == g) list.Add(e);
            return list;
        }
    }
}
