using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un BATCH girişi — menü ekranını ve dükkânı Unity'yi elle açmadan yeniden kurar:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.RebuildUiAndStoreBatch -logFile log.txt
    /// </code>
    ///
    /// Neden ikisi BİRLİKTE ve BU SIRAYLA: <see cref="SetupStore"/> PlayerBuffs'ı yok edip yeniden
    /// ekliyor ve sonunda harita ekranının ona olan referansını tazeliyor. Ters sırada koşarsa
    /// harita ekranı yok edilmiş bir bileşene işaret eder (2026-08-17'de yaşandı).
    ///
    /// Sahne AÇILIR ve KAYDEDİLİR: batch modda kaydedilmeyen sahne değişiklikleri -quit ile yok olur.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string BatchScenePath = "Assets/Scenes/xd.unity";

        public static void RebuildUiAndStoreBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[UI/Store] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            _silentSetup = true;
            try
            {
                SetupUIShell();   // menu kabugu + harita ekrani (+ seyahat gosterisi, seyahat kuresi)
                SetupStore();     // dukkan katalogu (Yol Tasi cikarildi) + PlayerBuffs tazeleme
            }
            finally { _silentSetup = false; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI/Store] Menu ekrani ve dukkan yeniden kuruldu, sahne kaydedildi.");
        }
    }
}
