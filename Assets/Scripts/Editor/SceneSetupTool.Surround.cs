using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// HARİTA ÇEVRESİ KURULUMU — "hiçbir yönde boşluk görünmesin" (kullanıcı isteği 2026-08-12).
    ///
    /// İki profil asset'i üretir (okyanus / sonsuz orman) ve <see cref="MapSurroundBuilder"/>
    /// bileşenini sahneye bağlar. Değerler burada BİR KEZ yazılır; asset zaten varsa EZİLMEZ
    /// (CLAUDE.md §9.1 idempotentlik kuralı) — Efe renkleri/yoğunluğu Inspector'dan ayarlayınca
    /// TAM KURULUM onları silmez.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string OceanProfilePath  = "Assets/Data/Config/Surround_Okyanus.asset";
        private const string ForestProfilePath = "Assets/Data/Config/Surround_Orman.asset";

        [MenuItem("TacticalRPG/Harita Cevresi - Sonsuz Deniz + Orman Kur", false, 31)]
        public static void SetupMapSurroundMenu()
        {
            SetupMapSurround();
            EditorUtility.DisplayDialog("Harita Cevresi",
                "Haritanin disi dolduruldu:\n\n" +
                "  • Overworld -> sonsuz okyanus + sis/kaya obekleri\n" +
                "  • Savas arenasi -> sonsuz orman (kanopi halkasi)\n\n" +
                "Ucu birden collider'siz ve hucre degil: tiklanamaz, yurunmez, sise girmez.", "Tamam");
        }

        public static void SetupMapSurround()
        {
            var grid = FindComponentAnywhere<HexGridManager>();
            if (grid == null)
            {
                Debug.LogError("[Cevre] HexGridManager bulunamadi — once TAM KURULUM.");
                return;
            }

            MapSurroundProfileSO ocean  = EnsureProfile(OceanProfilePath,  ocean: true);
            MapSurroundProfileSO forest = EnsureProfile(ForestProfilePath, ocean: false);

            // Bileşen grid'in yanında dursun — harita neredeyse çevresi de orada.
            var builder = grid.GetComponent<MapSurroundBuilder>();
            if (builder == null) builder = grid.gameObject.AddComponent<MapSurroundBuilder>();

            var so = new SerializedObject(builder);
            so.FindProperty("_grid").objectReferenceValue              = grid;
            so.FindProperty("_state").objectReferenceValue             = FindComponentAnywhere<GameStateManager>();
            so.FindProperty("_overworldProfile").objectReferenceValue  = ocean;
            so.FindProperty("_combatProfile").objectReferenceValue     = forest;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(builder);

            // Editörde de GÖRÜNSÜN. Çevre mevcut hücrelerden türediği için hücre sözlüğü şart;
            // Play'e basılmadıysa (Awake koşmadı) sözlük boştur → burada bir kez kurulur.
            // Ürettiği karolar sahnedekilerin aynısıdır (aynı harita asset'i), yani içerik değişmez.
            // Çevre mesh'leri HideFlags.DontSave taşır → sahne dosyasına yazılmaz, yalnız önizleme.
            if (!grid.HasCells) grid.GenerateGrid();
            builder.Rebuild(force: true);

            Debug.Log("[Cevre] Harita cevresi kuruldu (okyanus + orman profilleri bagli).");
        }

        /// <summary>Profil asset'i yoksa üretir ve varsayılan değerleri yazar. VARSA dokunmaz.</summary>
        private static MapSurroundProfileSO EnsureProfile(string path, bool ocean)
        {
            var existing = AssetDatabase.LoadAssetAtPath<MapSurroundProfileSO>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var p = ScriptableObject.CreateInstance<MapSurroundProfileSO>();

            if (ocean)
            {
                // OKYANUS — kıtanın dışı. Renkler TileCatalog'un derin su/sis karolarından türetildi
                // ki bandın iç kenarı haritanın kendi kıyı karolarıyla aynı dili konuşsun.
                p.displayName     = "Okyanus";
                p.planeColor      = new Color(0.09f, 0.20f, 0.34f);
                p.planeMargin     = 320f;
                p.planeHeight     = 0f;
                p.planeSmoothness = 0.55f;

                p.bandRings      = 6;
                p.bandColorNear  = new Color(0.13f, 0.29f, 0.49f);   // derin su
                p.bandColorFar   = new Color(0.09f, 0.20f, 0.34f);   // düzleme doğru
                p.heightJitter   = 0.03f;
                p.tileScale      = 1f;

                // Seyrek: açık deniz kalabalık olmamalı — uzak kayalıklar ve sis öbekleri.
                p.propMargin  = 70f;
                p.propSpacing = 5.5f;
                p.propChance  = 0.22f;
                p.propLimit   = 500;
                p.propColor   = new Color(0.38f, 0.45f, 0.53f);
                p.propWidth   = new Vector2(0.9f, 2.4f);
                p.propHeight  = new Vector2(0.4f, 1.3f);             // basık = kaya/sis, ağaç değil
                p.propKeepOut = 2;

                // Overworld SİSLİ açılır: keşfedilmemiş kara neredeyse siyahtır (0.05). Deniz çok
                // parlak olursa sisli kıtanın önüne geçer — göz denize kayar. 0.55 = "orada, ama
                // uzakta". Tek sayı; Efe oyuna bakarak profil asset'inden ayarlayabilir.
                p.brightness = 0.55f;
            }
            else
            {
                // SONSUZ ORMAN — arenanın dışı. Arena 10×8; çevresi kapalı kanopi olmalı ki
                // "burası ormandaki bir açıklık" okunsun, "tahta burada bitti" değil.
                p.displayName     = "Sonsuz Orman";
                p.planeColor      = new Color(0.12f, 0.17f, 0.11f);
                p.planeMargin     = 240f;
                p.planeHeight     = 0f;
                p.planeSmoothness = 0.05f;

                p.bandRings      = 7;
                p.bandColorNear  = new Color(0.19f, 0.27f, 0.15f);   // orman zemini
                p.bandColorFar   = new Color(0.12f, 0.17f, 0.11f);
                p.heightJitter   = 0.05f;
                p.tileScale      = 1f;

                // Sık: ağaç duvarı. Arena küçük olduğu için sınır bir karo ötede başlıyor.
                p.propMargin  = 48f;
                p.propSpacing = 2.0f;
                p.propChance  = 0.72f;
                p.propLimit   = 1600;
                p.propColor   = new Color(0.13f, 0.27f, 0.16f);
                p.propWidth   = new Vector2(0.9f, 1.9f);
                p.propHeight  = new Vector2(2.0f, 4.4f);             // yüksek = ağaç
                p.propKeepOut = 1;

                p.brightness = 0.85f;
            }

            AssetDatabase.CreateAsset(p, path);
            return p;
        }
    }
}
