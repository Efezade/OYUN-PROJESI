using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// YÜRÜYÜŞ İPTALİ (2026-09-06) için sahne bağlantıları — Unity KAPALIYKEN koşturulur:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupWalkCancelBatch -logFile log.txt
    /// </code>
    ///
    /// DAR KAPSAMLI OLMASI KASITLI (<see cref="SetupQuestChainBatch"/> ile aynı gerekçe): iki alan
    /// bağlanacak diye tüm bölüm kurulumunu (SetupChapterWorld) koşturmak, haritayı/ayarları
    /// yeniden üreten geniş bir zinciri tetiklerdi. Buradaki iş yalnız EKLEMEK:
    ///   • <c>MapInputHandler._ap</c>  → iptal edilen yolculuğun kalan bedava hamlesi temizlensin.
    ///   • <c>ChapterRunHUD._player</c> → "SAĞ TIK: dur" şeridi çizilebilsin.
    ///
    /// Bu alanların ikisi de TAM KURULUM zincirinde (SetupChapterWorld) zaten bağlanıyor; burası
    /// yalnız "tam kurulum koşturmadan da sahne güncel olsun" kestirmesi.
    /// </summary>
    public static partial class SceneSetupTool
    {
        [MenuItem("TacticalRPG/Bolum - Yuruyus Iptali Baglantilarini Kur", false, 29)]
        public static void SetupWalkCancelMenu()
        {
            int n = ApplyWalkCancelRefs();
            EditorUtility.DisplayDialog("Yuruyus Iptali",
                n > 0 ? "Baglantilar kuruldu (MapInputHandler + ChapterRunHUD).\n\n" +
                        "SAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede MapInputHandler/ChapterRunHUD bulunamadi.",
                "Tamam");
        }

        public static void SetupWalkCancelBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Yuruyus] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (ApplyWalkCancelRefs() == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Ortak gövde (menü + batch). 0 = kurulamadı.</summary>
        private static int ApplyWalkCancelRefs()
        {
            var input  = FindComponentAnywhere<MapInputHandler>();
            var runHud = FindComponentAnywhere<TacticalRPG.UI.ChapterRunHUD>();
            var ap     = FindComponentAnywhere<ActionPointManager>();
            var player = FindComponentAnywhere<PlayerController>();

            if (input == null && runHud == null)
            {
                Debug.LogError("[Yuruyus] Sahnede MapInputHandler da ChapterRunHUD da yok — " +
                               "once TAM KURULUM.");
                return 0;
            }

            if (input != null)
            {
                var so = new SerializedObject(input);
                so.FindProperty("_ap").objectReferenceValue = ap;
                so.ApplyModifiedProperties();
            }

            if (runHud != null)
            {
                var so = new SerializedObject(runHud);
                so.FindProperty("_player").objectReferenceValue = player;
                so.ApplyModifiedProperties();
            }

            Debug.Log($"[Yuruyus] DOGRULAMA — girdi:{(input != null)} ap:{(ap != null)} " +
                      $"hud:{(runHud != null)} oyuncu:{(player != null)}");
            return 1;
        }
    }
}
