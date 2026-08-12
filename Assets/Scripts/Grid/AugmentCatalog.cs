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

    /// <summary>Karonun ne yaptığı. Çözümlemesi <c>AugmentTileManager</c>'da.</summary>
    public enum AugmentEffect
    {
        None,
        Initiative,     // sıra sıralamasında öne/geriye (+/-)
        Damage,         // saldırı gücü (+/-)
        Defense,        // savunma (+/-)
        Move,           // hareket menzili (+/-)
        Regen,          // tur başı can (+/-)
        Mana,           // tur başı mana (yalnız komutan/Kam)
        Accuracy,       // isabet yüzdesi (+/-)  [isabet sistemi gelince etkinleşir]
        Range,          // saldırı menzili (+)   [menzil sistemi gelince etkinleşir]
        ExtraAction,    // o tur fazladan aksiyon
        Stun,           // üstüne gelen birim 1 tur aksiyon yapamaz
        EntryDamage,    // karoya girişte hasar
        Explode,        // tetiklenince alan hasarı, sonra karo tükenir
        Impassable,     // geçilemez (duvar/boşluk)
        BlockSight      // görüş hattını keser (aynı zamanda geçilemez)
    }

    /// <summary>
    /// Etkinin NE ZAMAN çözümlendiği. Bu alan olmadan <c>AugmentTileManager</c> "Stun" gördüğünde
    /// bunun sürekli bir aura mı yoksa girişte bir kez mi tetiklendiğini bilemezdi — eskiden
    /// karolar hiç çalışmıyordu çünkü etkinin zamanı hiçbir yerde yazılı değildi.
    /// </summary>
    public enum AugmentTrigger
    {
        Aura,       // yarıçap içinde DURDUĞU sürece stat değişir (giren kazanır, çıkan kaybeder)
        TurnStart,  // yarıçap içinde TUR BAŞLATANA uygulanır (can/mana/aksiyon)
        OnEnter,    // alana GİREN birime bir kez uygulanır (tuzak, diken, buz)
        OnDamaged,  // üstündeki birim hasar alınca patlar (fıçı)
        Fuse,       // N tur sonra kendiliğinden patlar (ruh bombası)
        Terrain     // etki karonun KENDİSİ: geçilemez/görüş keser (duvar, boşluk, moloz)
    }

    /// <summary>Kimi etkiler.</summary>
    public enum AugmentTarget { Allies, Enemies, Everyone }

    /// <summary>
    /// Davul vuruşunda Kam'a sunulan KARO KARTLARI havuzu.
    ///
    /// Havuz boyutu 24: savaş başına 2-5 vuruş × 3 kart = 6-15 kart görülür, bölüm başına ~200.
    /// 24 tip hem tekrar yorgunluğu yaratmaz hem öğrenilebilir kalır (TFT/Balatro aralığı 20-40).
    ///
    /// ═══ YARIÇAP KURALI (2026-08-13'te YENİLENDİ — kullanıcı kuralı: "tek karoluk olmasın") ═══
    ///
    /// TEK KARO ARTIK YOK. Sebebi mekanik: bir karo tek hexi etkiliyorsa oyuncunun onu işe
    /// yaratması için düşmanın TAM O KAROYA basmasını beklemesi gerekiyordu — hex tahtada bu
    /// neredeyse hiç olmuyor. Karo ya boşa gidiyor ya da oyun "düşman oraya bassın" diye
    /// bekleme oyununa dönüyordu. Kart konduğu anda BİR ŞEY yapmalı.
    ///
    /// İKİ KADEME (ideal taban = 1):
    ///   • **YARIÇAP 1 — 7 hex, 3 karo çapı → TABAN ÖLÇÜ.** Kartların çoğu burada.
    ///     Neden ideal: 80 karoluk arenada %8.5 yer kaplar; birim yoğunluğu (Kam+4 vs 4-7 düşman
    ///     ≈ 10 birim / 80 karo) ile çarpıldığında alanda ORTALAMA 1 birim düşer — yani karo
    ///     "kesin bir şey yapar" ama "her şeyi kapsamaz". Bir karo eni (3) düşmanın bir turluk
    ///     hareketinden (3-4) küçük olduğu için kaçınılabilir de kalır: hâlâ konum kararı.
    ///   • **YARIÇAP 2 — 19 hex, 5 karo çapı → GENİŞ ama YAVAŞ/ZAYIF.** Yalnız gecikmeli ya da
    ///     düşük etkili kartlar (Ruh Bombası fitilli, Kutsal Zemin tur başı iyileştirme).
    ///     %23 yer kaplar; anlık ve güçlü bir etki bu alanda savaşı tek kartla bitirirdi.
    /// Arazi kartları (Taş Duvar / Boşluk / Çığ) yarıçapla değil KAPLADIKLARI KAROYLA ölçülür —
    /// onlar da tek karo değil: üçü de 3 karo örer.
    ///
    /// AÇIKLAMA = SÖZLEŞME (kullanıcı kuralı 2026-08-12): "dedikleri şey gerçekten gerçekleşsin".
    /// Karttaki metin ne diyorsa <c>AugmentTileManager</c> tam olarak onu yapar. Metin ile kod
    /// ayrışırsa METİN düzeltilir — süslü ama çalışmayan bir vaat yazılmaz.
    ///
    /// UnityEngine'e bağımlı değil — denge taraması Unity açmadan koşabilsin diye.
    /// </summary>
    public static class AugmentCatalog
    {
        public sealed class Entry
        {
            public string         Id;
            public string         Name;
            public string         Description;   // karta yazılan açıklama (= davranışın sözleşmesi)
            public AugmentGroup   Group;
            public AugmentTarget  Target;
            public AugmentEffect  Effect;
            public AugmentTrigger Trigger;
            public int            Magnitude;     // etkinin büyüklüğü (işaretli)
            public int            Radius;        // 0 = tek hex, 1 = 7 hex, 2 = 19 hex
            public int            TileCount = 1; // kaç karo yerleştirilir (Duvar 3, Kutsal Zemin 3)
            public string         VisualId;      // TileCatalog id'si (davul karosu görseli)
            /// <summary>Tetiklenince karo tükenir (zemine döner).</summary>
            public bool           OneShot;
            /// <summary>Fuse tetikleyicisinde kaç TUR sonra patlar.</summary>
            public int            FuseRounds;
            /// <summary>Sadece bu sınıf sahadayken çıkar (Sinifsal kartlar). null = herkese açık.</summary>
            public string         RequiresClass;
            /// <summary>İsabet/menzil sistemi gelmeden ETKİSİ ÇALIŞMAZ — draft'tan elenir.</summary>
            public bool           NeedsRangedSystem;

            /// <summary>Bu karo tahtayı kapatıyor mu (yürünmez + görüş keser)?</summary>
            public bool IsTerrain => Trigger == AugmentTrigger.Terrain;
        }

        private static Entry E(string id, string name, string desc, AugmentGroup g, AugmentTarget t,
                               AugmentEffect e, AugmentTrigger trig, int mag, int radius, string visual,
                               int tiles = 1, bool oneShot = false, int fuse = 0,
                               string cls = null, bool needsRanged = false)
            => new Entry { Id = id, Name = name, Description = desc, Group = g, Target = t, Effect = e,
                           Trigger = trig, Magnitude = mag, Radius = radius, VisualId = visual,
                           TileCount = tiles, OneShot = oneShot, FuseRounds = fuse,
                           RequiresClass = cls, NeedsRangedSystem = needsRanged };

        public static readonly Entry[] All =
        {
            // ── KUT (yandaşa +) ──────────────────────────────────────────────
            E("ata_tasi",    "Ata Taşı",     "Bu alandaki yandaşlar sırada ÖNE GEÇER (+3 inisiyatif).",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Initiative, AugmentTrigger.Aura,
              +3, 1, TileCatalog.AugAtaTasi),
            E("kalkan_tasi", "Kalkan Taşı",  "Bu alandaki yandaşlar +2 savunma kazanır.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Defense, AugmentTrigger.Aura,
              +2, 1, TileCatalog.AugKalkanTasi),
            E("ruzgar_tasi", "Rüzgâr Taşı",  "Bu alandaki yandaşlar +1 hareket kazanır.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Move, AugmentTrigger.Aura,
              +1, 1, TileCatalog.AugRuzgarTasi),
            E("ocak",        "Ocak",         "Bu alanda TUR BAŞLATAN yandaş 2 can yeniler.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Regen, AugmentTrigger.TurnStart,
              +2, 1, TileCatalog.AugOcak),
            E("ofke_tasi",   "Öfke Taşı",    "Bu alandaki yandaşlar +2 hasar verir.",
              AugmentGroup.Kut, AugmentTarget.Allies, AugmentEffect.Damage, AugmentTrigger.Aura,
              +2, 1, TileCatalog.AugOfkeTasi),

            // ── KARGIŞ (düşmana −) ───────────────────────────────────────────
            E("tuzak_tasi",  "Tuzak Taşı",   "Bu alana GİREN düşman SERSEMLER — sıradaki turunu kaybeder.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Stun, AugmentTrigger.OnEnter,
              1, 1, TileCatalog.AugTuzakTasi),
            E("camur",       "Çamur",        "Bu alandaki düşmanın hareketi 2 azalır.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Move, AugmentTrigger.Aura,
              -2, 1, TileCatalog.AugCamur),
            E("korku_sisi",  "Korku Sisi",   "Bu alandaki düşman %30 daha az isabet eder.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Accuracy, AugmentTrigger.Aura,
              -30, 1, TileCatalog.AugKorkuSisi, 1, false, 0, null, true),
            E("diken",       "Diken Tarlası","Bu alana GİREN düşman 3 hasar alır.",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.EntryDamage, AugmentTrigger.OnEnter,
              3, 1, TileCatalog.AugDiken),
            E("agirlik",     "Ağırlık Taşı", "Bu alandaki düşman sırada GERİYE DÜŞER (−3 inisiyatif).",
              AugmentGroup.Kargis, AugmentTarget.Enemies, AugmentEffect.Initiative, AugmentTrigger.Aura,
              -3, 1, TileCatalog.AugAgirlik),

            // ── NÖTR (herkese / arazi) ───────────────────────────────────────
            E("sarsinti",    "Sarsıntı Hattı","Bu alandaki HERKES −2 savunma. İki ağızlı: seni de keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.Defense, AugmentTrigger.Aura,
              -2, 1, TileCatalog.AugSarsinti),
            E("ruh_kapisi",  "Ruh Kapısı",   "Bu alanda TUR BAŞLATAN herkes +1 aksiyon kazanır.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.ExtraAction, AugmentTrigger.TurnStart,
              +1, 1, TileCatalog.AugRuhKapisi),
            E("duvar",       "Taş Duvar",    "3 karoluk geçilemez duvar örer; görüş hattını da keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.Impassable, AugmentTrigger.Terrain,
              0, 0, TileCatalog.AugDuvar, 3),
            E("bosluk",      "Boşluk",       "3 karoyu uçuruma çevirir: geçilemez, görüş hattını keser.",
              AugmentGroup.Notr, AugmentTarget.Everyone, AugmentEffect.BlockSight, AugmentTrigger.Terrain,
              0, 0, TileCatalog.AugBosluk, 3),

            // ── PATLAYICI (bir kez tetiklenir) ───────────────────────────────
            E("ates_ficisi", "Ateş Fıçısı",  "Üstündeki birim vurulunca patlar: çevresine 5 hasar.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Explode, AugmentTrigger.OnDamaged,
              5, 1, TileCatalog.AugAtesFicisi, 1, true),
            E("buz_kabugu",  "Buz Kabuğu",   "Bu alana İLK giren birim DONAR (bir tur), sonra karo kırılır.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Stun, AugmentTrigger.OnEnter,
              1, 1, TileCatalog.AugBuzKabugu, 1, true),
            E("ruh_bombasi", "Ruh Bombası",  "2 tur sonra kendiliğinden patlar: geniş alana 4 hasar.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Explode, AugmentTrigger.Fuse,
              4, 2, TileCatalog.AugRuhBombasi, 1, true, 2),
            E("cig_tasi",    "Çığ Taşı",     "Yerleştiği karoyu ve 2 komşusunu molozla kapatır: geçilemez siper.",
              AugmentGroup.Patlayici, AugmentTarget.Everyone, AugmentEffect.Impassable, AugmentTrigger.Terrain,
              0, 0, TileCatalog.AugCigTasi, 3),

            // ── SINIFSAL (nadir — yalnız o sınıf sahadaysa) ──────────────────
            // Menzil bonusu artık gerçekten çalışıyor (Unit.AttackRange karo bonusunu topluyor)
            // → draft'tan elenme sebebi kalktı. Korku Sisi hâlâ elenir: isabet YÜZDESİ için
            // vuruş zarı gerekiyor, saldırılar şu an hep isabet ediyor.
            E("nisan_kayasi","Nişan Kayası", "OKÇU kartı: bu alandaki yandaşlar +2 menzille atar.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Range, AugmentTrigger.Aura,
              +2, 1, TileCatalog.AugNisanKayasi, 1, false, 0, "Okcu"),
            E("kalkan_duvari","Kalkan Duvarı","SAVAŞÇI kartı: bu alandaki yandaşlar +2 savunma kazanır.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Defense, AugmentTrigger.Aura,
              +2, 1, TileCatalog.AugKalkanDuvari, 1, false, 0, "Savasci"),
            E("ley_damari",  "Ley Damarı",   "BÜYÜCÜ kartı: bu alanda tur başlatan yandaş +1 aksiyon kazanır.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.ExtraAction, AugmentTrigger.TurnStart,
              +1, 1, TileCatalog.AugLeyDamari, 1, false, 0, "Buyucu"),
            E("kutsal_zemin","Kutsal Zemin", "RAHİP kartı: bu geniş alanda tur başlatan yandaş 2 can yeniler.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Regen, AugmentTrigger.TurnStart,
              +2, 2, TileCatalog.AugKutsalZemin, 3, false, 0, "Rahip"),
            E("golge_yarigi","Gölge Yarığı", "SERSERİ kartı: bu alandaki yandaşlar +3 hasar verir.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Damage, AugmentTrigger.Aura,
              +3, 1, TileCatalog.AugGolgeYarigi, 1, false, 0, "Serseri"),
            E("davul_tasi",  "Davul Taşı",   "KAM kartı: bu alanda tur başlatan Kam 3 mana kazanır.",
              AugmentGroup.Sinifsal, AugmentTarget.Allies, AugmentEffect.Mana, AugmentTrigger.TurnStart,
              +3, 1, TileCatalog.AugDavulTasi, 1, false, 0, "Kam"),
        };

        /// <summary>Yarıçapın kaç hex kapladığı (kart açıklamasında gösterilir).</summary>
        public static int HexCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        /// <summary>Kart altında görünecek etki alanı satırı — kullanıcı isteği: yarıçap bilgisi
        /// kartın altında YAZSIN.</summary>
        public static string AreaLabel(Entry e)
        {
            // Çap (karo) kullanılıyor, yarıçap değil: oyuncu tahtaya bakarken "kaç karo enine"
            // diye düşünüyor. Büyü kartlarıyla da aynı dil (KamSkillCatalog.AreaLabel).
            if (e.IsTerrain) return $"Etki: {e.TileCount} karo örer";

            string area = $"{HexCount(e.Radius)} hex ({e.Radius * 2 + 1} karo çapı)";
            return e.TileCount > 1 ? $"Etki: {area}  ·  {e.TileCount} karo yerleştirilir" : $"Etki: {area}";
        }

        /// <summary>
        /// KURAL DENETİMİ — "tek karoluk etki olmasın" (kullanıcı kuralı 2026-08-13) makine
        /// tarafından korunsun. Yeni bir kart yarıçapsız eklenirse kurulum bunu bağırarak söyler;
        /// yalnız yorum satırında duran bir kural er ya da geç sessizce delinir.
        /// Boş liste = her şey yolunda.
        /// </summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var e in All)
            {
                if (e.IsTerrain)
                {
                    if (e.TileCount < 2)
                        problems.Add($"{e.Id}: arazi kartı {e.TileCount} karo örüyor — en az 2 olmalı");
                    continue;
                }
                if (e.Radius < 1) problems.Add($"{e.Id}: yarıçap {e.Radius} — TEK KARO yasak (en az 1)");
                if (e.Radius > 2) problems.Add($"{e.Id}: yarıçap {e.Radius} — en fazla 2 olmalı (geniş kademe)");
            }
            return problems;
        }

        public static Entry Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>Tahtadaki karo id'sinden kartı bulur. <c>AugmentTileManager</c> savaş
        /// yeniden kurulduğunda tahtayı tarayıp durumu bundan geri kurar.</summary>
        public static Entry ByVisual(string visualId)
        {
            if (visualId == null) return null;
            foreach (var e in All) if (e.VisualId == visualId) return e;
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
