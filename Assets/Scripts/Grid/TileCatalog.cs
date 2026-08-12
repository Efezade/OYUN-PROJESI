using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Karonun hangi üretim ailesine ait olduğu — üretici ve istatistik bunu kullanır.</summary>
    public enum TileFamily
    {
        Void,        // hücre YOK (harita dışı) — grid bu koordinatta karo üretmez
        Fringe,      // sınır dekoru (deniz/sis/adacık) — YÜRÜNEMEZ, istatistiğe GİRMEZ
        Plain,       // yürünür, öz yok
        Stone,       // yürünür, TAŞ özü
        Nature,      // yürünür, DOĞA özü
        Crossing,    // yürünür GEÇİT (köprü / sığ geçit / dağ geçidi) — bağlantının anahtarı
        Landmark,    // yürünür, yön bulma/gizem karosu (nadir, göze çarpar)
        River,       // yürünemez nehir
        Mountain,    // yürünemez dağ/kaya
        Blob,        // yürünemez sık orman / göl blobu
        Node,        // düğüm karoları (mağara/kamp/görev/mağaza/kule) — ChapterNodeManager boyar
        Augment      // KAM'IN DAVULDA KOYDUĞU karo (yalnız savaş arenasında; overworld üretimine girmez)
    }

    /// <summary>Karonun yüzey malzemesi — editör tarafı buna göre doku/materyal üretir.</summary>
    public enum Surface { Grass, Dry, Sand, Snow, Rock, Dirt, Swamp, Water, Ice, Ash, Moss, Stone }

    /// <summary>Bölüm 1'in öz türleri (GAME_DESIGN.md §3: sadece taş + doğa).</summary>
    public enum EssenceKind { None = 0, Tas = 1, Doga = 2 }

    /// <summary>
    /// TÜM karo türlerinin tek doğruluk kaynağı: id, görünen ad, aile, yürünürlük, öz, yüzey, renk.
    ///
    /// Neden ayrı dosya: bu tablo hem ÜRETİCİ (<see cref="TerrainGenerator"/>) hem EDİTÖR
    /// (karo modeli/materyali üretimi + TilePalette girişleri) tarafından okunuyor. Tek yerde
    /// durmazsa iki liste kaçınılmaz olarak ayrışır (id palette'te var üreticide yok → karo
    /// gri placeholder olarak çıkar, yürünürlüğü yanlış olur).
    ///
    /// UnityEngine'e BAĞIMLI DEĞİL (renk float üçlüsü olarak tutulur) — seed taraması Unity
    /// açmadan bu kodun AYNISINI derleyebilsin diye.
    /// </summary>
    public static class TileCatalog
    {
        // ── Void / sınır ─────────────────────────────────────────────────────
        public const string Void        = "bos";
        public const string SigSu       = "sig_su";
        public const string DerinSu     = "derin_su";
        public const string SisPerdesi  = "sis_perdesi";
        public const string UzakKayalik = "uzak_kayalik";
        public const string Girdap      = "girdap";

        // ── Ova / çayır (öz yok) ─────────────────────────────────────────────
        public const string Ova         = "ova";
        public const string Cayir       = "cayir";
        public const string UzunOt      = "uzun_ot";
        public const string Bozkir      = "bozkir";
        public const string KurakToprak = "kurak_toprak";
        public const string Tundra      = "tundra";
        public const string KarliOva    = "karli_ova";
        public const string Kumul       = "kumul";
        public const string SahilKumu   = "sahil_kumu";
        public const string Bataklik    = "bataklik";
        public const string YosunTarla  = "yosun_tarlasi";
        public const string Lavanta     = "lavanta_tarlasi";

        // ── Taş özü ──────────────────────────────────────────────────────────
        public const string TaslikOva    = "taslik_ova";
        public const string BolTaslikOva = "bol_taslik_ova";
        public const string CakilYatagi  = "cakil_yatagi";
        public const string KayaYigini   = "kaya_yigini";
        public const string KilliYamac   = "killi_yamac";
        public const string MermerDamari = "mermer_damari";
        public const string ObsidyenTarla= "obsidyen_tarla";
        public const string KirmiziKaya  = "kirmizi_kaya";

        // ── Doğa özü ─────────────────────────────────────────────────────────
        public const string AzAgacliOva  = "az_agacli_ova";
        public const string Orman        = "orman";
        public const string YuksekOrman  = "nadir_yuksek_orman";
        public const string CamOrmani    = "cam_ormani";
        public const string HusKorusu    = "hus_korusu";
        public const string BambuKoru    = "bambu_koru";
        public const string MeyveBahcesi = "meyve_bahcesi";
        public const string MantarOrmani = "mantar_ormani";
        public const string BataklikOtu  = "bataklik_otu";
        public const string Fundalik     = "fundalik";
        public const string Sazlik       = "sazlik";
        public const string KavakSirasi  = "kavak_sirasi";

        // ── Geçitler (yürünür — bağlantının anahtarı) ────────────────────────
        public const string Kopru     = "kopru";
        public const string SigGecit  = "sig_gecit";
        public const string DagGecidi = "dag_gecidi";

        // ── Dağ / kaya (yürünemez) ───────────────────────────────────────────
        public const string Dag           = "dag";
        public const string YuksekDag     = "yuksek_dag";
        public const string Kayalik       = "kayalik";
        public const string Ucurum        = "ucurum";
        public const string VolkanikKaya  = "volkanik_kaya";
        public const string DevKaya       = "dev_kaya";
        public const string SutunKaya     = "sutun_kaya";

        // ── Sık orman / göl blobu (yürünemez) ────────────────────────────────
        public const string SikOrman      = "sik_orman";
        public const string KaranlikOrman = "karanlik_orman";
        public const string KadimOrman    = "kadim_orman";
        public const string DikenliCalilik= "dikenli_calilik";
        public const string Gol           = "gol";
        public const string BuzGolu       = "buz_golu";
        public const string BataklikGolu  = "bataklik_golu";
        public const string KaynakGolu    = "kaynak_golu";

        // ── Nehir (yürünemez) ────────────────────────────────────────────────
        public const string Nehir   = "nehir";
        public const string Sellale = "sellale";

        // ── Landmark (yürünür, gizem/yön bulma) ──────────────────────────────
        public const string Dikilitas    = "dikilitas";
        public const string Harabe       = "harabe";
        public const string TasDaire     = "tas_daire";
        public const string DevAgac      = "dev_agac";
        public const string Krater       = "krater";
        public const string KemikTarlasi = "kemik_tarlasi";
        public const string TerkKule     = "terk_edilmis_kule";
        public const string Sunak        = "sunak";
        public const string KadimKapi    = "kadim_kapi";
        public const string DusmusDev    = "dusmus_dev";
        public const string IsikCukuru   = "isik_cukuru";
        public const string GemiEnkazi   = "gemi_enkazi";

        // ── DAVUL KAROLARI (Kam savaşta koyar — TEK RENK TEMASI) ────────────
        // Bunlar overworld karosu DEĞİL: yalnız savaş arenasında, yalnız Kam'ın davul draftıyla
        // yerleşir. Ayrı id'leri olmasının sebebi kullanıcı isteği (2026-08-12): "karolara özel
        // modeller ekle, özelliklerine benzesinler, tek renk temasından oluşsunlar". Eskiden
        // arazi karolarının görselini ödünç alıyorlardı (dikilitaş, bataklık…) — bu yüzden ne
        // tanınıyorlardı ne de bir takım gibi duruyorlardı.
        //
        // TEMA: kara bazalt taban + RUH TEALİ (0.30, 0.85, 0.78) parlayan oyma. Zemin renkleri
        // aynı hue'nun 5 değer basamağıdır; ayrım RENKTEN değil MODEL BİÇİMİNDEN gelir.
        public const string AugAtaTasi      = "aug_ata_tasi";
        public const string AugKalkanTasi   = "aug_kalkan_tasi";
        public const string AugRuzgarTasi   = "aug_ruzgar_tasi";
        public const string AugOcak         = "aug_ocak";
        public const string AugOfkeTasi     = "aug_ofke_tasi";
        public const string AugTuzakTasi    = "aug_tuzak_tasi";
        public const string AugCamur        = "aug_camur";
        public const string AugKorkuSisi    = "aug_korku_sisi";
        public const string AugDiken        = "aug_diken";
        public const string AugAgirlik      = "aug_agirlik";
        public const string AugSarsinti     = "aug_sarsinti";
        public const string AugRuhKapisi    = "aug_ruh_kapisi";
        public const string AugDuvar        = "aug_duvar";
        public const string AugBosluk       = "aug_bosluk";
        public const string AugAtesFicisi   = "aug_ates_ficisi";
        public const string AugBuzKabugu    = "aug_buz_kabugu";
        public const string AugRuhBombasi   = "aug_ruh_bombasi";
        public const string AugCigTasi      = "aug_cig_tasi";
        public const string AugNisanKayasi  = "aug_nisan_kayasi";
        public const string AugKalkanDuvari = "aug_kalkan_duvari";
        public const string AugLeyDamari    = "aug_ley_damari";
        public const string AugKutsalZemin  = "aug_kutsal_zemin";
        public const string AugGolgeYarigi  = "aug_golge_yarigi";
        public const string AugDavulTasi    = "aug_davul_tasi";

        /// <summary>Özü toplanmış karo buna döner (ova = öz yok). Öz TEK SEFERLİK.</summary>
        public const string Depleted = Ova;

        /// <summary>Davul karosu tükendiğinde (patladığında/kırıldığında) buna döner — arena
        /// zemini (<c>CombatArenaGenerator.FloorIds</c> ile aynı aile). Yürünür kalmalı: patlamış
        /// bir fıçının yeri duvara dönerse tahta savaşın ortasında kapanır.</summary>
        public const string Spent = Ova;

        // ─────────────────────────────────────────────────────────────────────

        public sealed class Entry
        {
            public string      Id;
            public string      Name;
            public TileFamily  Family;
            public bool        Walkable;
            public int         Essence;        // 0 = öz yok
            public EssenceKind Kind;
            public Surface     Surface;
            public float       R, G, B;        // zemin rengi (editör materyal/doku üretimi kullanır)
            /// <summary>Biyom kovasında bu karonun çekilme ağırlığı (0 = üretici kendisi yerleştirir).</summary>
            public float       Weight;
        }

        private static Entry E(string id, string name, TileFamily fam, bool walk, int ess, EssenceKind kind,
                               Surface surf, float r, float g, float b, float weight = 0f)
            => new Entry { Id = id, Name = name, Family = fam, Walkable = walk, Essence = ess, Kind = kind,
                           Surface = surf, R = r, G = g, B = b, Weight = weight };

        /// <summary>Tüm karo tanımları. SIRA sadece okunabilirlik içindir — üretim sırası bundan
        /// bağımsızdır (seçimler açık ağırlık listeleriyle yapılır).</summary>
        public static readonly Entry[] All =
        {
            // id, ad, aile, yürünür, öz, tür, yüzey, r, g, b, ağırlık
            E(Void,        "Boş (harita dışı)",  TileFamily.Void,   false, 0, EssenceKind.None, Surface.Water, 0f,0f,0f),

            // ── Sınır dekoru (istatistiğe girmez) ──
            E(SigSu,       "Sığ Su",             TileFamily.Fringe, false, 0, EssenceKind.None, Surface.Water, 0.36f,0.62f,0.78f),
            E(DerinSu,     "Derin Su",           TileFamily.Fringe, false, 0, EssenceKind.None, Surface.Water, 0.12f,0.28f,0.48f),
            E(SisPerdesi,  "Sis Perdesi",        TileFamily.Fringe, false, 0, EssenceKind.None, Surface.Water, 0.72f,0.75f,0.80f),
            E(UzakKayalik, "Uzak Kayalık",       TileFamily.Fringe, false, 0, EssenceKind.None, Surface.Rock,  0.40f,0.40f,0.44f),
            E(Girdap,      "Girdap",             TileFamily.Fringe, false, 0, EssenceKind.None, Surface.Water, 0.16f,0.34f,0.42f),

            // ── Ova ailesi (öz yok) ──
            E(Ova,         "Ova",                TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Grass, 0.51f,0.66f,0.35f, 1.00f),
            E(Cayir,       "Çiçekli Çayır",      TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Grass, 0.58f,0.72f,0.38f, 1.10f),
            E(UzunOt,      "Uzun Ot",            TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Grass, 0.46f,0.62f,0.30f, 1.10f),
            E(Bozkir,      "Bozkır",             TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Dry,   0.72f,0.68f,0.40f, 0.90f),
            E(KurakToprak, "Kurak Toprak",       TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Dirt,  0.68f,0.54f,0.36f, 0.90f),
            E(Tundra,      "Tundra",             TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Moss,  0.55f,0.60f,0.50f, 0.90f),
            E(KarliOva,    "Karlı Ova",          TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Snow,  0.86f,0.89f,0.92f, 0.80f),
            E(Kumul,       "Kumul",              TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Sand,  0.86f,0.78f,0.54f, 0.45f),
            E(SahilKumu,   "Sahil Kumu",         TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Sand,  0.88f,0.83f,0.62f, 1.00f),
            E(Bataklik,    "Bataklık",           TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Swamp, 0.38f,0.44f,0.30f, 0.70f),
            E(YosunTarla,  "Yosun Tarlası",      TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Moss,  0.40f,0.56f,0.34f, 0.95f),
            E(Lavanta,     "Lavanta Tarlası",    TileFamily.Plain,  true,  0, EssenceKind.None, Surface.Grass, 0.60f,0.55f,0.72f, 0.35f),

            // ── Taş özü ──
            E(TaslikOva,    "Taşlık Ova (1 taş)",   TileFamily.Stone, true, 1, EssenceKind.Tas, Surface.Rock,  0.63f,0.63f,0.56f, 1.05f),
            E(BolTaslikOva, "Bol Taşlık (2 taş)",   TileFamily.Stone, true, 2, EssenceKind.Tas, Surface.Rock,  0.55f,0.54f,0.50f, 0.52f),
            E(CakilYatagi,  "Çakıl Yatağı (1 taş)", TileFamily.Stone, true, 1, EssenceKind.Tas, Surface.Rock,  0.68f,0.66f,0.62f, 0.75f),
            E(KayaYigini,   "Kaya Yığını (2 taş)",  TileFamily.Stone, true, 2, EssenceKind.Tas, Surface.Rock,  0.50f,0.49f,0.47f, 0.48f),
            E(KilliYamac,   "Killi Yamaç (1 taş)",  TileFamily.Stone, true, 1, EssenceKind.Tas, Surface.Dirt,  0.62f,0.47f,0.34f, 0.57f),
            E(MermerDamari, "Mermer Damarı (3 taş)",TileFamily.Stone, true, 3, EssenceKind.Tas, Surface.Stone, 0.84f,0.84f,0.86f, 0.15f),
            E(ObsidyenTarla,"Obsidyen Tarla (3 taş)",TileFamily.Stone,true, 3, EssenceKind.Tas, Surface.Ash,   0.20f,0.19f,0.23f, 0.13f),
            E(KirmiziKaya,  "Kırmızı Kaya (2 taş)", TileFamily.Stone, true, 2, EssenceKind.Tas, Surface.Rock,  0.72f,0.42f,0.30f, 0.33f),

            // ── Doğa özü ──
            E(AzAgacliOva,  "Az Ağaçlı Ova (1 doğa)",TileFamily.Nature, true, 1, EssenceKind.Doga, Surface.Grass, 0.45f,0.62f,0.33f, 1.05f),
            E(Orman,        "Orman (2 doğa)",        TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Grass, 0.30f,0.50f,0.26f, 0.85f),
            E(YuksekOrman,  "Yüksek Orman (3 doğa)", TileFamily.Nature, true, 3, EssenceKind.Doga, Surface.Grass, 0.20f,0.40f,0.22f, 0.21f),
            E(CamOrmani,    "Çam Ormanı (2 doğa)",   TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Moss,  0.22f,0.42f,0.30f, 0.68f),
            E(HusKorusu,    "Huş Korusu (2 doğa)",   TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Grass, 0.56f,0.66f,0.42f, 0.52f),
            E(BambuKoru,    "Bambu Koru (2 doğa)",   TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Grass, 0.52f,0.68f,0.32f, 0.30f),
            E(MeyveBahcesi, "Meyve Bahçesi (2 doğa)",TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Grass, 0.48f,0.62f,0.34f, 0.30f),
            E(MantarOrmani, "Mantar Ormanı (3 doğa)",TileFamily.Nature, true, 3, EssenceKind.Doga, Surface.Moss,  0.44f,0.36f,0.46f, 0.16f),
            E(BataklikOtu,  "Bataklık Otu (1 doğa)", TileFamily.Nature, true, 1, EssenceKind.Doga, Surface.Swamp, 0.42f,0.50f,0.32f, 0.75f),
            E(Fundalik,     "Fundalık (1 doğa)",     TileFamily.Nature, true, 1, EssenceKind.Doga, Surface.Dry,   0.60f,0.56f,0.36f, 0.82f),
            E(Sazlik,       "Sazlık (1 doğa)",       TileFamily.Nature, true, 1, EssenceKind.Doga, Surface.Swamp, 0.50f,0.58f,0.36f, 0.68f),
            E(KavakSirasi,  "Kavak Sırası (2 doğa)", TileFamily.Nature, true, 2, EssenceKind.Doga, Surface.Grass, 0.52f,0.64f,0.36f, 0.36f),

            // ── Geçitler ──
            E(Kopru,     "Köprü (nehir geçidi)",  TileFamily.Crossing, true, 0, EssenceKind.None, Surface.Water, 0.55f,0.45f,0.35f),
            E(SigGecit,  "Sığ Geçit",             TileFamily.Crossing, true, 0, EssenceKind.None, Surface.Water, 0.62f,0.72f,0.74f),
            E(DagGecidi, "Dağ Geçidi",            TileFamily.Crossing, true, 0, EssenceKind.None, Surface.Rock,  0.58f,0.56f,0.52f),

            // ── Dağ / kaya (yürünemez) ──
            E(Dag,          "Dağ",              TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Rock,  0.46f,0.44f,0.42f, 1.00f),
            E(YuksekDag,    "Yüksek Dağ (karlı)",TileFamily.Mountain,false, 0, EssenceKind.None, Surface.Snow,  0.78f,0.82f,0.88f, 0.45f),
            E(Kayalik,      "Kayalık",          TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Rock,  0.54f,0.52f,0.48f, 0.70f),
            E(Ucurum,       "Uçurum",           TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Stone, 0.42f,0.40f,0.39f, 0.40f),
            E(VolkanikKaya, "Volkanik Kaya",    TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Ash,   0.26f,0.23f,0.24f, 0.30f),
            E(DevKaya,      "Dev Kaya",         TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Rock,  0.50f,0.48f,0.46f, 0.25f),
            E(SutunKaya,    "Sütun Kaya",       TileFamily.Mountain, false, 0, EssenceKind.None, Surface.Sand,  0.74f,0.62f,0.44f, 0.20f),

            // ── Sık orman / göl blobu (yürünemez) ──
            E(SikOrman,      "Sık Orman",       TileFamily.Blob, false, 0, EssenceKind.None, Surface.Grass, 0.14f,0.30f,0.16f, 1.00f),
            E(KaranlikOrman, "Karanlık Orman",  TileFamily.Blob, false, 0, EssenceKind.None, Surface.Moss,  0.11f,0.22f,0.16f, 0.60f),
            E(KadimOrman,    "Kadim Orman",     TileFamily.Blob, false, 0, EssenceKind.None, Surface.Moss,  0.16f,0.26f,0.14f, 0.35f),
            E(DikenliCalilik,"Dikenli Çalılık", TileFamily.Blob, false, 0, EssenceKind.None, Surface.Dry,   0.40f,0.38f,0.22f, 0.45f),
            E(Gol,           "Göl",             TileFamily.Blob, false, 0, EssenceKind.None, Surface.Water, 0.22f,0.46f,0.68f, 1.00f),
            E(BuzGolu,       "Buz Gölü",        TileFamily.Blob, false, 0, EssenceKind.None, Surface.Ice,   0.72f,0.85f,0.90f, 0.35f),
            E(BataklikGolu,  "Bataklık Gölü",   TileFamily.Blob, false, 0, EssenceKind.None, Surface.Swamp, 0.30f,0.38f,0.30f, 0.45f),
            E(KaynakGolu,    "Sıcak Kaynak",    TileFamily.Blob, false, 0, EssenceKind.None, Surface.Water, 0.46f,0.72f,0.70f, 0.22f),

            // ── Nehir ──
            E(Nehir,   "Nehir",   TileFamily.River, false, 0, EssenceKind.None, Surface.Water, 0.28f,0.56f,0.76f),
            E(Sellale, "Şelale",  TileFamily.River, false, 0, EssenceKind.None, Surface.Water, 0.62f,0.80f,0.90f),

            // ── Landmark (yürünür — yön bulma + gizem) ──
            E(Dikilitas,    "Dikilitaş",         TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Stone, 0.52f,0.52f,0.56f, 1.0f),
            E(Harabe,       "Harabe",            TileFamily.Landmark, true, 1, EssenceKind.Tas,  Surface.Stone, 0.60f,0.58f,0.52f, 1.0f),
            E(TasDaire,     "Taş Daire",         TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Grass, 0.50f,0.58f,0.42f, 1.0f),
            E(DevAgac,      "Dev Ağaç",          TileFamily.Landmark, true, 3, EssenceKind.Doga, Surface.Grass, 0.28f,0.46f,0.24f, 1.0f),
            E(Krater,       "Krater",            TileFamily.Landmark, true, 2, EssenceKind.Tas,  Surface.Ash,   0.32f,0.28f,0.26f, 1.0f),
            E(KemikTarlasi, "Kemik Tarlası",     TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Dry,   0.74f,0.72f,0.62f, 1.0f),
            E(TerkKule,     "Terk Edilmiş Kule", TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Stone, 0.56f,0.54f,0.50f, 1.0f),
            E(Sunak,        "Sunak",             TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Stone, 0.48f,0.46f,0.54f, 1.0f),
            E(KadimKapi,    "Kadim Kapı",        TileFamily.Landmark, true, 0, EssenceKind.None, Surface.Stone, 0.46f,0.44f,0.48f, 1.0f),
            E(DusmusDev,    "Düşmüş Dev",        TileFamily.Landmark, true, 2, EssenceKind.Tas,  Surface.Moss,  0.44f,0.50f,0.40f, 1.0f),
            E(IsikCukuru,   "Işık Çukuru",       TileFamily.Landmark, true, 3, EssenceKind.Doga, Surface.Moss,  0.36f,0.52f,0.48f, 1.0f),
            E(GemiEnkazi,   "Gemi Enkazı",       TileFamily.Landmark, true, 1, EssenceKind.Tas,  Surface.Sand,  0.70f,0.64f,0.50f, 1.0f),

            // ── DAVUL KAROLARI (yalnız savaş; ağırlık 0 → arazi üreticisi asla seçmez) ──
            // Renkler bilerek BİRBİRİNE ÇOK YAKIN: hepsi tek bir kara-bazalt temasının değer
            // basamakları. Oyuncu karoyu renginden değil BİÇİMİNDEN tanır; renk yalnız "bu bir
            // ruh karosu" der. Yürünürlük burada tanımlanır → palet ve grid otomatik uyar.
            E(AugAtaTasi,      "Ata Taşı",       TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.21f,0.27f,0.29f),
            E(AugKalkanTasi,   "Kalkan Taşı",    TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.21f,0.27f,0.29f),
            E(AugRuzgarTasi,   "Rüzgâr Taşı",    TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.21f,0.27f,0.29f),
            E(AugOcak,         "Ocak",           TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.21f,0.27f,0.29f),
            E(AugOfkeTasi,     "Öfke Taşı",      TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.21f,0.27f,0.29f),

            E(AugTuzakTasi,    "Tuzak Taşı",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.13f,0.16f,0.18f),
            E(AugCamur,        "Çamur",          TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.13f,0.16f,0.18f),
            E(AugKorkuSisi,    "Korku Sisi",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.13f,0.16f,0.18f),
            E(AugDiken,        "Diken Tarlası",  TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.13f,0.16f,0.18f),
            E(AugAgirlik,      "Ağırlık Taşı",   TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.13f,0.16f,0.18f),

            E(AugSarsinti,     "Sarsıntı Hattı", TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.17f,0.21f,0.23f),
            E(AugRuhKapisi,    "Ruh Kapısı",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.17f,0.21f,0.23f),
            E(AugDuvar,        "Taş Duvar",      TileFamily.Augment, false, 0, EssenceKind.None, Surface.Stone, 0.17f,0.21f,0.23f),
            E(AugBosluk,       "Boşluk",         TileFamily.Augment, false, 0, EssenceKind.None, Surface.Stone, 0.06f,0.07f,0.09f),

            E(AugAtesFicisi,   "Ateş Fıçısı",    TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.16f,0.20f,0.22f),
            E(AugBuzKabugu,    "Buz Kabuğu",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.16f,0.20f,0.22f),
            E(AugRuhBombasi,   "Ruh Bombası",    TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.16f,0.20f,0.22f),
            E(AugCigTasi,      "Çığ Taşı",       TileFamily.Augment, false, 0, EssenceKind.None, Surface.Stone, 0.16f,0.20f,0.22f),

            E(AugNisanKayasi,  "Nişan Kayası",   TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
            E(AugKalkanDuvari, "Kalkan Duvarı",  TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
            E(AugLeyDamari,    "Ley Damarı",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
            E(AugKutsalZemin,  "Kutsal Zemin",   TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
            E(AugGolgeYarigi,  "Gölge Yarığı",   TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
            E(AugDavulTasi,    "Davul Taşı",     TileFamily.Augment, true,  0, EssenceKind.None, Surface.Stone, 0.20f,0.25f,0.28f),
        };

        /// <summary>Davul karosu mu? (Savaş arenasında Kam'ın koyduğu karo.)</summary>
        public static bool IsAugment(string id)
        {
            Entry e = Get(id);
            return e != null && e.Family == TileFamily.Augment;
        }

        private static readonly Dictionary<string, Entry> ById = BuildIndex();

        private static Dictionary<string, Entry> BuildIndex()
        {
            var d = new Dictionary<string, Entry>(All.Length);
            foreach (var e in All) d[e.Id] = e;
            return d;
        }

        public static Entry Get(string id)
            => id != null && ById.TryGetValue(id, out Entry e) ? e : null;

        /// <summary>Verilen ailedeki karolar (ağırlıklı seçim için).</summary>
        public static List<Entry> Family(TileFamily f)
        {
            var list = new List<Entry>();
            foreach (var e in All) if (e.Family == f) list.Add(e);
            return list;
        }

        // ── Üreticinin/oyunun sorduğu sorular ────────────────────────────────

        /// <summary>Bu karonun üstünde yürünebilir mi? (Düğüm karoları palette'ten okunur;
        /// burada bilinmeyen id = yürünemez sayılır — sessiz "her yere gidilebilir" hatası olmasın.)</summary>
        public static bool IsWalkable(string id)
        {
            Entry e = Get(id);
            return e != null && e.Walkable;
        }

        /// <summary>Yürünemez mi? (void ve sınır dekoru DAHİL — bunlar tahtada engel gibi davranır.)</summary>
        public static bool IsImpassable(string id) => !IsWalkable(id);

        /// <summary>Bu koordinatta karo var mı? (void = karo yok, grid hücre üretmez.)</summary>
        public static bool IsVoid(string id) => id == null || id == Void;

        /// <summary>Karonun taşıdığı öz (miktar + tür). Yoksa (0, None).</summary>
        public static void EssenceOf(string id, out int amount, out EssenceKind kind)
        {
            Entry e = Get(id);
            if (e == null) { amount = 0; kind = EssenceKind.None; return; }
            amount = e.Essence;
            kind   = e.Kind;
        }

        /// <summary>İstatistiğe giren karo mu? (void ve sınır dekoru harita yüzdesine SAYILMAZ —
        /// kullanıcının verdiği %78.4 / %21.6 oranları OYNANAN kıtaya aittir.)</summary>
        public static bool CountsInStats(string id)
        {
            Entry e = Get(id);
            if (e == null) return true;                       // düğüm karoları (palette) sayılır
            return e.Family != TileFamily.Void && e.Family != TileFamily.Fringe
                && e.Family != TileFamily.Augment;            // davul karosu overworld'e hiç girmez
        }
    }
}
