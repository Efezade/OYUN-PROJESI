using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// KAM'IN DAVUL KAROLARININ MODELLERİ — 24 karo, TEK RENK TEMASI.
    ///
    /// Kullanıcı isteği (2026-08-12): "karolara özel modeller ekle, özellikleriyle ilgili
    /// özelliklerine çok benzesinler ve genel olarak tek renk temasından oluşsunlar".
    ///
    /// TASARIM KURALI — BİÇİM ANLATIR, RENK BİRLEŞTİRİR:
    ///   • Zemin ve taş: kara bazalt (<see cref="AugRock"/>) — 24 karoda AYNI.
    ///   • Oyma/parıltı: ruh teali (<see cref="AugAccent"/>) — 24 karoda AYNI, emisyonlu.
    ///   • Ayrım tamamen SİLUETTEN gelir: ocak bir ateş çukuru, tuzak dişli bir halka, kapı bir
    ///     kemer, fıçı bir fıçı. Oyuncu karoyu uzaktan biçiminden tanır; renk yalnız "bu Kam'ın
    ///     koyduğu bir ruh karosu" der. Eskiden karolar arazi görsellerini ödünç alıyordu
    ///     (dikilitaş, bataklık, sıcak kaynak) — ne tanınıyor ne de bir takım gibi duruyorlardı.
    ///
    /// Parlayan parçalar <see cref="AugmentTileVisual.AccentPrefix"/> ("Accent") ile adlandırılır;
    /// çalışma zamanında nabız/çakış tam olarak o parçalara uygulanır.
    ///
    /// Bu dosya <see cref="TileVisualFactory"/>'nin partial parçasıdır → Prim/Cone/NewBase/
    /// WritePalette gibi karo üretim sözlüğünü paylaşır, kopyalamaz.
    /// </summary>
    public static partial class TileVisualFactory
    {
        private const string AugPrefabFolder = "Assets/Prefabs/Grid/Augments";

        // ── Tema ─────────────────────────────────────────────────────────────
        private static readonly Color AugStoneColor  = new(0.19f, 0.21f, 0.24f);
        private static readonly Color AugStoneDeep   = new(0.09f, 0.10f, 0.12f);
        private static readonly Color AugAccentColor = new(0.30f, 0.85f, 0.78f);   // ruh teali

        private static Material AugRock     => SolidMat("AugRock",  AugStoneColor, 0.10f);
        private static Material AugRockDeep => SolidMat("AugDeep",  AugStoneDeep,  0.04f);
        private static Material AugAccent   => AugEmissiveMat();

        // ═════════════════════════════════════════════════════════════════════
        //  Menü / batch
        // ═════════════════════════════════════════════════════════════════════

        [MenuItem("TacticalRPG/Karo/Davul Karolarini Kur (Kam'in koydugu 24 karo)", false, 30)]
        public static void BuildAugmentsMenu()
        {
            int n = BuildAugments(force: true);
            EditorUtility.DisplayDialog("Davul Karolari",
                $"{n} davul karosu uretildi ve palete baglandi.\n\n" +
                "Savasta Kam davulu vurup karo koydugunda artik kendi modeli cikar,\n" +
                "yerden yukselerek oturur ve oymalari nabiz gibi parlar.", "Tamam");
        }

        /// <summary>Batch girişi: <c>-executeMethod TacticalRPG.Editor.TileVisualFactory.BuildAugmentsBatch</c></summary>
        public static void BuildAugmentsBatch() => BuildAugments(force: true);

        /// <summary>24 davul karosunun prefabını üretir ve palete yazar. Üretilen sayıyı döner.</summary>
        public static int BuildAugments(bool force)
        {
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>("Assets/Data/Map/TilePalette.asset");
            if (palette == null) { Debug.LogError("[Davul Karo] TilePalette.asset bulunamadi."); return 0; }

            EnsureFolder(MatFolder);
            EnsureFolder(TexFolder);
            EnsureFolder(AugPrefabFolder);

            // Zemin dokusu (Stone) TileVisualFactory'nin ürettiği gri tonlu doku ile aynı —
            // davul karoları arenanın geri kalanıyla aynı malzeme dilinde konuşsun diye.
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(Surface.Stone)) == null)
            {
                if (WriteSurfaceTexture(Surface.Stone, true)) { AssetDatabase.Refresh(); ConfigureSurfaceTexture(Surface.Stone); }
            }

            var built = new List<(TileCatalog.Entry entry, GameObject prefab)>();
            int i = 0;
            foreach (var e in TileCatalog.All)
            {
                if (e.Family != TileFamily.Augment) continue;
                EditorUtility.DisplayProgressBar("Davul karolari", e.Name, i++ / 24f);

                GameObject prefab = EnsureAugmentPrefab(e, force);
                if (prefab != null) built.Add((e, prefab));
            }
            EditorUtility.ClearProgressBar();

            int touched = WritePalette(palette, built, force);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Davul Karo] {built.Count} karo prefabi hazir, palette {touched} giris yazildi.");
            VerifyAugments(palette);
            return built.Count;
        }

        /// <summary>Kart havuzundaki HER kartın görselinin gerçekten palette olduğunu doğrular.
        /// Bir kart eksik kalsaydı savaşta o karo görünmez olurdu (sessiz kırılma).</summary>
        private static void VerifyAugments(TilePaletteSO palette)
        {
            var missing = new List<string>();
            foreach (var card in AugmentCatalog.All)
            {
                if (TileCatalog.Get(card.VisualId) == null) { missing.Add($"{card.Id} → katalogda yok: {card.VisualId}"); continue; }
                if (palette.GetById(card.VisualId) == null) missing.Add($"{card.Id} → palette yok: {card.VisualId}");
            }
            if (missing.Count > 0) Debug.LogError("[Davul Karo] EKSIK: " + string.Join(" | ", missing));
            else Debug.Log($"[Davul Karo] Dogrulama TAMAM — {AugmentCatalog.All.Length} kartin gorseli palette bagli.");

            // "Tek karoluk etki olmasin" kurali kod tarafindan korunur.
            var rule = AugmentCatalog.Validate();
            if (rule.Count > 0) Debug.LogError("[Davul Karo] YARICAP KURALI IHLALI: " + string.Join(" | ", rule));
            else
            {
                int r1 = 0, r2 = 0, terrain = 0;
                foreach (var e in AugmentCatalog.All)
                {
                    if (e.IsTerrain)      terrain++;
                    else if (e.Radius == 1) r1++;
                    else if (e.Radius == 2) r2++;
                }
                Debug.Log($"[Davul Karo] Yaricap TAMAM — {r1} kart 3 karo capi, {r2} kart 5 karo capi, " +
                          $"{terrain} arazi karti (3 karo orer). Tek karoluk etki: 0.");
            }
        }

        private static GameObject EnsureAugmentPrefab(TileCatalog.Entry e, bool force)
        {
            string path = $"{AugPrefabFolder}/Aug_{e.Id}.prefab";
            if (!force)
            {
                var have = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (have != null) return have;
            }

            GameObject root = NewBase(e, force);
            if (root == null) return null;
            root.name = $"Aug_{e.Id}";

            BuildAugmentModel(root.transform, e.Id);

            // Yerleşme animasyonu + nabız bileşeni prefabın parçası olur → karo nereden
            // üretilirse üretilsin (draft, kurulum, test) canlanır.
            root.AddComponent<AugmentTileVisual>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Modeller — her karo kendi özelliğine benzer
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildAugmentModel(Transform t, string id)
        {
            var rnd = new System.Random(id.GetHashCode());

            switch (id)
            {
                // ── KUT ──────────────────────────────────────────────────────
                case TileCatalog.AugAtaTasi:       // sırada ÖNE geçer → ileri bakan ok uçları
                    ACube(t, new Vector3(0f, Y0 + 0.34f, -0.22f), new Vector3(0.56f, 0.68f, 0.15f),
                          new Vector3(12f, 0f, 0f), AugRock, "Slab");
                    AChevron(t, 0.30f, 0.10f);
                    AChevron(t, 0.05f, 0.10f);
                    break;

                case TileCatalog.AugKalkanTasi:    // savunma → kubbe kalkan
                    APrim(t, PrimitiveType.Sphere, new Vector3(0f, Y0 - 0.10f, 0f),
                          new Vector3(0.98f, 0.62f, 0.98f), Vector3.zero, AugRock, "Dome");
                    ARing(t, 0.40f, Y0 + 0.16f, 10, new Vector3(0.10f, 0.05f, 0.05f), true, "Band");
                    ACube(t, new Vector3(0f, Y0 + 0.22f, 0f), new Vector3(0.10f, 0.05f, 0.30f),
                          Vector3.zero, AugAccent, "Accent_Boss");
                    break;

                case TileCatalog.AugRuzgarTasi:    // hareket → eğik kanatlar + hız çizgileri
                    for (int i = 0; i < 3; i++)
                    {
                        float a = -35f + i * 35f;
                        ACube(t, new Vector3(-0.22f + i * 0.22f, Y0 + 0.26f, 0f),
                              new Vector3(0.06f, 0.50f, 0.34f), new Vector3(0f, a, 22f), AugRock, $"Fin{i}");
                    }
                    for (int i = 0; i < 3; i++)
                        ABar(t, new Vector2(-0.52f, -0.26f + i * 0.26f), new Vector2(0.52f, -0.26f + i * 0.26f),
                             0.05f, Y0 + 0.02f, true, $"Gust{i}");
                    break;

                case TileCatalog.AugOcak:          // can yeniler → taş çemberli ateş çukuru
                    ARing(t, 0.44f, Y0 + 0.06f, 7, new Vector3(0.17f, 0.16f, 0.14f), false, "Stone");
                    ADisc(t, Y0 + 0.01f, 0.62f, 0.03f, true, "Embers");
                    ACone(t, new Vector3(0f, Y0, 0f), 0.30f, 0.58f, AugAccent, "Accent_Flame");
                    ACone(t, new Vector3(0.10f, Y0, 0.06f), 0.16f, 0.34f, AugAccent, "Accent_Flame2");
                    break;

                case TileCatalog.AugOfkeTasi:      // hasar → yukarı fırlayan keskin dikenler
                    ADisc(t, Y0, 0.78f, 0.05f, false, "Base");
                    ACone(t, new Vector3(0f, Y0, 0f), 0.22f, 0.62f, AugAccent, "Accent_Spike0");
                    ACone(t, new Vector3(-0.26f, Y0, 0.14f), 0.16f, 0.42f, AugAccent, "Accent_Spike1");
                    ACone(t, new Vector3(0.24f, Y0, -0.16f), 0.16f, 0.38f, AugAccent, "Accent_Spike2");
                    break;

                // ── KARGIŞ ───────────────────────────────────────────────────
                case TileCatalog.AugTuzakTasi:     // sersemletir → kapanan dişli çene
                    ADisc(t, Y0 - 0.04f, 0.80f, 0.06f, false, "Pit");
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * 0.36f, Y0 - 0.02f, Mathf.Sin(a) * 0.36f);
                        ACone(t, p, 0.11f, 0.26f, AugAccent, $"Accent_Tooth{i}",
                              new Vector3(Mathf.Sin(a) * 26f, 0f, -Mathf.Cos(a) * 26f));
                    }
                    break;

                case TileCatalog.AugCamur:         // hareketi yavaşlatır → çökmüş balçık + kabarcık
                    ADisc(t, Y0 - 0.09f, 0.92f, 0.07f, false, "Sink");
                    for (int i = 0; i < 4; i++)
                    {
                        float s = Rf(rnd, 0.12f, 0.22f);
                        APrim(t, PrimitiveType.Sphere, Spot(rnd, 0.40f, Y0 - 0.04f),
                              new Vector3(s, s * 0.55f, s), Vector3.zero, AugAccent, $"Accent_Bubble{i}");
                    }
                    break;

                case TileCatalog.AugKorkuSisi:     // isabeti düşürür → alçak sis kümeleri
                    for (int i = 0; i < 5; i++)
                    {
                        float s = Rf(rnd, 0.30f, 0.52f);
                        APrim(t, PrimitiveType.Sphere, Spot(rnd, 0.38f, Y0 + 0.10f),
                              new Vector3(s, s * 0.32f, s), Vector3.zero, AugAccent, $"Accent_Mist{i}");
                    }
                    break;

                case TileCatalog.AugDiken:         // girişte hasar → tarla dolusu diken
                    ADisc(t, Y0, 0.80f, 0.04f, false, "Bed");
                    for (int i = 0; i < 11; i++)
                        ACone(t, Spot(rnd, 0.56f, Y0), Rf(rnd, 0.07f, 0.12f), Rf(rnd, 0.18f, 0.34f),
                              AugAccent, $"Accent_Thorn{i}");
                    break;

                case TileCatalog.AugAgirlik:       // sırada geriye düşürür → bastıran ağır blok
                    ACube(t, new Vector3(0f, Y0 + 0.17f, 0f), new Vector3(0.74f, 0.34f, 0.58f),
                          Vector3.zero, AugRock, "Anvil");
                    ACube(t, new Vector3(0f, Y0 + 0.42f, 0f), new Vector3(0.46f, 0.20f, 0.38f),
                          Vector3.zero, AugRock, "AnvilTop");
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i / 4f * Mathf.PI * 2f + 0.4f;
                        ABar(t, new Vector2(Mathf.Cos(a) * 0.30f, Mathf.Sin(a) * 0.30f),
                                new Vector2(Mathf.Cos(a) * 0.66f, Mathf.Sin(a) * 0.66f),
                             0.05f, Y0 + 0.01f, true, $"Crack{i}");
                    }
                    break;

                // ── NÖTR ─────────────────────────────────────────────────────
                case TileCatalog.AugSarsinti:      // herkese −savunma → yer yarığı hattı
                    ABar(t, new Vector2(-0.60f, -0.30f), new Vector2(-0.12f, 0.06f), 0.11f, Y0 + 0.01f, true, "Fissure0");
                    ABar(t, new Vector2(-0.12f, 0.06f), new Vector2(0.20f, -0.16f), 0.11f, Y0 + 0.01f, true, "Fissure1");
                    ABar(t, new Vector2(0.20f, -0.16f), new Vector2(0.62f, 0.22f), 0.11f, Y0 + 0.01f, true, "Fissure2");
                    ACube(t, new Vector3(-0.34f, Y0 + 0.10f, 0.34f), new Vector3(0.34f, 0.20f, 0.26f),
                          new Vector3(0f, 24f, 14f), AugRock, "Shard0");
                    ACube(t, new Vector3(0.38f, Y0 + 0.09f, -0.36f), new Vector3(0.30f, 0.18f, 0.24f),
                          new Vector3(0f, -18f, -12f), AugRock, "Shard1");
                    break;

                case TileCatalog.AugRuhKapisi:     // +1 aksiyon → ruh kapısı kemeri
                    ACube(t, new Vector3(-0.34f, Y0 + 0.38f, 0f), new Vector3(0.15f, 0.76f, 0.18f),
                          Vector3.zero, AugRock, "PillarL");
                    ACube(t, new Vector3(0.34f, Y0 + 0.38f, 0f), new Vector3(0.15f, 0.76f, 0.18f),
                          Vector3.zero, AugRock, "PillarR");
                    ACube(t, new Vector3(0f, Y0 + 0.82f, 0f), new Vector3(0.88f, 0.15f, 0.20f),
                          Vector3.zero, AugRock, "Lintel");
                    ACube(t, new Vector3(0f, Y0 + 0.38f, 0f), new Vector3(0.52f, 0.72f, 0.03f),
                          Vector3.zero, AugAccent, "Accent_Veil");
                    break;

                case TileCatalog.AugDuvar:         // geçilemez → yüksek taş duvar
                    ACube(t, new Vector3(0f, Y0 + 0.40f, 0f), new Vector3(1.06f, 0.80f, 0.46f),
                          Vector3.zero, AugRock, "Wall");
                    ACube(t, new Vector3(0f, Y0 + 0.84f, 0f), new Vector3(1.10f, 0.10f, 0.52f),
                          Vector3.zero, AugRock, "Cap");
                    ABar(t, new Vector2(-0.52f, 0f), new Vector2(0.52f, 0f), 0.04f, Y0 + 0.52f, true, "Seam0");
                    ABar(t, new Vector2(-0.52f, 0f), new Vector2(0.52f, 0f), 0.04f, Y0 + 0.22f, true, "Seam1");
                    break;

                case TileCatalog.AugBosluk:        // uçurum → çöken kara delik + parlayan ağız
                    ADiscMat(t, Y0 - 0.30f, 0.94f, 0.30f, AugRockDeep, "Void");
                    ARing(t, 0.47f, Y0 + 0.02f, 14, new Vector3(0.09f, 0.04f, 0.06f), true, "Rim");
                    break;

                // ── PATLAYICI ────────────────────────────────────────────────
                case TileCatalog.AugAtesFicisi:    // vurulunca patlar → fıçı
                    ACylinder(t, new Vector3(0f, Y0 + 0.30f, 0f), 0.48f, 0.60f, AugRock, "Barrel");
                    ARing(t, 0.25f, Y0 + 0.16f, 12, new Vector3(0.07f, 0.04f, 0.05f), true, "Hoop0");
                    ARing(t, 0.25f, Y0 + 0.46f, 12, new Vector3(0.07f, 0.04f, 0.05f), true, "Hoop1");
                    ADisc(t, Y0 + 0.60f, 0.40f, 0.03f, true, "Lid");
                    break;

                case TileCatalog.AugBuzKabugu:     // donduran kabuk → kırılgan billur kubbe
                    APrim(t, PrimitiveType.Sphere, new Vector3(0f, Y0 - 0.06f, 0f),
                          new Vector3(0.86f, 0.66f, 0.86f), Vector3.zero, AugRock, "Shell");
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f;
                        ACone(t, new Vector3(Mathf.Cos(a) * 0.28f, Y0 + 0.22f, Mathf.Sin(a) * 0.28f),
                              0.14f, 0.34f, AugAccent, $"Accent_Shard{i}",
                              new Vector3(Mathf.Sin(a) * -18f, 0f, Mathf.Cos(a) * 18f));
                    }
                    break;

                case TileCatalog.AugRuhBombasi:    // fitil → kaideye oturmuş ruh küresi
                    ACylinder(t, new Vector3(0f, Y0 + 0.10f, 0f), 0.40f, 0.20f, AugRock, "Pedestal");
                    APrim(t, PrimitiveType.Sphere, new Vector3(0f, Y0 + 0.44f, 0f),
                          new Vector3(0.40f, 0.40f, 0.40f), Vector3.zero, AugAccent, "Accent_Orb");
                    ARing(t, 0.32f, Y0 + 0.44f, 12, new Vector3(0.05f, 0.05f, 0.04f), true, "Halo");
                    break;

                case TileCatalog.AugCigTasi:       // moloz siperi → devrilmiş kaya yığını
                    for (int i = 0; i < 5; i++)
                    {
                        float s = Rf(rnd, 0.30f, 0.54f);
                        APrim(t, PrimitiveType.Sphere, Spot(rnd, 0.36f, Y0 + s * 0.22f),
                              new Vector3(s * 1.2f, s * 0.85f, s), new Vector3(Rf(rnd, 0, 24), Rf(rnd, 0, 360), Rf(rnd, 0, 24)),
                              AugRock, $"Boulder{i}");
                    }
                    ABar(t, new Vector2(-0.44f, -0.28f), new Vector2(0.40f, 0.30f), 0.05f, Y0 + 0.01f, true, "Dust");
                    break;

                // ── SINIFSAL ─────────────────────────────────────────────────
                case TileCatalog.AugNisanKayasi:   // +menzil → nişan almak için çentikli sütun
                    ACube(t, new Vector3(0f, Y0 + 0.26f, 0f), new Vector3(0.28f, 0.52f, 0.28f),
                          Vector3.zero, AugRock, "PillarLow");
                    ACube(t, new Vector3(0f, Y0 + 0.80f, 0f), new Vector3(0.28f, 0.30f, 0.28f),
                          Vector3.zero, AugRock, "PillarTop");
                    ACube(t, new Vector3(0f, Y0 + 0.64f, 0.10f), new Vector3(0.06f, 0.04f, 0.70f),
                          Vector3.zero, AugAccent, "Accent_Sightline");
                    break;

                case TileCatalog.AugKalkanDuvari:  // alan savunması → yan yana dizilmiş kalkanlar
                    for (int i = 0; i < 3; i++)
                        APrim(t, PrimitiveType.Sphere, new Vector3(-0.32f + i * 0.32f, Y0 + 0.16f, 0f),
                              new Vector3(0.30f, 0.44f, 0.16f), Vector3.zero, AugRock, $"Shield{i}");
                    ABar(t, new Vector2(-0.50f, 0f), new Vector2(0.50f, 0f), 0.06f, Y0 + 0.22f, true, "Band");
                    break;

                case TileCatalog.AugLeyDamari:     // büyü hattı → merkezden yayılan damarlar
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        ABar(t, Vector2.zero, new Vector2(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.62f),
                             0.05f, Y0 + 0.01f, true, $"Vein{i}");
                    }
                    APrim(t, PrimitiveType.Sphere, new Vector3(0f, Y0 + 0.08f, 0f),
                          new Vector3(0.22f, 0.16f, 0.22f), Vector3.zero, AugAccent, "Accent_Core");
                    break;

                case TileCatalog.AugKutsalZemin:   // geniş iyileşme → yere kazınmış çember mühür
                    ARing(t, 0.56f, Y0 + 0.01f, 20, new Vector3(0.07f, 0.03f, 0.05f), true, "Outer");
                    ARing(t, 0.28f, Y0 + 0.01f, 12, new Vector3(0.06f, 0.03f, 0.05f), true, "Inner");
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i / 4f * Mathf.PI * 2f + 0.78f;
                        ACube(t, new Vector3(Mathf.Cos(a) * 0.60f, Y0 + 0.16f, Mathf.Sin(a) * 0.60f),
                              new Vector3(0.12f, 0.32f, 0.12f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f),
                              AugRock, $"Menhir{i}");
                    }
                    break;

                case TileCatalog.AugGolgeYarigi:   // gölge hasarı → yerdeki karanlık yarık
                    ACube(t, new Vector3(0f, Y0 - 0.05f, 0f), new Vector3(0.26f, 0.14f, 1.00f),
                          new Vector3(0f, 22f, 0f), AugRockDeep, "Rift");
                    ABar(t, new Vector2(-0.20f, -0.46f), new Vector2(0.18f, 0.46f), 0.04f, Y0 + 0.01f, true, "EdgeL");
                    ABar(t, new Vector2(-0.02f, -0.48f), new Vector2(0.36f, 0.44f), 0.04f, Y0 + 0.01f, true, "EdgeR");
                    break;

                case TileCatalog.AugDavulTasi:     // Kam'ın manası → davul
                    ACylinder(t, new Vector3(0f, Y0 + 0.22f, 0f), 0.56f, 0.44f, AugRock, "Drum");
                    ADisc(t, Y0 + 0.44f, 0.50f, 0.03f, true, "Skin");
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        ACube(t, new Vector3(Mathf.Cos(a) * 0.29f, Y0 + 0.22f, Mathf.Sin(a) * 0.29f),
                              new Vector3(0.04f, 0.40f, 0.04f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f),
                              AugAccent, $"Accent_Lace{i}");
                    }
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Geometri yardımcıları (hepsi tek temadan besleniyor)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Emisyonlu ruh teali. Emisyon ŞART: çalışma zamanındaki nabız
        /// (<see cref="AugmentTileVisual"/>) _EmissionColor'ı sürüyor; keyword kapalıysa
        /// MaterialPropertyBlock yazsa da hiçbir şey parlamaz.</summary>
        private static Material AugEmissiveMat()
        {
            const string path = MatFolder + "/AugAccent.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            SetColor(mat, AugAccentColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", AugAccentColor * 1.6f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject APrim(Transform t, PrimitiveType type, Vector3 pos, Vector3 scale,
                                        Vector3 euler, Material mat, string name)
        {
            GameObject go = Prim(t, type, pos, scale, euler, mat);
            go.name = name;
            return go;
        }

        private static void ACube(Transform t, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, string name)
            => APrim(t, PrimitiveType.Cube, pos, scale, euler, mat, name);

        /// <summary>Silindir (Unity primitifi 1 birim çap, 2 birim yükseklik).</summary>
        private static void ACylinder(Transform t, Vector3 center, float diameter, float height,
                                      Material mat, string name)
            => APrim(t, PrimitiveType.Cylinder, center, new Vector3(diameter, height * 0.5f, diameter),
                     Vector3.zero, mat, name);

        /// <summary>Yere yatık ince disk.</summary>
        private static void ADisc(Transform t, float y, float diameter, float thickness, bool accent, string name)
            => ADiscMat(t, y, diameter, thickness, accent ? AugAccent : AugRock, accent ? "Accent_" + name : name);

        private static void ADiscMat(Transform t, float y, float diameter, float thickness, Material mat, string name)
            => APrim(t, PrimitiveType.Cylinder, new Vector3(0f, y, 0f),
                     new Vector3(diameter, thickness * 0.5f, diameter), Vector3.zero, mat, name);

        /// <summary>Koni (taban verilen noktada, tepe yukarı).</summary>
        private static void ACone(Transform t, Vector3 basePos, float width, float height,
                                  Material mat, string name, Vector3 euler = default)
        {
            GameObject go = Cone(t, basePos, new Vector3(width, height, width), mat);
            go.name = name;
            if (euler != default) go.transform.localEulerAngles = euler;
        }

        /// <summary>Çember üstüne dizilmiş küçük bloklar (bant/halka/kasnak).</summary>
        private static void ARing(Transform t, float radius, float y, int count, Vector3 size,
                                  bool accent, string tag)
        {
            Material mat = accent ? AugAccent : AugRock;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                ACube(t, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius), size,
                      new Vector3(0f, -a * Mathf.Rad2Deg, 0f), mat,
                      accent ? $"Accent_{tag}{i}" : $"{tag}{i}");
            }
        }

        /// <summary>İki nokta arasına uzanan ince çubuk (çatlak, damar, hat).</summary>
        private static void ABar(Transform t, Vector2 from, Vector2 to, float width, float y,
                                 bool accent, string name)
        {
            Vector3 a = new(from.x, y, from.y), b = new(to.x, y, to.y);
            float len = Vector3.Distance(a, b);
            if (len < 0.001f) return;
            float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;

            ACube(t, (a + b) * 0.5f, new Vector3(width, 0.05f, len), new Vector3(0f, yaw, 0f),
                  accent ? AugAccent : AugRock, accent ? $"Accent_{name}" : name);
        }

        /// <summary>İleri bakan "&gt;" işareti — Ata Taşı'nın yön duygusu.</summary>
        private static void AChevron(Transform t, float z, float width)
        {
            ABar(t, new Vector2(-0.30f, z - 0.26f), new Vector2(0f, z + 0.06f), width, Y0 + 0.02f, true, $"ChevL{z:F2}");
            ABar(t, new Vector2(0.30f, z - 0.26f),  new Vector2(0f, z + 0.06f), width, Y0 + 0.02f, true, $"ChevR{z:F2}");
        }
    }
}
