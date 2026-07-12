using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Kam'ın animasyonlu karakter FBX'lerini oyuna bağlayan boru hattı (karo
    /// "Klasörü Tara" deseninin karakter karşılığı).
    ///
    /// KULLANIM: Mixamo tarzı "model@AnimasyonAdi.fbx" dosyalarını
    /// <see cref="KamFolder"/> klasörüne at → menüden "Kam Animasyon Kurulumu"
    /// (ya da TAM KURULUM) çalıştır. Araç her dosya için:
    ///   • import ayarını yapar (Generic rig + klip adı = @ soneki + loop bayrağı),
    ///   • KamAnimator.controller'ı yeniden kurar (Idle ↔ Walk, "IsMoving" bool).
    ///
    /// Sonek eşlemesi (küçük harf İÇERİR araması):
    ///   walk/yuru → Walk (loop) · idle/bekle/dur → Idle (loop) · run/kos → Run (loop)
    ///   attack/saldiri/vur → Attack · death/die/olum → Death · diğerleri → kendi adında durum.
    /// Idle klibi yoksa Walk klibinin İLK KARESİ dondurulur (state.speed=0) — @Idle
    /// dosyası eklenince otomatik gerçek idle'a döner.
    ///
    /// Controller asset'i YERİNDE yeniden kurulur (GUID sabit) → sahnedeki Animator ve
    /// Kam.asset referansları bozulmaz; yeni animasyon ekleyince TAM KURULUM GEREKMEZ,
    /// bu menü yeter.
    /// </summary>
    public static class CharacterAnimationImporter
    {
        public const string KamFolder         = "Assets/Art/Models/Characters/Kam";
        public const string KamControllerPath = KamFolder + "/KamAnimator.controller";

        private const string IsMovingParam = "IsMoving"; // CharacterAnimationDriver ile aynı ad

        [MenuItem("TacticalRPG/Karakter - Kam Animasyon Kurulumu", false, 40)]
        public static void SetupKamMenu()
        {
            AnimatorController controller = SetupKam();
            int clipCount = controller != null ? controller.animationClips.Length : 0;

            EditorUtility.DisplayDialog(
                "Kam Animasyon Kurulumu",
                controller != null
                    ? "Kurulum tamam:\n\n" +
                      $"  • Klasör: {KamFolder}\n" +
                      $"  • Controller: {KamControllerPath}\n" +
                      $"  • Klip sayısı: {clipCount}\n\n" +
                      "Yeni animasyon eklemek için 'barbarkarakteri@AnimasyonAdi.fbx' dosyasını\n" +
                      "aynı klasöre atıp bu menüyü tekrar çalıştırman yeter.\n" +
                      "(Sahne ilk kez kuruluyorsa TAM KURULUM gerekir.)"
                    : $"Animasyonlu FBX bulunamadı!\n\n'{KamFolder}' klasörüne\n" +
                      "'barbarkarakteri@Walking.fbx' gibi @'li dosyalar atıp tekrar dene.",
                "Tamam");
        }

        /// <summary>
        /// Kam klasöründeki animasyon FBX'lerini hazırlar ve controller'ı yeniden kurar.
        /// Idempotent — TAM KURULUM (Faz 1/2) da çağırır. Animasyon yoksa null döner.
        /// </summary>
        public static AnimatorController SetupKam()
        {
            EnsureKamFolder();

            var clips = new List<(string suffix, AnimationClip clip)>();
            foreach (string path in AnimationFbxPaths())
            {
                string suffix = SuffixOf(path);
                EnsureImportSettings(path, suffix);

                AnimationClip clip = LoadMainClip(path);
                if (clip != null) clips.Add((suffix.ToLowerInvariant(), clip));
                else Debug.LogWarning($"[KamAnim] '{path}' içinde animasyon klibi bulunamadı.");
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning($"[KamAnim] '{KamFolder}' içinde @'li animasyon FBX yok — controller kurulmadı.");
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(KamControllerPath);
            }

            return BuildController(clips);
        }

        /// <summary>
        /// Kam'ın GÖRSEL model kaynağı. Öncelik: @'siz temel model FBX'i → @walk'lu FBX
        /// (mesh+rig+animasyonu birlikte taşır) → klasördeki ilk FBX. Yoksa null.
        /// </summary>
        public static GameObject FindKamModelFbx()
        {
            if (!AssetDatabase.IsValidFolder(KamFolder)) return null;

            string best = null;
            foreach (string path in AllFbxPaths())
            {
                string suffix = SuffixOf(path);
                if (suffix == null) { best = path; break; }                     // düz model dosyası en iyisi
                if (best == null || Has(suffix.ToLowerInvariant(), "walk", "yuru"))
                    best = path;                                                // yoksa yürüyüş FBX'i
            }
            return best != null ? AssetDatabase.LoadAssetAtPath<GameObject>(best) : null;
        }

        // ── Tarama yardımcıları ───────────────────────────────────────────────

        private static IEnumerable<string> AllFbxPaths() =>
            Directory.GetFiles(KamFolder, "*.fbx", SearchOption.TopDirectoryOnly)
                     .Select(p => p.Replace('\\', '/'))
                     .OrderBy(p => p);

        private static IEnumerable<string> AnimationFbxPaths() =>
            AllFbxPaths().Where(p => SuffixOf(p) != null);

        // "model@Walking.fbx" → "Walking"; @ yoksa null (animasyon dosyası değil).
        private static string SuffixOf(string path)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            int at = file.IndexOf('@');
            return at >= 0 && at < file.Length - 1 ? file.Substring(at + 1) : null;
        }

        private static bool Has(string s, params string[] keys) => keys.Any(s.Contains);

        private static bool IsLooping(string suffixLower) =>
            Has(suffixLower, "walk", "yuru", "run", "kos", "idle", "bekle", "dur");

        // Sonek → controller durum adı (Walk/Idle/Run/Attack/Death ya da sonek kendisi).
        private static string StateNameOf(string suffixLower)
        {
            if (Has(suffixLower, "walk", "yuru"))            return "Walk";
            if (Has(suffixLower, "idle", "bekle", "dur"))    return "Idle";
            if (Has(suffixLower, "run", "kos"))              return "Run";
            if (Has(suffixLower, "attack", "saldiri", "vur")) return "Attack";
            if (Has(suffixLower, "death", "die", "olum"))    return "Death";
            return char.ToUpperInvariant(suffixLower[0]) + suffixLower.Substring(1);
        }

        // ── Import ayarları ───────────────────────────────────────────────────

        // FBX'in rig'ini Generic yapar, klibi @ sonekiyle adlandırır, locomotion ise loop açar.
        // Yalnızca bir şey DEĞİŞECEKSE reimport eder (idempotent, gereksiz churn yok).
        private static void EnsureImportSettings(string path, string suffix)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool loop = IsLooping(suffix.ToLowerInvariant());
            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                dirty = true;
            }
            if (!importer.importAnimation) { importer.importAnimation = true; dirty = true; }

            ModelImporterClipAnimation[] clipDefs = importer.clipAnimations;
            if (clipDefs == null || clipDefs.Length == 0) clipDefs = importer.defaultClipAnimations;

            if (clipDefs.Length > 0)
            {
                // Tek take varsayımı (Mixamo hep tek klip verir) — ilk klip esas alınır.
                ModelImporterClipAnimation c = clipDefs[0];
                if (c.name != suffix || c.loopTime != loop || c.loopPose != loop)
                {
                    c.name     = suffix;
                    c.loopTime = loop;
                    c.loopPose = loop; // döngü başı/sonu poz uyumu (yürüyüş takılmasın)
                    importer.clipAnimations = clipDefs;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[KamAnim] Import ayarlandı: {Path.GetFileName(path)} (klip='{suffix}', loop={loop})");
            }
        }

        private static AnimationClip LoadMainClip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                         .OfType<AnimationClip>()
                         .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        // ── Controller kurulumu ───────────────────────────────────────────────

        // KamAnimator.controller'ı deterministik yeniden kurar: Idle (varsayılan) ↔ Walk
        // "IsMoving" bool'una bağlı; diğer klipler kendi durumlarında hazır bekler
        // (Attack/Death geçişleri savaş animasyonu fazında koddan bağlanacak).
        private static AnimatorController BuildController(List<(string suffix, AnimationClip clip)> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(KamControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(KamControllerPath);

            // Parametreleri sıfırla → tek kaynaktan kur.
            foreach (AnimatorControllerParameter p in controller.parameters)
                controller.RemoveParameter(p);
            controller.AddParameter(IsMovingParam, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states)
                sm.RemoveState(s.state);

            AnimationClip walk = FindClip(clips, "walk", "yuru");
            AnimationClip idle = FindClip(clips, "idle", "bekle", "dur");

            // Idle — varsayılan durum. Gerçek idle klibi yoksa yürüyüşün ilk karesi dondurulur.
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idle != null ? idle : walk;
            if (idle == null) idleState.speed = 0f;
            sm.defaultState = idleState;

            if (walk != null)
            {
                AnimatorState walkState = sm.AddState("Walk");
                walkState.motion = walk;

                AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
                toWalk.hasExitTime = false;
                toWalk.duration    = 0.10f;
                toWalk.AddCondition(AnimatorConditionMode.If, 0f, IsMovingParam);

                AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
                toIdle.hasExitTime = false;
                toIdle.duration    = 0.15f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, IsMovingParam);
            }

            // Locomotion dışındaki klipler (Attack, Death, …) durum olarak eklenir.
            var placed = new HashSet<string> { "Idle", "Walk" };
            foreach ((string suffix, AnimationClip clip) in clips)
            {
                string stateName = StateNameOf(suffix);
                if (!placed.Add(stateName)) continue; // aynı ada ikinci klip → ilki kazanır
                AnimatorState st = sm.AddState(stateName);
                st.motion = clip;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KamAnim] Controller kuruldu: {KamControllerPath} ({clips.Count} klip)");
            return controller;
        }

        private static AnimationClip FindClip(List<(string suffix, AnimationClip clip)> clips, params string[] keys)
        {
            foreach ((string suffix, AnimationClip clip) in clips)
                if (Has(suffix, keys)) return clip;
            return null;
        }

        private static void EnsureKamFolder()
        {
            if (AssetDatabase.IsValidFolder(KamFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Models/Characters"))
                AssetDatabase.CreateFolder("Assets/Art/Models", "Characters");
            AssetDatabase.CreateFolder("Assets/Art/Models/Characters", "Kam");
        }
    }
}
