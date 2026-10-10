namespace TacticalRPG.Grid
{
    /// <summary>
    /// BÖLÜM 1 — KÖK AHDİ ORMANI'nın VARSAYILAN bölge tarifi (hikaye/Dokuz Hücre…md §5).
    ///
    /// Bu sınıf yalnız BAŞLANGIÇ DEĞERİDİR: TAM KURULUM bunlardan <c>RegionSO</c> asset'lerini
    /// bir kez üretir, sonrası Inspector'dan ayarlanır (asset varsa EZİLMEZ). Seed taraması da
    /// Unity açmadan aynı planı buradan kurar. Karo listesinin insan okunur hâli:
    /// MODELLER/Karolar/KARO_LISTESI.md — ikisini birlikte güncelle.
    ///
    /// Ağırlık kuralı: her bölgede DÜZLÜK (öz yok) payı ~%45+ — düğümler (görev, zindan,
    /// karşılaşma, market) yalnız düzlüğe konur; taş ve doğa karoları da her bölgede bulunur ki
    /// öz alanları (EssenceFieldManager) haritanın her yerine dağılabilsin.
    /// </summary>
    public static class KokAhdiRegions
    {
        public const string YirtikKoru      = "yirtik_koru";
        public const string HalkaKoyu       = "halka_koyu";
        public const string FisiltiBatakligi= "fisilti_batakligi";
        public const string UyuyanlarVadisi = "uyuyanlar_vadisi";
        public const string OyukTepeler     = "oyuk_tepeler";
        public const string BayterekKalbi   = "bayterek_kalbi";
        public const string KokAhdiKorulari = "kok_ahdi_korulari";

        private static TileWeight W(string id, float w) => new TileWeight(id, w);

        public static RegionDef[] CreateDefs() => new[]
        {
            new RegionDef
            {
                Id = YirtikKoru, Name = "Yırtık Koru", Role = RegionRole.Start, Area = 0.9f,
                ElevBias = 0f, MoistBias = -0.15f,
                Ground = new[]
                {
                    W(TileCatalog.SolgunCayir, 3.0f), W(TileCatalog.Cayir, 0.8f),
                    W(TileCatalog.KurumusKoru, 1.6f), W(TileCatalog.AzAgacliOva, 0.6f),
                    W(TileCatalog.TaslikOva, 0.8f),   W(TileCatalog.ObsidyenTarla, 0.35f),
                },
                Forest    = new[] { TileCatalog.DikenliCalilik, TileCatalog.KaranlikOrman },
                Lake      = new[] { TileCatalog.BataklikGolu },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.DevKaya },
                Landmarks = new[] { TileCatalog.KemikTarlasi, TileCatalog.Harabe },
                CorruptionSource = true, SourceTile = TileCatalog.ZarYirtigi, SourcePermanent = true,
            },
            new RegionDef
            {
                Id = HalkaKoyu, Name = "Halka Köyü", Role = RegionRole.Hub, Area = 0.6f,
                ElevBias = -0.12f, MoistBias = 0f,
                Ground = new[]
                {
                    W(TileCatalog.HalkaZemini, 2.4f), W(TileCatalog.FenerYolu, 1.2f),
                    W(TileCatalog.DalEv, 1.0f),       W(TileCatalog.Cayir, 1.0f),
                    W(TileCatalog.MeyveBahcesi, 1.0f),W(TileCatalog.Orman, 0.5f),
                    W(TileCatalog.TaslikOva, 0.4f),
                },
                Forest    = new[] { TileCatalog.SikOrman },
                Lake      = new[] { TileCatalog.Gol },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.DevKaya },
                Landmarks = new[] { TileCatalog.DevAgac, TileCatalog.TasDaire },
                Unique    = new[] { TileCatalog.HalkaMeclisi },
            },
            new RegionDef
            {
                Id = FisiltiBatakligi, Name = "Fısıltı Bataklığı", Role = RegionRole.Graft, Area = 1.1f,
                ElevBias = -0.18f, MoistBias = 0.30f,
                Ground = new[]
                {
                    W(TileCatalog.Bataklik, 2.6f),    W(TileCatalog.YosunTarla, 0.8f),
                    W(TileCatalog.Sazlik, 1.2f),      W(TileCatalog.BataklikOtu, 1.0f),
                    W(TileCatalog.Mandragora, 0.4f),  W(TileCatalog.MantarOrmani, 0.4f),
                    W(TileCatalog.BatikTas, 0.7f),
                },
                Forest    = new[] { TileCatalog.KaranlikOrman },
                Lake      = new[] { TileCatalog.BataklikGolu },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.DevKaya },
                Landmarks = new[] { TileCatalog.IsikCukuru },
                CorruptionSource = true, SourceTile = TileCatalog.KaraAsi,
            },
            new RegionDef
            {
                Id = UyuyanlarVadisi, Name = "Uyuyanlar Vadisi", Role = RegionRole.Graft, Area = 1.1f,
                ElevBias = -0.05f, MoistBias = 0.12f,
                Ground = new[]
                {
                    W(TileCatalog.YosunTarla, 2.2f),  W(TileCatalog.Cayir, 1.2f),
                    W(TileCatalog.UzunOt, 0.6f),      W(TileCatalog.Orman, 1.0f),
                    W(TileCatalog.YuksekOrman, 0.5f), W(TileCatalog.KodamaYuvasi, 0.6f),
                    W(TileCatalog.MantarOrmani, 0.25f), W(TileCatalog.KayaYigini, 0.6f),
                },
                Forest    = new[] { TileCatalog.KadimOrman },
                Lake      = new[] { TileCatalog.Gol },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.DevKaya, TileCatalog.Kayalik },
                Landmarks = new[] { TileCatalog.DusmusDev },
                CorruptionSource = true, SourceTile = TileCatalog.KaraAsi,
            },
            new RegionDef
            {
                Id = OyukTepeler, Name = "Oyuk Tepeler", Role = RegionRole.Graft, Area = 1.0f,
                ElevBias = 0.16f, MoistBias = -0.05f,
                Ground = new[]
                {
                    W(TileCatalog.KokSirti, 2.2f),    W(TileCatalog.Cayir, 0.8f),
                    W(TileCatalog.HusKorusu, 1.0f),   W(TileCatalog.AgacKovugu, 0.5f),
                    W(TileCatalog.KilliYamac, 0.8f),  W(TileCatalog.KayaYigini, 0.6f),
                },
                Forest    = new[] { TileCatalog.KaranlikOrman },
                Lake      = new[] { TileCatalog.Gol },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.Ucurum },
                Landmarks = new[] { TileCatalog.Harabe, TileCatalog.TasDaire },
                Unique    = new[] { TileCatalog.RuhTapinagi },
                CorruptionSource = true, SourceTile = TileCatalog.KaraAsi,
            },
            new RegionDef
            {
                Id = BayterekKalbi, Name = "Bayterek'in Kalbi", Role = RegionRole.Final, Area = 0.75f,
                ElevBias = 0.06f, MoistBias = 0.08f,
                Ground = new[]
                {
                    W(TileCatalog.KokAgi, 2.6f),         W(TileCatalog.YosunTarla, 0.8f),
                    W(TileCatalog.KehribarDamari, 0.6f), W(TileCatalog.Orman, 0.6f),
                    W(TileCatalog.YuksekOrman, 0.5f),
                },
                Forest    = new[] { TileCatalog.DevKokler },
                Lake      = new[] { TileCatalog.Gol },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.DevKaya },
                Landmarks = new[] { TileCatalog.IsikCukuru },
                Unique    = new[] { TileCatalog.Bayterek },
                CorruptionSource = true, SourceTile = TileCatalog.KaraAsi, SourcePermanent = true,
            },
            new RegionDef
            {
                Id = KokAhdiKorulari, Name = "Kök Ahdi Koruları", Role = RegionRole.Filler, Seeds = 2, Area = 0.9f,
                Ground = new[]
                {
                    W(TileCatalog.Cayir, 2.2f),       W(TileCatalog.UzunOt, 1.2f),
                    W(TileCatalog.YosunTarla, 0.6f),  W(TileCatalog.AzAgacliOva, 1.0f),
                    W(TileCatalog.Orman, 1.2f),       W(TileCatalog.TaslikOva, 0.8f),
                    W(TileCatalog.KayaYigini, 0.4f),  W(TileCatalog.CakilYatagi, 0.3f),
                },
                Forest    = new[] { TileCatalog.SikOrman },
                Lake      = new[] { TileCatalog.Gol },
                Mountain  = new[] { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.DevKaya },
                Landmarks = new[] { TileCatalog.Dikilitas, TileCatalog.TasDaire, TileCatalog.DevAgac },
            },
        };

        /// <summary>Varsayılan plan (test/seed taraması için).</summary>
        public static RegionPlan CreatePlan(int layoutSeed) => new RegionPlan
        {
            Regions = CreateDefs(), LayoutSeed = layoutSeed
        };
    }
}
