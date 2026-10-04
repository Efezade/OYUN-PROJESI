using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// MİNYATÜR efekti (2026-10-04) kurulumu: ana kameraya <see cref="MiniatureEffect"/> ekler/bağlar,
    /// <see cref="DisplaySettings"/>'e iletir ve AYARLAR → GÖRÜNTÜ'ye "MİNYATÜR  AÇ / KAPA" satırı koyar.
    ///
    /// TAM KURULUM zincirinde: <c>PopulateSettingsScreen</c> → <see cref="EnsureMiniatureEffect"/> +
    /// satır (VSYNC'in altına). Buradaki menü/batch yalnız "tam kurulum koşturmadan canlı sahne güncel
    /// olsun" kestirmesi — Unity KAPALIYKEN:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupMiniatureBatch -logFile log.txt
    /// </code>
    /// Idempotent: bileşen/satır varsa yeniden yaratmaz, yalnız bağları tazeler.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string MiniatureShaderPath = "Assets/Shaders/MiniatureTiltShift.shader";
        private const string MiniatureRowLabel   = "MİNYATÜR";
        private const float  SettingsRowStep     = 66f; // CreateButtonRow/CreateSliderRow satır adımı

        [MenuItem("TacticalRPG/Gorsel - Minyatur Efektini Kur (overworld tilt-shift)", false, 33)]
        public static void SetupMiniatureMenu()
        {
            bool ok = ApplyMiniatureSetup();
            EditorUtility.DisplayDialog("Minyatur Efekti",
                ok ? "Kuruldu: ana kamerada MiniatureEffect + AYARLAR > GORUNTU > MINYATUR satiri.\n\n" +
                     "SAHNEYI KAYDET (Ctrl+S)."
                   : "Kurulamadi: ana kamera bulunamadi (Console'a bak).",
                "Tamam");
        }

        public static void SetupMiniatureBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Minyatur] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (!ApplyMiniatureSetup()) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Ortak gövde (menü + batch). false = ana kamera yok.</summary>
        private static bool ApplyMiniatureSetup()
        {
            MiniatureEffect fx = EnsureMiniatureEffect();
            if (fx == null) return false;

            var display = Object.FindFirstObjectByType<DisplaySettings>(FindObjectsInactive.Include);
            if (display != null)
            {
                var dso = new SerializedObject(display);
                dso.FindProperty("_miniature").objectReferenceValue = fx;
                dso.ApplyModifiedProperties();
            }

            var ctrl = Object.FindFirstObjectByType<SettingsController>(FindObjectsInactive.Include);
            bool rowOk = ctrl != null && EnsureMiniatureSettingsRow(ctrl);

            Debug.Log($"[Minyatur] DOGRULAMA — efekt:{fx.gameObject.name} display:{display != null} " +
                      $"ayarlar:{ctrl != null} satir:{rowOk} shaderHatasiz:{CompileMiniatureShader()}");
            return true;
        }

        /// <summary>Shader'ın 3 geçişini şimdi derler (içe aktarma derlemeyi Play'e bırakır) ve
        /// hataları Console'a yazar — batch'te Unity açmadan doğrulanabilsin.</summary>
        private static bool CompileMiniatureShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(MiniatureShaderPath);
            if (shader == null) return false;
            var mat = new Material(shader);
            for (int p = 0; p < mat.passCount; p++) ShaderUtil.CompilePass(mat, p, true);
            Object.DestroyImmediate(mat);
            foreach (var msg in ShaderUtil.GetShaderMessages(shader))
                Debug.Log($"[Minyatur] shader {msg.severity}: {msg.message} (satir {msg.line})");
            return !ShaderUtil.ShaderHasError(shader);
        }

        /// <summary>Ana kamerada MiniatureEffect'i bulur/ekler ve bağlarını (durum, odak, shader) tazeler.</summary>
        private static MiniatureEffect EnsureMiniatureEffect()
        {
            var fx = Object.FindFirstObjectByType<MiniatureEffect>(FindObjectsInactive.Include);
            if (fx == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[Minyatur] MainCamera etiketli kamera yok — efekt eklenemedi.");
                    return null;
                }
                fx = cam.gameObject.AddComponent<MiniatureEffect>();
            }

            // Odak = kameranın takip ettiği hedef (Kam / oyuncu jetonu) — ekranın ortası.
            Transform focus = null;
            var follow = fx.GetComponent<CameraFollow>();
            if (follow != null)
                focus = new SerializedObject(follow).FindProperty("_target").objectReferenceValue as Transform;
            if (focus == null)
            {
                var player = FindComponentAnywhere<PlayerController>();
                if (player != null) focus = player.transform;
            }

            var so = new SerializedObject(fx);
            so.FindProperty("_camera").objectReferenceValue      = fx.GetComponent<Camera>();
            so.FindProperty("_state").objectReferenceValue       = FindComponentAnywhere<GameStateManager>();
            so.FindProperty("_focusTarget").objectReferenceValue = focus;
            so.FindProperty("_shader").objectReferenceValue      = AssetDatabase.LoadAssetAtPath<Shader>(MiniatureShaderPath);
            so.ApplyModifiedProperties();
            return fx;
        }

        /// <summary>Var olan AYARLAR paneline VSYNC'in altına MİNYATÜR satırını ekler (alttaki satırları
        /// bir satır aşağı kaydırır) ve controller'a bağlar. Satır zaten varsa yalnız bağlar.</summary>
        private static bool EnsureMiniatureSettingsRow(SettingsController ctrl)
        {
            Transform t = ctrl.transform;
            Transform existingVal = t.Find("Val_" + MiniatureRowLabel);
            Transform existingBtn = t.Find("Btn_" + MiniatureRowLabel);

            TextMeshProUGUI val;
            UnityEngine.UI.Button btn;
            if (existingVal != null && existingBtn != null)
            {
                val = existingVal.GetComponent<TextMeshProUGUI>();
                btn = existingBtn.GetComponent<UnityEngine.UI.Button>();
            }
            else
            {
                var vsyncLbl = t.Find("Lbl_VSYNC") as RectTransform;
                if (vsyncLbl == null)
                {
                    Debug.LogError("[Minyatur] AYARLAR panelinde Lbl_VSYNC yok — satir eklenemedi.");
                    return false;
                }
                float vsyncY = vsyncLbl.anchoredPosition.y;

                // VSYNC satırının ALTINDAKİ üst-ankrajlı her şey bir satır aşağı (alt ankrajlı ipucu hariç).
                foreach (Transform child in t)
                {
                    if (!(child is RectTransform rt) || rt.anchorMin.y < 0.99f) continue;
                    if (rt.anchoredPosition.y < vsyncY - 30f)
                        rt.anchoredPosition += new Vector2(0f, -SettingsRowStep);
                }

                float y = vsyncY - SettingsRowStep;
                btn = CreateButtonRow(t, MiniatureRowLabel, "AÇ / KAPA", ref y, out val);
            }

            var cso = new SerializedObject(ctrl);
            cso.FindProperty("_miniatureValue").objectReferenceValue = val;
            cso.ApplyModifiedProperties();

            // Persistent listener tekrar eklenmesin (idempotent).
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, ctrl.OnToggleMiniature);
            return true;
        }
    }
}
