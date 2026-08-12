using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// <see cref="TileCatalog"/>'daki HER karo tipi için RENKLİ + DOKULU bir 3B karo üretir ve
    /// TilePalette'e bağlar. Tek tıkla: doku (PNG) → materyal → prefab → palet girişi.
    ///
    /// NEDEN koddan üretiliyor (indirilen hazır model değil):
    ///   • Lisans/boyut derdi yok, repo şişmiyor, her makinede aynı sonuç çıkıyor.
    ///   • 70+ karo tipini elle modellemek/indirmek günler sürerdi; buradaki tablo ile dakikalar.
    ///   • Bunlar WHITEBOX: kullanıcı sonradan `Assets/Art/Models/Tiles/` klasörüne gerçek FBX
    ///     atıp Tile Painter'ın "Klasörü Tara" hattından palete bağlayınca hiçbir şey bozulmaz —
    ///     bu araç palet girişinin sadece prefab alanını doldurur.
    ///
    /// GÖRSEL YAKLAŞIM: doku katmanı GRİ TONLUdur (parlaklık dalgalanması), renk materyalin
    /// `_BaseColor`'ından gelir. Böylece 12 doku 70+ karoya hizmet eder ve her karo kendi renginde
    /// görünür — "her şey beyaz" sorununun çözümü bu (kullanıcı geri bildirimi 2026-08-12).
    /// </summary>
    /// <remarks>
    /// <c>partial</c>: davul karolarının modelleri <c>TileVisualFactory.Augments.cs</c> dosyasında
    /// üretilir. Ayrı bir sınıf olsaydı buradaki geometri sözlüğünü (Prim/Cone/NewBase/WritePalette)
    /// ya kopyalamak ya da yarısını public'e açmak gerekirdi; ikisi de bu dosyanın "karo üretimi
    /// tek elden" amacını bozardı.
    /// </remarks>
    public static partial class TileVisualFactory
    {
        private const string TexFolder    = "Assets/Art/Textures/Tiles";
        private const string MatFolder    = "Assets/Art/Materials/Tiles";
        private const string PrefabFolder = "Assets/Prefabs/Grid/Terrain";
        private const string BaseTilePath = "Assets/Prefabs/Grid/Tile_default.prefab";
        private const string ConeMeshPath = "Assets/Art/Meshes/ConeMesh.asset";

        /// <summary>Palette'te bu id varsa kurulum daha önce koşmuş demektir (otomatik tetikleyici).</summary>
        public const string SentinelId = TileCatalog.SisPerdesi;

        // ── Süsleme türleri ──────────────────────────────────────────────────
        // Her biri küçük bir geometri reçetesi. Karo tablosu bunlardan 1-2 tanesini seçer;
        // 70 karo için 70 ayrı metod yazmak yerine 30 reçete × parametre.
        private enum Deco
        {
            None, Rocks, Boulders, Pebbles, Crystals, Slabs,
            Conifer, Broadleaf, Birch, Bamboo, DeadTree, GiantTree,
            Bush, Grass, Reeds, Flowers, Mushroom, Cactus, Vines,
            Peak, SnowPeak, Cliff, Pillar, Arch,
            Water, DeepWater, Ripples, Ice, Mist, Whirl,
            Bones, Obelisk, Ruin, StoneCircle, Crater, Tower, Altar, Gate, Statue, GlowPit, Wreck,
            Deck, Stepping, PassPost
        }

        private readonly struct Recipe
        {
            public readonly string Id;
            public readonly Deco   A, B;
            public readonly int    CountA, CountB;
            public Recipe(string id, Deco a, int ca, Deco b = Deco.None, int cb = 0)
            { Id = id; A = a; CountA = ca; B = b; CountB = cb; }
        }

        // Karo → süsleme reçetesi. Buradaki SIRA önemsiz; eksik id'ler süslemesiz (düz renkli) çıkar.
        private static readonly Recipe[] Recipes =
        {
            // ── Sınır dekoru ──
            new Recipe(TileCatalog.SigSu,       Deco.Ripples, 3),
            new Recipe(TileCatalog.DerinSu,     Deco.DeepWater, 1),
            new Recipe(TileCatalog.SisPerdesi,  Deco.Mist, 5),
            new Recipe(TileCatalog.UzakKayalik, Deco.Boulders, 3, Deco.Water, 1),
            new Recipe(TileCatalog.Girdap,      Deco.Whirl, 1),

            // ── Ova ailesi ──
            new Recipe(TileCatalog.Ova,         Deco.Grass, 3),
            new Recipe(TileCatalog.Cayir,       Deco.Flowers, 7),
            new Recipe(TileCatalog.UzunOt,      Deco.Grass, 9),
            new Recipe(TileCatalog.Bozkir,      Deco.Grass, 5, Deco.Pebbles, 2),
            new Recipe(TileCatalog.KurakToprak, Deco.Pebbles, 4),
            new Recipe(TileCatalog.Tundra,      Deco.Bush, 3, Deco.Pebbles, 2),
            new Recipe(TileCatalog.KarliOva,    Deco.Rocks, 2, Deco.Bush, 1),
            new Recipe(TileCatalog.Kumul,       Deco.Ripples, 4),
            new Recipe(TileCatalog.SahilKumu,   Deco.Pebbles, 3, Deco.Reeds, 2),
            new Recipe(TileCatalog.Bataklik,    Deco.Reeds, 5, Deco.Water, 1),
            new Recipe(TileCatalog.YosunTarla,  Deco.Bush, 5),
            new Recipe(TileCatalog.Lavanta,     Deco.Flowers, 11),

            // ── Taş özü ──
            new Recipe(TileCatalog.TaslikOva,    Deco.Rocks, 3),
            new Recipe(TileCatalog.BolTaslikOva, Deco.Rocks, 6),
            new Recipe(TileCatalog.CakilYatagi,  Deco.Pebbles, 9),
            new Recipe(TileCatalog.KayaYigini,   Deco.Boulders, 3, Deco.Pebbles, 3),
            new Recipe(TileCatalog.KilliYamac,   Deco.Slabs, 3),
            new Recipe(TileCatalog.MermerDamari, Deco.Slabs, 4, Deco.Crystals, 2),
            new Recipe(TileCatalog.ObsidyenTarla,Deco.Crystals, 5),
            new Recipe(TileCatalog.KirmiziKaya,  Deco.Pillar, 2, Deco.Rocks, 2),

            // ── Doğa özü ──
            new Recipe(TileCatalog.AzAgacliOva,  Deco.Broadleaf, 1, Deco.Grass, 3),
            new Recipe(TileCatalog.Orman,        Deco.Broadleaf, 3),
            new Recipe(TileCatalog.YuksekOrman,  Deco.Broadleaf, 4, Deco.Bush, 2),
            new Recipe(TileCatalog.CamOrmani,    Deco.Conifer, 3),
            new Recipe(TileCatalog.HusKorusu,    Deco.Birch, 4),
            new Recipe(TileCatalog.BambuKoru,    Deco.Bamboo, 8),
            new Recipe(TileCatalog.MeyveBahcesi, Deco.Broadleaf, 4, Deco.Flowers, 4),
            new Recipe(TileCatalog.MantarOrmani, Deco.Mushroom, 5),
            new Recipe(TileCatalog.BataklikOtu,  Deco.Reeds, 6),
            new Recipe(TileCatalog.Fundalik,     Deco.Bush, 6),
            new Recipe(TileCatalog.Sazlik,       Deco.Reeds, 8),
            new Recipe(TileCatalog.KavakSirasi,  Deco.Birch, 3, Deco.Grass, 3),

            // ── Geçitler ──
            new Recipe(TileCatalog.Kopru,     Deco.Deck, 1),
            new Recipe(TileCatalog.SigGecit,  Deco.Stepping, 5, Deco.Ripples, 2),
            new Recipe(TileCatalog.DagGecidi, Deco.PassPost, 2, Deco.Rocks, 3),

            // ── Dağ / kaya ──
            new Recipe(TileCatalog.Dag,          Deco.Peak, 1),
            new Recipe(TileCatalog.YuksekDag,    Deco.SnowPeak, 1),
            new Recipe(TileCatalog.Kayalik,      Deco.Boulders, 5),
            new Recipe(TileCatalog.Ucurum,       Deco.Cliff, 1),
            new Recipe(TileCatalog.VolkanikKaya, Deco.Peak, 1, Deco.Crystals, 3),
            new Recipe(TileCatalog.DevKaya,      Deco.Boulders, 1),
            new Recipe(TileCatalog.SutunKaya,    Deco.Pillar, 3),

            // ── Sık orman / göl ──
            new Recipe(TileCatalog.SikOrman,      Deco.Conifer, 6),
            new Recipe(TileCatalog.KaranlikOrman, Deco.Conifer, 5, Deco.DeadTree, 2),
            new Recipe(TileCatalog.KadimOrman,    Deco.Broadleaf, 4, Deco.Vines, 3),
            new Recipe(TileCatalog.DikenliCalilik,Deco.Bush, 8, Deco.DeadTree, 1),
            new Recipe(TileCatalog.Gol,           Deco.Water, 1, Deco.Ripples, 3),
            new Recipe(TileCatalog.BuzGolu,       Deco.Ice, 4),
            new Recipe(TileCatalog.BataklikGolu,  Deco.Water, 1, Deco.Reeds, 4),
            new Recipe(TileCatalog.KaynakGolu,    Deco.Water, 1, Deco.Mist, 3),

            // ── Nehir ──
            new Recipe(TileCatalog.Nehir,   Deco.Water, 1, Deco.Ripples, 4),
            new Recipe(TileCatalog.Sellale, Deco.Water, 1, Deco.Mist, 4),

            // ── Landmark ──
            new Recipe(TileCatalog.Dikilitas,    Deco.Obelisk, 1),
            new Recipe(TileCatalog.Harabe,       Deco.Ruin, 1),
            new Recipe(TileCatalog.TasDaire,     Deco.StoneCircle, 1),
            new Recipe(TileCatalog.DevAgac,      Deco.GiantTree, 1),
            new Recipe(TileCatalog.Krater,       Deco.Crater, 1),
            new Recipe(TileCatalog.KemikTarlasi, Deco.Bones, 5),
            new Recipe(TileCatalog.TerkKule,     Deco.Tower, 1),
            new Recipe(TileCatalog.Sunak,        Deco.Altar, 1),
            new Recipe(TileCatalog.KadimKapi,    Deco.Gate, 1),
            new Recipe(TileCatalog.DusmusDev,    Deco.Statue, 1),
            new Recipe(TileCatalog.IsikCukuru,   Deco.GlowPit, 1),
            new Recipe(TileCatalog.GemiEnkazi,   Deco.Wreck, 1),
        };

        // ═════════════════════════════════════════════════════════════════════
        //  Menü
        // ═════════════════════════════════════════════════════════════════════

        [MenuItem("TacticalRPG/Karo/Organik Harita Karolarini Kur (renkli + dokulu)", false, 28)]
        public static void BuildAllMenu()
        {
            int n = BuildAll(force: false);
            EditorUtility.DisplayDialog("Karo Kurulumu",
                $"{n} karo tipi kuruldu/guncellendi.\n\nHarita bir sonraki uretimde (Play ya da TAM KURULUM)\n" +
                "renkli ve dokulu gelir.", "Tamam");
        }

        [MenuItem("TacticalRPG/Karo/Organik Harita Karolarini YENIDEN Uret (hepsini ez)", false, 29)]
        public static void RebuildAllMenu()
        {
            if (!EditorUtility.DisplayDialog("Karolari Yeniden Uret",
                    "Uretilmis karo prefab/materyal/dokulari YENIDEN olusturulacak (elle yaptigin\n" +
                    "degisiklikler kaybolur). Devam?", "Evet, ez", "Vazgec")) return;
            int n = BuildAll(force: true);
            EditorUtility.DisplayDialog("Karo Kurulumu", $"{n} karo tipi yeniden uretildi.", "Tamam");
        }

        /// <summary>Batch-mode girişi (Unity'yi elle açmadan doğrulama/kurulum için):
        /// <c>Unity.exe -batchmode -quit -projectPath … -executeMethod
        /// TacticalRPG.Editor.TileVisualFactory.BuildAllBatch</c>. Dialog göstermez.</summary>
        public static void BuildAllBatch() => BuildAll(force: false);

        /// <summary>Tüm katalog karolarını üretip palete bağlar. Üretilen sayıyı döner.</summary>
        public static int BuildAll(bool force)
        {
            TilePaletteSO palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>("Assets/Data/Map/TilePalette.asset");
            if (palette == null) { Debug.LogError("[Karo] TilePalette.asset bulunamadi."); return 0; }

            EnsureFolder(TexFolder); EnsureFolder(MatFolder); EnsureFolder(PrefabFolder);

            // 1) Yüzey dokuları (gri tonlu — renk materyalden gelir).
            //    İKİ FAZ: önce PNG'ler diske yazılır, Refresh ile içe aktarılır, SONRA importer
            //    ayarları yazılır. Tek fazda yapılırsa (StartAssetEditing içinde) ImportAsset
            //    ertelenir ve AssetImporter.GetAtPath null döner — ayarlar sessizce uygulanmaz.
            var written = new List<Surface>();
            foreach (Surface s in System.Enum.GetValues(typeof(Surface)))
                if (WriteSurfaceTexture(s, force)) written.Add(s);
            if (written.Count > 0) AssetDatabase.Refresh();
            foreach (var s in written) ConfigureSurfaceTexture(s);

            // 2) Karo başına materyal + prefab
            var built = new List<(TileCatalog.Entry entry, GameObject prefab)>();
            int i = 0;
            foreach (var e in TileCatalog.All)
            {
                if (e.Family == TileFamily.Void) continue;
                // Davul karolarının modelleri BU üreteçte değil, Augments dosyasında yapılır
                // (özel geometri + tek renk teması). Burada üretilseydi düz renkli birer taş
                // olurlar ve az önce yazılan özel prefabı EZERLERDİ.
                if (e.Family == TileFamily.Augment) continue;
                EditorUtility.DisplayProgressBar("Karo uretimi", e.Name, i++ / (float)TileCatalog.All.Length);
                GameObject prefab = EnsureTilePrefab(e, force);
                if (prefab != null) built.Add((e, prefab));
            }
            EditorUtility.ClearProgressBar();

            // 3) Palet girişleri
            int touched = WritePalette(palette, built, force);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Karo] {built.Count} karo prefabi hazir, palette {touched} giris yazildi.");
            VerifyAgainstGenerator(palette);
            return built.Count;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Dokular — prosedürel, gri tonlu
        // ═════════════════════════════════════════════════════════════════════

        private static string TexPath(Surface s) => $"{TexFolder}/Surface_{s}.png";

        /// <summary>Yüzey ailesine göre gri tonlu bir doku üretip PNG olarak diske yazar.
        /// Renk KATILMAZ (materyalin _BaseColor'ı çarpar) — 12 doku 70+ karoya yeter.
        /// Yazdıysa true döner (çağıran Refresh + importer ayarı yapar).</summary>
        private static bool WriteSurfaceTexture(Surface s, bool force)
        {
            string path = TexPath(s);
            if (!force && AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return false;

            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
            int seed = (int)s * 7717 + 13;

            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float lum = Mathf.Clamp(SurfaceLuma(s, u, v, seed), 0.55f, 1.35f);
                    tex.SetPixel(x, y, new Color(lum, lum, lum));
                }

            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }

        private static void ConfigureSurfaceTexture(Surface s)
        {
            if (!(AssetImporter.GetAtPath(TexPath(s)) is TextureImporter imp)) return;
            imp.wrapMode           = TextureWrapMode.Repeat;
            imp.filterMode         = FilterMode.Bilinear;
            imp.mipmapEnabled      = true;
            imp.maxTextureSize     = 256;
            imp.textureCompression = TextureImporterCompression.CompressedLQ;
            imp.SaveAndReimport();
        }

        /// <summary>Yüzey ailesinin parlaklık deseni. Doku SEAMLESS olsun diye gürültü
        /// koordinatları toroidal (u,v → sin/cos) değil, tekrar eden düşük frekansla alınır —
        /// karo başına tek karo görüldüğü için dikiş görünmez, basit tutuldu.</summary>
        private static float SurfaceLuma(Surface s, float u, float v, int seed)
        {
            float fine  = MapNoise.Fbm(u * 34f, v * 34f, seed, 3) * 0.5f + 0.5f;
            float mid   = MapNoise.Fbm(u * 11f, v * 11f, seed + 5, 3) * 0.5f + 0.5f;
            float coarse= MapNoise.Fbm(u * 4.5f, v * 4.5f, seed + 9, 2) * 0.5f + 0.5f;
            float spot  = MapNoise.White((int)(u * 128f), (int)(v * 128f), seed);

            switch (s)
            {
                case Surface.Grass:  return 0.86f + mid * 0.24f + (spot < 0.10f ? 0.16f : 0f);
                case Surface.Dry:    return 0.84f + coarse * 0.22f + fine * 0.12f;
                case Surface.Sand:   return 0.90f + Mathf.Sin((v + coarse * 0.4f) * 44f) * 0.06f + fine * 0.08f;
                case Surface.Snow:   return 0.94f + mid * 0.14f + (spot > 0.965f ? 0.28f : 0f);
                case Surface.Rock:   return 0.72f + coarse * 0.34f + (fine < 0.30f ? -0.14f : 0f);
                case Surface.Dirt:   return 0.78f + fine * 0.30f + coarse * 0.10f;
                case Surface.Swamp:  return 0.74f + mid * 0.30f + (spot < 0.18f ? -0.14f : 0f);
                case Surface.Water:  return 0.86f + Mathf.Sin((u * 9f + mid * 5.5f) * 2.4f) * 0.13f;
                case Surface.Ice:    return 0.92f + Mathf.Abs(MapNoise.Perlin(u * 7f, v * 7f, seed)) * 0.26f;
                case Surface.Ash:    return 0.70f + fine * 0.34f + (spot > 0.95f ? 0.22f : 0f);
                case Surface.Moss:   return 0.80f + mid * 0.26f + (spot < 0.14f ? 0.18f : 0f);
                case Surface.Stone:  // kaba taş blok deseni
                {
                    float bx = Mathf.Repeat(u * 5f, 1f), by = Mathf.Repeat(v * 5f + (Mathf.Floor(u * 5f) % 2f) * 0.5f, 1f);
                    float line = (bx < 0.055f || by < 0.055f) ? -0.18f : 0f;
                    return 0.88f + coarse * 0.16f + line;
                }
                default: return 1f;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Materyaller
        // ═════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, Material> MatCache = new();

        private static Shader LitShader =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        /// <summary>Karo zemini için renkli + dokulu materyal.</summary>
        private static Material EnsureGroundMaterial(TileCatalog.Entry e, bool force)
        {
            string path = $"{MatFolder}/Tile_{e.Id}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !force) return existing;

            Material mat = existing ?? new Material(LitShader);
            mat.shader = LitShader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(e.Surface));
            var col = new Color(e.R, e.G, e.B);

            SetColor(mat, col);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", new Vector2(2.2f, 2.2f)); }
                if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); mat.SetTextureScale("_MainTex", new Vector2(2.2f, 2.2f)); }
            }
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", e.Surface == Surface.Water || e.Surface == Surface.Ice ? 0.62f : 0.06f);
            mat.enableInstancing = true;

            if (existing == null) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Süsleme için düz renkli materyal (renge göre önbelleklenir — materyal patlaması yok).</summary>
        private static Material SolidMat(string name, Color c, float smooth = 0.06f)
        {
            string key = name;
            if (MatCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            string path = $"{MatFolder}/Deco_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader);
                SetColor(mat, c);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            MatCache[key] = mat;
            return mat;
        }

        private static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
        }

        /// <summary>Karonun kendi renginden türetilmiş yaprak/gövde rengi — orman karosu içinde
        /// bulunduğu biyomun rengini taşısın (kar altında koyu yeşil, kurakta zeytin yeşili…).</summary>
        private static Material LeafMat(TileCatalog.Entry e, float darken)
        {
            Color c = new Color(e.R, e.G, e.B) * darken;
            c.a = 1f;
            string key = $"Leaf_{Mathf.RoundToInt(c.r * 20)}_{Mathf.RoundToInt(c.g * 20)}_{Mathf.RoundToInt(c.b * 20)}";
            return SolidMat(key, c);
        }

        private static Material Bark   => SolidMat("Bark",   new Color(0.32f, 0.22f, 0.14f));
        private static Material Stone  => SolidMat("Stone",  new Color(0.58f, 0.57f, 0.54f));
        private static Material Dark   => SolidMat("Dark",   new Color(0.10f, 0.10f, 0.12f));
        private static Material Snow   => SolidMat("Snow",   new Color(0.93f, 0.95f, 0.98f));
        private static Material WaterM => SolidMat("Water",  new Color(0.22f, 0.48f, 0.72f), 0.75f);
        private static Material IceM   => SolidMat("Ice",    new Color(0.74f, 0.88f, 0.94f), 0.80f);
        private static Material MistM  => SolidMat("Mist",   new Color(0.86f, 0.88f, 0.92f), 0.02f);
        private static Material BoneM  => SolidMat("Bone",   new Color(0.86f, 0.84f, 0.74f));
        private static Material GlowM  => SolidMat("Glow",   new Color(0.55f, 0.95f, 0.85f), 0.55f);
        private static Material WoodM  => SolidMat("Wood",   new Color(0.55f, 0.40f, 0.25f));
        private static Material RustM  => SolidMat("Rust",   new Color(0.55f, 0.30f, 0.20f));
        private static Material MossM  => SolidMat("Moss",   new Color(0.32f, 0.45f, 0.26f));

        // ═════════════════════════════════════════════════════════════════════
        //  Prefab üretimi
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject EnsureTilePrefab(TileCatalog.Entry e, bool force)
        {
            string path = $"{PrefabFolder}/Tile_{e.Id}.prefab";
            if (!force)
            {
                var have = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (have != null) return have;
            }

            GameObject root = NewBase(e, force);
            if (root == null) return null;

            Recipe? rec = FindRecipe(e.Id);
            if (rec.HasValue)
            {
                var rnd = new System.Random(e.Id.GetHashCode());
                AddDeco(root.transform, e, rec.Value.A, rec.Value.CountA, rnd);
                AddDeco(root.transform, e, rec.Value.B, rec.Value.CountB, rnd);
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Recipe? FindRecipe(string id)
        {
            foreach (var r in Recipes) if (r.Id == id) return r;
            return null;
        }

        /// <summary>Karonun TABANI: mevcut `Tile_default` prefabının kopyası (ayak izi/pivot zaten
        /// doğru), zemin materyali karonun kendi rengiyle DEĞİŞTİRİLMİŞ. Taban prefab yoksa
        /// koddan hex prizma üretilir (proje FBX'siz de çalışsın).</summary>
        private static GameObject NewBase(TileCatalog.Entry e, bool force)
        {
            Material ground = EnsureGroundMaterial(e, force);
            GameObject baseTile = AssetDatabase.LoadAssetAtPath<GameObject>(BaseTilePath);
            GameObject go;

            if (baseTile != null)
            {
                go = Object.Instantiate(baseTile);
                // Tile_default İÇİNDE bir FBX prefab örneği barındırıyor. Materyali doğrudan
                // yazmak "override" olarak kaydedilirdi; iç içe bağlantıyı çözünce yeni prefab
                // tamamen bağımsız olur (kaynak FBX değişse de üretilmiş karolar bozulmaz).
                foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                    if (tr != null && PrefabUtility.IsAnyPrefabInstanceRoot(tr.gameObject))
                        PrefabUtility.UnpackPrefabInstance(tr.gameObject, PrefabUnpackMode.Completely,
                                                           InteractionMode.AutomatedAction);
            }
            else
            {
                go = new GameObject("tile", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                Mesh mesh = HexMetrics.CreateHexMesh(0.95f);
                go.GetComponent<MeshFilter>().sharedMesh   = mesh;
                go.GetComponent<MeshCollider>().sharedMesh = mesh;
            }

            go.name = $"Tile_{e.Id}";
            // ZEMİN RENGİ: taban modelin beyaz materyali karo rengiyle değiştirilir. "Her karo beyaz"
            // sorununun çözümü tam olarak bu satır — palet rengi sadece editörde görünüyordu,
            // sahnede model kendi (beyaz) materyalini kullanıyordu.
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[Mathf.Max(1, mr.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = ground;
                mr.sharedMaterials = mats;
            }
            return go;
        }

        private static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos,
                                       Vector3 scale, Vector3 euler, Material mat)
        {
            GameObject d = GameObject.CreatePrimitive(type);
            d.transform.SetParent(parent, false);
            d.transform.localPosition    = pos;
            d.transform.localScale       = scale;
            d.transform.localEulerAngles = euler;
            var col = d.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // tıklama karoya geçsin
            d.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return d;
        }

        private static GameObject Cone(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            // Unity'de koni primitifi YOK — düşük poli koni asset'i SceneSetupTool üretir/paylaşır.
            Mesh cone = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath) ?? SceneSetupTool.EnsureConeMesh();
            if (cone == null) return Prim(parent, PrimitiveType.Cylinder, pos,
                                          new Vector3(scale.x, scale.y * 0.5f, scale.z), Vector3.zero, mat);
            var go = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.GetComponent<MeshFilter>().sharedMesh       = cone;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private const float Y0 = HexMetrics.TileHeight;   // karonun üst yüzeyi

        private static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>Rastgele ama karo içinde kalan bir konum (yarıçap 0..0.62).</summary>
        private static Vector3 Spot(System.Random r, float maxRadius = 0.62f, float y = Y0)
        {
            float ang = Rf(r, 0f, 6.2831853f), rad = Mathf.Sqrt((float)r.NextDouble()) * maxRadius;
            return new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
        }

        private static void AddDeco(Transform t, TileCatalog.Entry e, Deco kind, int count, System.Random r)
        {
            if (kind == Deco.None || count <= 0) return;

            switch (kind)
            {
                // ── Taş ──
                case Deco.Rocks:
                    for (int i = 0; i < count; i++)
                    { float s = Rf(r, 0.14f, 0.26f);
                      Prim(t, PrimitiveType.Sphere, Spot(r) + Vector3.up * s * 0.3f,
                           new Vector3(s * 1.35f, s * 0.8f, s), new Vector3(Rf(r,0,25), Rf(r,0,360), Rf(r,0,25)), Stone); }
                    break;
                case Deco.Boulders:
                    for (int i = 0; i < count; i++)
                    { float s = Rf(r, 0.30f, 0.52f);
                      Prim(t, PrimitiveType.Sphere, Spot(r, 0.45f) + Vector3.up * s * 0.25f,
                           new Vector3(s * 1.2f, s * 0.95f, s), new Vector3(Rf(r,0,20), Rf(r,0,360), Rf(r,0,20)), Stone); }
                    break;
                case Deco.Pebbles:
                    for (int i = 0; i < count; i++)
                    { float s = Rf(r, 0.06f, 0.13f);
                      Prim(t, PrimitiveType.Sphere, Spot(r) + Vector3.up * s * 0.25f,
                           new Vector3(s * 1.6f, s * 0.55f, s * 1.2f), new Vector3(0, Rf(r,0,360), 0), Stone); }
                    break;
                case Deco.Slabs:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cube, Spot(r, 0.48f) + Vector3.up * 0.05f,
                           new Vector3(Rf(r,0.24f,0.42f), 0.10f, Rf(r,0.20f,0.34f)),
                           new Vector3(Rf(r,-8,8), Rf(r,0,360), Rf(r,-8,8)), Stone);
                    break;
                case Deco.Crystals:
                    for (int i = 0; i < count; i++)
                    { float hgt = Rf(r, 0.22f, 0.46f);
                      Cone(t, Spot(r, 0.48f), new Vector3(0.16f, hgt, 0.16f), LeafMat(e, 1.35f)); }
                    break;
                case Deco.Pillar:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cylinder, Spot(r, 0.42f) + Vector3.up * Rf(r,0.30f,0.55f),
                           new Vector3(Rf(r,0.12f,0.20f), Rf(r,0.30f,0.55f), Rf(r,0.12f,0.20f)),
                           new Vector3(Rf(r,-6,6), 0, Rf(r,-6,6)), Stone);
                    break;

                // ── Ağaç / bitki ──
                case Deco.Conifer:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.50f); float hgt = Rf(r, 0.55f, 0.95f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.12f,
                           new Vector3(0.055f, hgt * 0.12f, 0.055f), Vector3.zero, Bark);
                      Cone(t, b + Vector3.up * hgt * 0.18f, new Vector3(0.30f, hgt * 0.55f, 0.30f), LeafMat(e, 0.72f));
                      Cone(t, b + Vector3.up * hgt * 0.50f, new Vector3(0.22f, hgt * 0.45f, 0.22f), LeafMat(e, 0.72f)); }
                    break;
                case Deco.Broadleaf:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.50f); float hgt = Rf(r, 0.40f, 0.70f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.06f, hgt * 0.5f, 0.06f), Vector3.zero, Bark);
                      Prim(t, PrimitiveType.Sphere, b + Vector3.up * (hgt + 0.10f),
                           new Vector3(0.38f, 0.30f, 0.38f), new Vector3(0, Rf(r,0,360), 0), LeafMat(e, 0.80f)); }
                    break;
                case Deco.Birch:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.52f); float hgt = Rf(r, 0.55f, 0.85f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.045f, hgt * 0.5f, 0.045f), Vector3.zero, SolidMat("Birch", new Color(0.88f,0.87f,0.82f)));
                      Prim(t, PrimitiveType.Sphere, b + Vector3.up * (hgt + 0.06f),
                           new Vector3(0.26f, 0.30f, 0.26f), Vector3.zero, LeafMat(e, 0.90f)); }
                    break;
                case Deco.Bamboo:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.55f); float hgt = Rf(r, 0.45f, 0.80f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.035f, hgt * 0.5f, 0.035f), new Vector3(Rf(r,-7,7),0,Rf(r,-7,7)), LeafMat(e, 0.95f)); }
                    break;
                case Deco.DeadTree:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.45f); float hgt = Rf(r, 0.40f, 0.65f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.05f, hgt * 0.5f, 0.05f), new Vector3(Rf(r,-12,12),0,Rf(r,-12,12)), Bark);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.85f,
                           new Vector3(0.03f, 0.14f, 0.03f), new Vector3(0,0,62), Bark); }
                    break;
                case Deco.GiantTree:
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0, Y0 + 0.55f, 0),
                         new Vector3(0.20f, 0.55f, 0.20f), Vector3.zero, Bark);
                    Prim(t, PrimitiveType.Sphere, new Vector3(0, Y0 + 1.20f, 0),
                         new Vector3(0.95f, 0.72f, 0.95f), Vector3.zero, LeafMat(e, 0.75f));
                    Prim(t, PrimitiveType.Sphere, new Vector3(0.28f, Y0 + 0.95f, -0.20f),
                         new Vector3(0.52f, 0.42f, 0.52f), Vector3.zero, LeafMat(e, 0.68f));
                    break;
                case Deco.Bush:
                    for (int i = 0; i < count; i++)
                    { float s = Rf(r, 0.14f, 0.24f);
                      Prim(t, PrimitiveType.Sphere, Spot(r) + Vector3.up * s * 0.6f,
                           new Vector3(s * 1.4f, s, s * 1.4f), Vector3.zero, LeafMat(e, 0.78f)); }
                    break;
                case Deco.Grass:
                    for (int i = 0; i < count; i++)
                    { float hgt = Rf(r, 0.10f, 0.22f);
                      Prim(t, PrimitiveType.Cube, Spot(r) + Vector3.up * hgt * 0.5f,
                           new Vector3(0.035f, hgt, 0.035f), new Vector3(Rf(r,-16,16), Rf(r,0,360), Rf(r,-16,16)),
                           LeafMat(e, 0.85f)); }
                    break;
                case Deco.Reeds:
                    for (int i = 0; i < count; i++)
                    { float hgt = Rf(r, 0.22f, 0.42f);
                      Prim(t, PrimitiveType.Cylinder, Spot(r, 0.55f) + Vector3.up * hgt * 0.5f,
                           new Vector3(0.022f, hgt * 0.5f, 0.022f), new Vector3(Rf(r,-14,14), 0, Rf(r,-14,14)),
                           LeafMat(e, 0.90f)); }
                    break;
                case Deco.Flowers:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r); float hgt = Rf(r, 0.09f, 0.16f);
                      Prim(t, PrimitiveType.Cube, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.02f, hgt, 0.02f), Vector3.zero, LeafMat(e, 0.75f));
                      Prim(t, PrimitiveType.Sphere, b + Vector3.up * (hgt + 0.03f),
                           new Vector3(0.06f, 0.05f, 0.06f), Vector3.zero, LeafMat(e, 1.45f)); }
                    break;
                case Deco.Mushroom:
                    for (int i = 0; i < count; i++)
                    { Vector3 b = Spot(r, 0.52f); float hgt = Rf(r, 0.16f, 0.34f);
                      Prim(t, PrimitiveType.Cylinder, b + Vector3.up * hgt * 0.5f,
                           new Vector3(0.05f, hgt * 0.5f, 0.05f), Vector3.zero, BoneM);
                      Prim(t, PrimitiveType.Sphere, b + Vector3.up * hgt,
                           new Vector3(0.24f, 0.13f, 0.24f), Vector3.zero, LeafMat(e, 1.30f)); }
                    break;
                case Deco.Cactus:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cylinder, Spot(r, 0.40f) + Vector3.up * 0.24f,
                           new Vector3(0.09f, 0.24f, 0.09f), Vector3.zero, LeafMat(e, 0.85f));
                    break;
                case Deco.Vines:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cube, Spot(r, 0.55f) + Vector3.up * 0.30f,
                           new Vector3(0.03f, 0.30f, 0.03f), new Vector3(Rf(r,-25,25), Rf(r,0,360), Rf(r,-25,25)), MossM);
                    break;

                // ── Dağ ──
                case Deco.Peak:
                    Cone(t, new Vector3(0, Y0, 0), new Vector3(1.35f, 1.25f, 1.35f), Stone);
                    Cone(t, new Vector3(-0.45f, Y0, 0.34f), new Vector3(0.70f, 0.60f, 0.70f), Stone);
                    Cone(t, new Vector3(0.47f, Y0, -0.30f), new Vector3(0.58f, 0.48f, 0.58f), Stone);
                    break;
                case Deco.SnowPeak:
                    Cone(t, new Vector3(0, Y0, 0), new Vector3(1.40f, 1.55f, 1.40f), Stone);
                    Cone(t, new Vector3(0, Y0 + 1.16f, 0), new Vector3(0.46f, 0.42f, 0.46f), Snow);
                    Cone(t, new Vector3(-0.44f, Y0, 0.32f), new Vector3(0.66f, 0.70f, 0.66f), Stone);
                    break;
                case Deco.Cliff:
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.10f, Y0 + 0.45f, 0.05f),
                         new Vector3(1.05f, 0.90f, 0.70f), new Vector3(0, 14f, -7f), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0.34f, Y0 + 0.22f, -0.26f),
                         new Vector3(0.55f, 0.45f, 0.50f), new Vector3(0, -22f, 5f), Stone);
                    break;
                case Deco.Arch:
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.34f, Y0 + 0.35f, 0),
                         new Vector3(0.18f, 0.70f, 0.22f), Vector3.zero, Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0.34f, Y0 + 0.35f, 0),
                         new Vector3(0.18f, 0.70f, 0.22f), Vector3.zero, Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.74f, 0),
                         new Vector3(0.90f, 0.16f, 0.24f), Vector3.zero, Stone);
                    break;

                // ── Su / sis ──
                case Deco.Water:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 - 0.03f, 0),
                         new Vector3(1.55f, 0.05f, 1.75f), Vector3.zero, WaterM);
                    break;
                case Deco.DeepWater:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 - 0.05f, 0),
                         new Vector3(1.60f, 0.05f, 1.80f), Vector3.zero,
                         SolidMat("DeepWater", new Color(0.10f, 0.24f, 0.42f), 0.80f));
                    break;
                case Deco.Ripples:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cube, Spot(r, 0.55f, Y0 + 0.02f),
                           new Vector3(Rf(r,0.24f,0.48f), 0.015f, 0.05f), new Vector3(0, Rf(r,0,360), 0),
                           SolidMat("Foam", new Color(0.90f, 0.95f, 1.00f), 0.5f));
                    break;
                case Deco.Ice:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cube, Spot(r, 0.45f, Y0 + 0.02f),
                           new Vector3(Rf(r,0.35f,0.65f), 0.05f, Rf(r,0.30f,0.55f)),
                           new Vector3(Rf(r,-4,4), Rf(r,0,360), Rf(r,-4,4)), IceM);
                    break;
                case Deco.Mist:
                    for (int i = 0; i < count; i++)
                    { float s = Rf(r, 0.42f, 0.78f);
                      Prim(t, PrimitiveType.Sphere, Spot(r, 0.40f) + Vector3.up * Rf(r, 0.10f, 0.45f),
                           new Vector3(s, s * 0.62f, s), Vector3.zero, MistM); }
                    break;
                case Deco.Whirl:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 - 0.03f, 0),
                         new Vector3(1.55f, 0.05f, 1.75f), Vector3.zero, WaterM);
                    for (int i = 0; i < 3; i++)
                      Prim(t, PrimitiveType.Cylinder, new Vector3(0, Y0 + 0.02f + i * 0.03f, 0),
                           new Vector3(0.62f - i * 0.18f, 0.012f, 0.62f - i * 0.18f), new Vector3(0, i * 40f, 0),
                           SolidMat("Foam", new Color(0.90f, 0.95f, 1.00f), 0.5f));
                    break;

                // ── Landmark ──
                case Deco.Bones:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cylinder, Spot(r, 0.55f) + Vector3.up * 0.05f,
                           new Vector3(0.045f, Rf(r,0.12f,0.24f), 0.045f),
                           new Vector3(88f, Rf(r,0,360), 0), BoneM);
                    Prim(t, PrimitiveType.Sphere, new Vector3(0.18f, Y0 + 0.10f, 0.12f),
                         new Vector3(0.20f, 0.18f, 0.22f), Vector3.zero, BoneM);
                    break;
                case Deco.Obelisk:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.62f, 0),
                         new Vector3(0.26f, 1.25f, 0.26f), new Vector3(0, 22f, 0), Stone);
                    Cone(t, new Vector3(0, Y0 + 1.24f, 0), new Vector3(0.28f, 0.22f, 0.28f), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.05f, 0),
                         new Vector3(0.52f, 0.10f, 0.52f), new Vector3(0, 22f, 0), Stone);
                    break;
                case Deco.Ruin:
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.30f, Y0 + 0.28f, 0.22f),
                         new Vector3(0.16f, 0.56f, 0.16f), new Vector3(4f, 0, -6f), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0.26f, Y0 + 0.18f, 0.26f),
                         new Vector3(0.16f, 0.36f, 0.16f), new Vector3(-5f, 0, 8f), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0.02f, Y0 + 0.10f, -0.30f),
                         new Vector3(0.80f, 0.20f, 0.22f), new Vector3(0, 12f, 0), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.34f, Y0 + 0.04f, -0.14f),
                         new Vector3(0.34f, 0.08f, 0.30f), new Vector3(0, 40f, 0), MossM);
                    break;
                case Deco.StoneCircle:
                    for (int i = 0; i < 7; i++)
                    { float a = i / 7f * 6.2831853f;
                      Prim(t, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.46f, Y0 + 0.24f, Mathf.Sin(a) * 0.46f),
                           new Vector3(0.13f, 0.48f, 0.13f), new Vector3(0, -a * Mathf.Rad2Deg, Rf(r,-5,5)), Stone); }
                    break;
                case Deco.Crater:
                    for (int i = 0; i < 9; i++)
                    { float a = i / 9f * 6.2831853f;
                      Prim(t, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.55f, Y0 + 0.05f, Mathf.Sin(a) * 0.55f),
                           new Vector3(0.26f, 0.16f, 0.26f), Vector3.zero, Stone); }
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0, Y0 - 0.02f, 0),
                         new Vector3(0.72f, 0.02f, 0.72f), Vector3.zero, Dark);
                    break;
                case Deco.Tower:
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0.06f, Y0 + 0.62f, 0),
                         new Vector3(0.34f, 0.62f, 0.34f), new Vector3(0, 0, 4f), Stone);
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0.10f, Y0 + 1.20f, 0),
                         new Vector3(0.30f, 0.10f, 0.30f), new Vector3(0, 0, 4f), Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.42f, Y0 + 0.12f, 0.22f),
                         new Vector3(0.30f, 0.24f, 0.26f), new Vector3(0, 24f, 0), Stone);
                    break;
                case Deco.Altar:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.16f, 0),
                         new Vector3(0.62f, 0.32f, 0.48f), Vector3.zero, Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.36f, 0),
                         new Vector3(0.74f, 0.10f, 0.60f), Vector3.zero, Stone);
                    Prim(t, PrimitiveType.Sphere, new Vector3(0, Y0 + 0.52f, 0),
                         new Vector3(0.20f, 0.20f, 0.20f), Vector3.zero, GlowM);
                    break;
                case Deco.Gate:
                    AddDeco(t, e, Deco.Arch, 1, r);
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.40f, 0),
                         new Vector3(0.56f, 0.62f, 0.06f), Vector3.zero, Dark);
                    break;
                case Deco.Statue:
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.10f, Y0 + 0.13f, 0),
                         new Vector3(1.10f, 0.26f, 0.30f), new Vector3(0, 18f, -4f), Stone);
                    Prim(t, PrimitiveType.Sphere, new Vector3(0.44f, Y0 + 0.20f, 0.10f),
                         new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, Stone);
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.48f, Y0 + 0.10f, -0.16f),
                         new Vector3(0.30f, 0.20f, 0.20f), new Vector3(0, 40f, 22f), MossM);
                    break;
                case Deco.GlowPit:
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0, Y0 + 0.01f, 0),
                         new Vector3(0.70f, 0.02f, 0.70f), Vector3.zero, GlowM);
                    for (int i = 0; i < 5; i++)
                      Prim(t, PrimitiveType.Sphere, Spot(r, 0.40f) + Vector3.up * Rf(r, 0.10f, 0.40f),
                           Vector3.one * Rf(r, 0.06f, 0.13f), Vector3.zero, GlowM);
                    break;
                case Deco.Wreck:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.16f, 0),
                         new Vector3(0.95f, 0.30f, 0.42f), new Vector3(0, 26f, 22f), WoodM);
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0.10f, Y0 + 0.45f, 0.05f),
                         new Vector3(0.05f, 0.42f, 0.05f), new Vector3(0, 0, 34f), WoodM);
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.40f, Y0 + 0.06f, 0.28f),
                         new Vector3(0.34f, 0.10f, 0.20f), new Vector3(0, 52f, 6f), RustM);
                    break;

                // ── Geçit ──
                case Deco.Deck:
                    Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.07f, 0),
                         new Vector3(1.55f, 0.11f, 0.62f), Vector3.zero, WoodM);
                    for (int i = -1; i <= 1; i += 2)
                      Prim(t, PrimitiveType.Cube, new Vector3(0, Y0 + 0.26f, i * 0.31f),
                           new Vector3(1.50f, 0.06f, 0.05f), Vector3.zero, WoodM);
                    for (int i = -1; i <= 1; i += 2)
                        for (int j = -1; j <= 1; j += 2)
                          Prim(t, PrimitiveType.Cylinder, new Vector3(i * 0.58f, Y0 + 0.20f, j * 0.31f),
                               new Vector3(0.05f, 0.20f, 0.05f), Vector3.zero, WoodM);
                    break;
                case Deco.Stepping:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cylinder, new Vector3(-0.60f + i * (1.20f / Mathf.Max(1, count - 1)),
                                                                  Y0 + 0.05f, Rf(r, -0.12f, 0.12f)),
                           new Vector3(0.24f, 0.05f, 0.24f), Vector3.zero, Stone);
                    break;
                case Deco.PassPost:
                    for (int i = 0; i < count; i++)
                      Prim(t, PrimitiveType.Cube, new Vector3(i == 0 ? -0.52f : 0.52f, Y0 + 0.30f, 0f),
                           new Vector3(0.14f, 0.60f, 0.14f), new Vector3(0, 12f, i == 0 ? -5f : 5f), Stone);
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Palet
        // ═════════════════════════════════════════════════════════════════════

        internal static int WritePalette(TilePaletteSO palette,
                                        List<(TileCatalog.Entry entry, GameObject prefab)> built, bool force)
        {
            var so    = new SerializedObject(palette);
            var tiles = so.FindProperty("tiles");
            int touched = 0;

            foreach (var (cat, prefab) in built)
            {
                SerializedProperty slot = null;
                for (int i = 0; i < tiles.arraySize; i++)
                {
                    var el = tiles.GetArrayElementAtIndex(i);
                    if (el.FindPropertyRelative("id").stringValue == cat.Id) { slot = el; break; }
                }

                bool isNew = slot == null;
                if (isNew)
                {
                    // DİKKAT: arraySize++ Unity'de SON ELEMANI KOPYALAR → HER alan açıkça yazılmalı.
                    // (Bir kez atlandı: yeni karolar "magaza" girişinden isStore=1 miras aldı ve
                    // harita boyunca her karoda dükkân açıldı.)
                    tiles.arraySize++;
                    slot = tiles.GetArrayElementAtIndex(tiles.arraySize - 1);
                    slot.FindPropertyRelative("canEnterCombat").boolValue = false;
                    slot.FindPropertyRelative("isStore").boolValue        = false;
                }

                slot.FindPropertyRelative("id").stringValue          = cat.Id;
                slot.FindPropertyRelative("displayName").stringValue = cat.Name;
                slot.FindPropertyRelative("isWalkable").boolValue    = cat.Walkable;
                slot.FindPropertyRelative("editorColor").colorValue  = new Color(cat.R, cat.G, cat.B);
                slot.FindPropertyRelative("surfaceHeightOverride").floatValue =
                    cat.Walkable ? HexMetrics.TileHeight : 0f;   // birim ağacın değil KARONUN üstünde durur

                var prefabProp = slot.FindPropertyRelative("prefab");
                if (isNew || force || prefabProp.objectReferenceValue == null || IsGenerated(prefabProp.objectReferenceValue))
                { prefabProp.objectReferenceValue = prefab; touched++; }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            return touched;
        }

        /// <summary>Üreticiyi GERÇEKTEN bir kez koşturup çıkan her karo id'sinin palette'te
        /// bulunduğunu ve yürünürlüğünün katalogla aynı olduğunu doğrular.
        ///
        /// Neden: bu iki liste ayrışırsa hata SESSİZ olur — eksik id gri placeholder olarak çıkar
        /// ve yürünürlüğü yanlış olabilir (dağın üstünde yürümek gibi). Kurulumun sonunda 1 harita
        /// üretmek birkaç milisaniye; sessiz ayrışmanın maliyeti bir oturum.</summary>
        private static void VerifyAgainstGenerator(TilePaletteSO palette)
        {
            var config = AssetDatabase.LoadAssetAtPath<TerrainConfigSO>("Assets/Data/Config/TerrainConfig.asset");
            TerrainParams pars = config != null ? config.ToParams() : TerrainParams.Default;
            int seed = config != null && config.SeedPool != null && config.SeedPool.Count > 0 ? config.SeedPool[0] : 1;

            MapResult res = TerrainGenerator.Generate(pars, seed);
            var missing  = new HashSet<string>();
            var mismatch = new HashSet<string>();
            for (int q = 0; q < pars.Width; q++)
                for (int r = 0; r < pars.Height; r++)
                {
                    string id = res.Tiles[q, r];
                    if (TileCatalog.IsVoid(id)) continue;
                    var entry = palette.GetById(id);
                    if (entry == null) { missing.Add(id); continue; }
                    if (entry.isWalkable != TileCatalog.IsWalkable(id)) mismatch.Add(id);
                }

            if (missing.Count > 0)
                Debug.LogError($"[Karo] Palette'te OLMAYAN karo id'leri: {string.Join(", ", missing)}");
            if (mismatch.Count > 0)
                Debug.LogError($"[Karo] Yurunurlugu KATALOGLA UYUSMAYAN karolar: {string.Join(", ", mismatch)}");
            if (missing.Count == 0 && mismatch.Count == 0)
                Debug.Log($"[Karo] Dogrulama TAMAM — seed {seed}: {res.Land} kara karo, " +
                          $"yurunur %{res.WalkablePct:F1}, erisilebilir %{res.ReachablePct:F1}, " +
                          $"sinir dekoru {res.Fringe}. Tum id'ler palette'te.");
        }

        /// <summary>Bu prefab ARAÇ TARAFINDAN üretilmiş mi? (yani üzerine yazmak serbest mi)
        ///
        /// Ayrım şu: `Assets/Prefabs/Grid/…` altındakiler her zaman kod tarafından üretildi
        /// (eski beyaz placeholder karolar dahil) — bunlar EZİLİR, çünkü kullanıcının şikâyeti tam
        /// da "her karo beyaz" idi. Tasarımcının kendi FBX'inden gelen karolar
        /// `Assets/Art/Models/Tiles/…` altında durur (bkz Docs/TILE_PIPELINE.md) ve ASLA ezilmez.</summary>
        private static bool IsGenerated(Object prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/Prefabs/Grid/");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
