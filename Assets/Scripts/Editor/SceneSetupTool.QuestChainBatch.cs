using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un ZORUNLU GÖREV ZİNCİRİ batch girişi (2026-08-28):
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupQuestChainBatch -logFile log.txt
    /// </code>
    ///
    /// DAR KAPSAMLI OLMASI KASITLI: tam kurulum zinciri sahneyi baştan kuruyor ve fazlar birbirinin
    /// bileşenini yeniden yaratabildiği için referans bozma riski taşıyor (2026-08-17'de yaşandı).
    /// Buradaki iş yalnızca EKLEMEK: ayar asset'ini üret, üç bileşeni sahnedeki mevcut düğüm
    /// yöneticisinin nesnesine tak, alanları bağla. Hiçbir şey yok edilmez.
    /// </summary>
    public static partial class SceneSetupTool
    {
        /// <summary>
        /// Aynı işin MENÜ girişi — Unity AÇIKKEN kullanmak için. Batch modu proje kilidini
        /// gerektiriyor, editör açıkken çalışamıyor; kapatıp açmak yerine buradan koşturulur.
        /// AÇIK SAHNE üzerinde çalışır (sahneyi yeniden AÇMAZ) → kaydetmek kullanıcıya kalır.
        /// </summary>
        [MenuItem("TacticalRPG/Bolum - Zorunlu Gorev Zincirini Kur", false, 27)]
        public static void SetupQuestChainMenu()
        {
            int n = ApplyQuestChain();
            EditorUtility.DisplayDialog("Zorunlu Gorev Zinciri",
                n > 0 ? "Zincir kuruldu: ayar asset'i, iki animasyon, yonetici, ust bar ve altin " +
                        "'gorev_tamam' karosu hazir.\n\nSAHNEYI KAYDET (Ctrl+S) — degisiklikler " +
                        "acik sahnede duruyor."
                      : "Kurulamadi: sahnede ChapterNodeManager bulunamadi. Once TAM KURULUM.",
                "Tamam");
        }

        public static void SetupQuestChainBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Gorev] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (ApplyQuestChain() == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Ortak gövde: menü de batch de bunu çağırır. 0 = kurulamadı.</summary>
        private static int ApplyQuestChain()
        {
            var nodes = FindComponentAnywhere<ChapterNodeManager>();
            if (nodes == null)
            {
                Debug.LogError("[Gorev] Sahnede ChapterNodeManager yok — once TAM KURULUM kosturulmali.");
                return 0;
            }

            var grid   = FindComponentAnywhere<HexGridManager>();
            var gen    = FindComponentAnywhere<ChapterMapGenerator>();
            var player = FindComponentAnywhere<PlayerController>();
            var state  = FindComponentAnywhere<GameStateManager>();
            var run    = FindComponentAnywhere<ChapterRunManager>();

            _silentSetup = true;
            try
            {
                // SIRA ONEMLI: once palet girisi ("gorev_tamam"), sonra modeli. Model atayici
                // paleti ID ile tariyor — giris yoksa modeli atayacak yer bulamaz.
                EnsureTerrainPaletteEntries(grid);
                AssignTerrainTileModels(force: false);   // DOLU girisleri EZMEZ
                SetupMandatoryQuestChain(nodes.gameObject, grid, gen, player, state, nodes, run);
            }
            finally { _silentSetup = false; }

            // Bagliligi DOGRULA: kurulum sessizce yarim kalip "tamam" demesin.
            var director = nodes.GetComponent<MandatoryQuestDirector>();
            var bar      = nodes.GetComponent<TacticalRPG.UI.MandatoryQuestBarHUD>();
            var fallFx   = nodes.GetComponent<MandatoryQuestFallEffect>();
            var clearFx  = nodes.GetComponent<MandatoryQuestClearEffect>();
            Debug.Log($"[Gorev] DOGRULAMA — yonetici:{(director != null)} bar:{(bar != null)} " +
                      $"dususFx:{(fallFx != null)} muhurFx:{(clearFx != null)} " +
                      $"ayar:{(director != null && director.Config != null)}");

            // Bitmis gorev karosu GERCEKTEN savas disi mi? Bayrak acik kalirsa bitmis goreve
            // tekrar girilebilir — sessizce gecmesin.
            TilePaletteSO pal = grid != null ? grid.TilePalette : null;
            bool sealedOk = false, hasModel = false;
            if (pal != null && pal.tiles != null)
                foreach (var t in pal.tiles)
                    if (t.id == ChapterNodeManager.ClearedMandatoryTileId)
                    {
                        sealedOk = !t.canEnterCombat && t.isWalkable;
                        hasModel = t.prefab != null;
                        break;
                    }
            Debug.Log($"[Gorev] DOGRULAMA — '{ChapterNodeManager.ClearedMandatoryTileId}' " +
                      $"savas-disi+yurunur:{sealedOk} model:{hasModel}");
            return 1;
        }
    }
}
