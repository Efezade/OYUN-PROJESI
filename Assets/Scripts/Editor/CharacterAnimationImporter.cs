using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Karakter modellerini + animasyonlarını oyuna bağlayan boru hattı (karo "Klasörü Tara"
    /// deseninin karakter karşılığı). HER KARAKTER için çalışır: <see cref="CharactersRoot"/>
    /// altındaki her alt klasör bir karakterdir ve klasör adı sınıfın <c>ClassName</c>'iyle
    /// eşleşir (Kam, Savasci, Ranger, Goblin …).
    ///
    /// KULLANIM: modeli klasöre at → menü "Karakter - Tum Karakterleri Kur". Araç her klasör için:
    ///   • FBX import ayarlarını yapar (rig tipi + klip adları + loop bayrakları),
    ///   • "&lt;Ad&gt;Animator.controller" kurar (Idle↔Walk "IsMoving"; Attack/Death trigger'lı),
    ///   • eşleşen <see cref="CharacterClassData"/> asset'ine modeli + controller'ı bağlar.
    ///
    /// İKİ MODEL/ANİMASYON BİÇİMİ desteklenir:
    ///   A) Mixamo deseni — her animasyon ayrı dosya: "model@Walking.fbx" (klip adı = @ soneki).
    ///   B) Tek dosyada ÇOK TAKE — "Orc.fbx" içinde Idle/Walk/Punch/Death (Quaternius deseni);
    ///      take adları korunur ve eşlenir.
    /// Kendi animasyonu OLMAYAN model (ör. Quaternius RPG karakterleri) <b>Humanoid</b> rig'e
    /// alınır ve <see cref="SharedHumanoidFolder"/> içindeki ORTAK klipler ona retarget edilir —
    /// böylece tek animasyon seti tüm insansı karakterleri sürer.
    ///
    /// Sonek/take eşlemesi (küçük harf İÇERİR araması):
    ///   walk/yuru → Walk (loop) · idle/bekle/dur → Idle (loop) · run/kos → Run (loop)
    ///   attack/saldiri/vur/punch/slash → Attack · death/die/olum → Death
    ///   (DİKKAT: "hit" anahtar DEĞİL — "HitReact" hasar tepkisidir, saldırı değil.)
    ///
    /// Controller asset'i YERİNDE yeniden kurulur (GUID sabit) → sahnedeki Animator ve
    /// sınıf asset'i referansları bozulmaz; yeni animasyon ekleyince TAM KURULUM GEREKMEZ.
    /// </summary>
    public static class CharacterAnimationImporter
    {
        public const string CharactersRoot       = "Assets/Art/Models/Characters";
        public const string KamFolder            = CharactersRoot + "/Kam";
        public const string KamControllerPath    = KamFolder + "/KamAnimator.controller";

        /// <summary>
        /// Kendi animasyonu olmayan HUMANOID karakterlerin ortak klip kaynağı. Buraya atılan
        /// "humanoid@Walking.fbx" gibi Mixamo klipleri Warrior/Ranger gibi tüm humanoid modellere
        /// retarget edilir. Klasör boşsa araç Kam'ın yürüyüş FBX'inden bir kopya tohumlar.
        /// </summary>
        public const string SharedHumanoidFolder = CharactersRoot + "/_SharedHumanoid";

        // CharacterAnimationDriver ile AYNI adlar (orada Animator.StringToHash ile aranır).
        private const string IsMovingParam = "IsMoving";
        private const string AttackParam   = "Attack";
        private const string DeathParam    = "Death";

        // Yeni karakterler Kam ile AYNI ölçü/duruşta kurulur (kullanıcı isteği).
        private const float   DefaultModelHeight = 1.5f;
        private static readonly Vector3 DefaultModelEuler = Vector3.zero;

        /// <summary>Bir karakter klasörünün kurulum sonucu (rapor için).</summary>
        public struct Result
        {
            public string             name;
            public GameObject         model;
            public AnimatorController controller;
            public string[]           clipNames;
            public bool               humanoidRetarget;
            public string             boundAssetPath;   // bağlandığı CharacterClassData (yoksa null)
        }

        // ── Menü ──────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Karakter - Tum Karakterleri Kur", false, 40)]
        public static void SetupAllMenu()
        {
            List<Result> results = SetupAllCharacters();

            if (results.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Karakter Kurulumu",
                    $"'{CharactersRoot}' altinda karakter klasoru bulunamadi!\n\n" +
                    "Her karakter icin bir klasor ac (klasor adi = sinif adi, or. 'Savasci'),\n" +
                    "icine modelin FBX'ini at ve bu menuyu tekrar calistir.",
                    "Tamam");
                return;
            }

            var sb = new System.Text.StringBuilder("Kurulum tamam:\n\n");
            foreach (Result r in results)
            {
                sb.AppendLine($"• {r.name}: {(r.model != null ? r.model.name : "MODEL YOK")}");
                sb.AppendLine($"    klipler: {(r.clipNames.Length > 0 ? string.Join(", ", r.clipNames) : "(yok)")}" +
                              (r.humanoidRetarget ? "  [ortak humanoid]" : ""));
                sb.AppendLine($"    sinif asset: {(r.boundAssetPath ?? "EŞLEŞMEDİ — klasor adi ClassName ile ayni mi?")}");
            }
            sb.AppendLine("\nYeni animasyon: 'model@AnimasyonAdi.fbx' dosyasini karakterin");
            sb.AppendLine("klasorune at ve bu menuyu tekrar calistir (TAM KURULUM gerekmez).");

            EditorUtility.DisplayDialog("Karakter Kurulumu", sb.ToString(), "Tamam");
        }

        // ── Ana akış ──────────────────────────────────────────────────────────

        /// <summary>
        /// Tüm karakter klasörlerini kurar ve eşleşen sınıf asset'lerine bağlar.
        /// Idempotent — TAM KURULUM (SceneSetupTool) da çağırır.
        /// </summary>
        public static List<Result> SetupAllCharacters()
        {
            var results = new List<Result>();
            if (!AssetDatabase.IsValidFolder(CharactersRoot)) return results;

            SeedSharedHumanoidFolder();
            List<(string name, AnimationClip clip)> sharedClips = LoadSharedHumanoidClips();

            foreach (string folder in CharacterFolders())
            {
                Result r = SetupCharacter(folder, sharedClips);
                if (r.model != null || r.controller != null) results.Add(r);
            }

            AssetDatabase.SaveAssets();
            return results;
        }

        /// <summary>
        /// TÜM karakterleri kurar ve Kam'ın controller'ını döndürür. TAM KURULUM (SceneSetupTool)
        /// bunu çağırır — böylece Kam'ın yanında Savasci/Ranger/Goblin de sınıf asset'lerine bağlanır.
        /// </summary>
        public static AnimatorController SetupKam()
        {
            EnsureFolder(KamFolder);
            foreach (Result r in SetupAllCharacters())
                if (NameMatches(r.name, "Kam")) return r.controller;
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(KamControllerPath);
        }

        /// <summary>
        /// Kam'ın GÖRSEL model kaynağı. Öncelik: @'siz temel model FBX'i → @walk'lu FBX
        /// (mesh+rig+animasyonu birlikte taşır) → klasördeki ilk FBX. Yoksa null.
        /// </summary>
        public static GameObject FindKamModelFbx() => FindModelFbx(KamFolder);

        // Bir karakter klasörünü kurar: import ayarları → controller → sınıf asset'ine bağla.
        private static Result SetupCharacter(string folder, List<(string name, AnimationClip clip)> sharedClips)
        {
            var result = new Result { name = Path.GetFileName(folder), clipNames = System.Array.Empty<string>() };

            string modelPath = FindModelFbxPath(folder);
            if (modelPath == null) return result;

            // 1) Kendi klipleri: @'li dosyalar + model dosyasının kendi take'leri.
            var clips = new List<(string name, AnimationClip clip)>();
            foreach (string path in AnimationFbxPaths(folder))
            {
                string suffix = SuffixOf(path);
                EnsureAnimationFbx(path, suffix, ModelImporterAnimationType.Generic);
                AnimationClip clip = LoadMainClip(path);
                if (clip != null) clips.Add((suffix, clip));
                else Debug.LogWarning($"[Karakter] '{path}' icinde animasyon klibi bulunamadi.");
            }

            bool modelIsAlsoAnimSource = SuffixOf(modelPath) != null; // Kam: barbarkarakteri@Walking.fbx
            if (!modelIsAlsoAnimSource && HasUsableTakes(modelPath))
            {
                // Tek dosyada çok take olabilir (Quaternius). ÖNCE import ayarını yap (klipler
                // ancak importAnimation açıkken asset olarak üretilir), SONRA klipleri oku.
                // Yalnız TANINAN klipler alınır — "HitReact"/"Wave"/"Duck" gibi kullanmadıklarımız
                // controller'ı kalabalıklaştırmasın.
                EnsureMultiTakeFbx(modelPath);
                foreach (AnimationClip c in LoadAllClips(modelPath))
                    if (IsRecognizedClip(c.name)) clips.Add((c.name, c));
            }

            // 2) Kendi animasyonu yoksa → HUMANOID rig + ortak klipleri retarget et.
            bool retarget = clips.Count == 0 && sharedClips.Count > 0;
            if (clips.Count == 0)
            {
                EnsureModelRig(modelPath, ModelImporterAnimationType.Human);
                clips.AddRange(sharedClips);
            }
            result.humanoidRetarget = retarget;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            result.model = model;

            // 3) Controller (klip varsa).
            if (clips.Count > 0)
            {
                result.controller = BuildController(ControllerPathFor(folder), clips);
                result.clipNames  = clips.Select(c => StateNameOf(c.name)).Distinct().ToArray();
            }

            // 4) Sınıf asset'ine bağla.
            result.boundAssetPath = BindToClassAsset(result.name, model, result.controller);
            return result;
        }

        // ── Sınıf asset'ine bağlama ───────────────────────────────────────────

        // Klasör adı ile ClassName eşleşen CharacterClassData'ya modeli + controller'ı yazar.
        // Model ZATEN aynıysa ölçü/duruş alanlarına DOKUNMAZ (kullanıcının ayarı korunur).
        private static string BindToClassAsset(string characterName, GameObject model,
                                               AnimatorController controller)
        {
            if (model == null) return null;

            foreach (string guid in AssetDatabase.FindAssets("t:CharacterClassData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CharacterClassData>(path);
                if (data == null || !NameMatches(data.ClassName, characterName)) continue;

                var so = new SerializedObject(data);
                SerializedProperty modelProp = so.FindProperty("_unitModel");
                bool isNewModel = modelProp.objectReferenceValue != model;

                modelProp.objectReferenceValue = model;
                so.FindProperty("_unitAnimator").objectReferenceValue = controller;

                // Yeni model → Kam ile aynı ölçü/duruş. Aynı model → kullanıcı ayarını KORU.
                if (isNewModel)
                {
                    so.FindProperty("_unitModelHeight").floatValue   = DefaultModelHeight;
                    so.FindProperty("_unitModelEuler").vector3Value  = DefaultModelEuler;
                    so.FindProperty("_unitModelYOffset").floatValue  = 0f;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                return path;
            }
            return null;
        }

        // "Savasci" == "Savascı" == "savasci" (Türkçe karakter/büyük-küçük harf toleransı).
        private static bool NameMatches(string a, string b) => Normalize(a) == Normalize(b);

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
            {
                char c = ch switch
                {
                    'ı' or 'i' or 'î' => 'i',
                    'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c',
                    _   => ch
                };
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Ortak humanoid klipler ────────────────────────────────────────────

        // Ortak klasör boşsa Kam'ın yürüyüş FBX'inden bir KOPYA tohumlar (Humanoid olarak import
        // edilir) — böylece kendi animasyonu olmayan modeller en azından yürür.
        private static void SeedSharedHumanoidFolder()
        {
            EnsureFolder(SharedHumanoidFolder);
            if (AllFbxPaths(SharedHumanoidFolder).Any()) return;

            string source = AnimationFbxPaths(KamFolder).FirstOrDefault(p => IsWalk(SuffixOf(p)));
            if (source == null) return;

            string dest = $"{SharedHumanoidFolder}/humanoid@{SuffixOf(source)}.fbx";
            File.Copy(source, dest, true);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Karakter] Ortak humanoid klip tohumlandi: {dest} (kaynak: {Path.GetFileName(source)})");
        }

        private static List<(string name, AnimationClip clip)> LoadSharedHumanoidClips()
        {
            var list = new List<(string, AnimationClip)>();
            if (!AssetDatabase.IsValidFolder(SharedHumanoidFolder)) return list;

            foreach (string path in AllFbxPaths(SharedHumanoidFolder))
            {
                string suffix = SuffixOf(path) ?? Path.GetFileNameWithoutExtension(path);
                EnsureAnimationFbx(path, suffix, ModelImporterAnimationType.Human);
                AnimationClip clip = LoadMainClip(path);
                if (clip != null) list.Add((suffix, clip));
            }
            return list;
        }

        // ── Tarama yardımcıları ───────────────────────────────────────────────

        private static IEnumerable<string> CharacterFolders() =>
            Directory.GetDirectories(CharactersRoot)
                     .Select(p => p.Replace('\\', '/'))
                     .Where(p => !Path.GetFileName(p).StartsWith("_"))   // "_SharedHumanoid" karakter değil
                     .OrderBy(p => p);

        private static IEnumerable<string> AllFbxPaths(string folder) =>
            !Directory.Exists(folder)
                ? Enumerable.Empty<string>()
                : Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly)
                           .Select(p => p.Replace('\\', '/'))
                           .OrderBy(p => p);

        private static IEnumerable<string> AnimationFbxPaths(string folder) =>
            AllFbxPaths(folder).Where(p => SuffixOf(p) != null);

        // Modelin FBX'i: @'siz dosya (Quaternius) → @walk'lu dosya (Mixamo) → ilk FBX.
        private static string FindModelFbxPath(string folder)
        {
            string best = null;
            foreach (string path in AllFbxPaths(folder))
            {
                string suffix = SuffixOf(path);
                if (suffix == null) return path;                        // düz model dosyası en iyisi
                if (best == null || IsWalk(suffix)) best = path;        // yoksa yürüyüş FBX'i
            }
            return best;
        }

        private static GameObject FindModelFbx(string folder)
        {
            string p = FindModelFbxPath(folder);
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }

        private static string ControllerPathFor(string folder) =>
            $"{folder}/{Path.GetFileName(folder)}Animator.controller";

        // "model@Walking.fbx" → "Walking"; @ yoksa null (animasyon dosyası değil).
        private static string SuffixOf(string path)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            int at = file.IndexOf('@');
            return at >= 0 && at < file.Length - 1 ? file.Substring(at + 1) : null;
        }

        private static bool Has(string s, params string[] keys) =>
            !string.IsNullOrEmpty(s) && keys.Any(s.ToLowerInvariant().Contains);

        private static bool IsWalk(string s)    => Has(s, "walk", "yuru");
        private static bool IsLooping(string s) => Has(s, "walk", "yuru", "run", "kos", "idle", "bekle", "dur");

        /// <summary>Oyunun kullandığı bir duruma karşılık gelen klip adı mı?</summary>
        private static bool IsRecognizedClip(string name) =>
            Has(name, "walk", "yuru", "idle", "bekle", "dur", "run", "kos",
                      "attack", "saldiri", "vur", "punch", "slash", "death", "die", "olum");

        // Model dosyasının İÇİNDE işimize yarayan take var mı? Quaternius RPG karakterleri tek
        // ANLAMSIZ take ile gelir ("Take 001") — onları animasyonlu sanıp Humanoid retarget
        // yolundan çıkmamak için take ADLARINA bakılır.
        private static bool HasUsableTakes(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer?.importedTakeInfos == null) return false;

            foreach (TakeInfo t in importer.importedTakeInfos)
                if (IsRecognizedClip(t.name) || IsRecognizedClip(t.defaultClipName)) return true;
            return false;
        }

        // Klip/sonek adı → controller durum adı.
        // "hit" ANAHTAR DEĞİL: Quaternius "HitReact" = hasar tepkisi, saldırı değil.
        private static string StateNameOf(string name)
        {
            if (Has(name, "walk", "yuru"))                            return "Walk";
            if (Has(name, "idle", "bekle", "dur"))                    return "Idle";
            if (Has(name, "run", "kos"))                              return "Run";
            if (Has(name, "attack", "saldiri", "vur", "punch", "slash")) return "Attack";
            if (Has(name, "death", "die", "olum"))                    return "Death";
            return string.IsNullOrEmpty(name)
                ? "Clip"
                : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        // ── Import ayarları ───────────────────────────────────────────────────

        // Tek klipli animasyon FBX'i (Mixamo deseni): rig tipi + klip adı = @ soneki + loop.
        // Yalnızca bir şey DEĞİŞECEKSE reimport eder (idempotent, gereksiz churn yok).
        private static void EnsureAnimationFbx(string path, string clipName,
                                               ModelImporterAnimationType rig)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool loop  = IsLooping(clipName);
            bool dirty = false;

            if (importer.animationType != rig) { importer.animationType = rig; dirty = true; }
            if (!importer.importAnimation)     { importer.importAnimation = true; dirty = true; }

            ModelImporterClipAnimation[] clipDefs = importer.clipAnimations;
            if (clipDefs == null || clipDefs.Length == 0) clipDefs = importer.defaultClipAnimations;

            if (clipDefs.Length > 0)
            {
                // Tek take varsayımı (Mixamo hep tek klip verir) — ilk klip esas alınır.
                ModelImporterClipAnimation c = clipDefs[0];
                if (c.name != clipName || c.loopTime != loop || c.loopPose != loop)
                {
                    c.name     = clipName;
                    c.loopTime = loop;
                    c.loopPose = loop; // döngü başı/sonu poz uyumu (yürüyüş takılmasın)
                    importer.clipAnimations = clipDefs;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[Karakter] Import ayarlandi: {Path.GetFileName(path)} (klip='{clipName}', rig={rig}, loop={loop})");
            }
        }

        // Çok take'li model FBX'i (Quaternius deseni): take ADLARI KORUNUR, yalnız loop bayrakları
        // kendi adlarına göre ayarlanır (Idle/Walk/Run döner, Punch/Death dönmez).
        private static void EnsureMultiTakeFbx(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            // Dosyada hiç take yoksa (ör. Quaternius RPG karakterleri) animasyon ayarına DOKUNMA —
            // o modeller Humanoid'e alınıp ortak kliplerle sürülecek.
            if (importer.importedTakeInfos == null || importer.importedTakeInfos.Length == 0) return;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            { importer.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; dirty = true; }

            ModelImporterClipAnimation[] clipDefs = importer.clipAnimations;
            if (clipDefs == null || clipDefs.Length == 0) clipDefs = importer.defaultClipAnimations;

            bool clipsDirty = false;
            foreach (ModelImporterClipAnimation c in clipDefs)
            {
                bool loop = IsLooping(c.name);
                if (c.loopTime != loop || c.loopPose != loop)
                {
                    c.loopTime = loop;
                    c.loopPose = loop;
                    clipsDirty = true;
                }
            }
            if (clipsDirty) { importer.clipAnimations = clipDefs; dirty = true; }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[Karakter] Cok-take import ayarlandi: {Path.GetFileName(path)} ({clipDefs.Length} klip)");
            }
        }

        // Animasyonsuz model FBX'i: yalnız rig tipini ayarlar (Humanoid → retarget edilebilir).
        private static void EnsureModelRig(string path, ModelImporterAnimationType rig)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.animationType == rig) return;

            importer.animationType = rig;
            importer.SaveAndReimport();
            Debug.Log($"[Karakter] Rig ayarlandi: {Path.GetFileName(path)} → {rig} (ortak klipler retarget edilecek)");
        }

        private static AnimationClip LoadMainClip(string path) =>
            LoadAllClips(path).FirstOrDefault();

        private static List<AnimationClip> LoadAllClips(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                         .OfType<AnimationClip>()
                         .Where(c => !c.name.StartsWith("__preview__"))
                         .ToList();

        // ── Controller kurulumu ───────────────────────────────────────────────

        // Controller'ı deterministik yeniden kurar: Idle (varsayılan) ↔ Walk "IsMoving" bool'una
        // bağlı; Attack/Death klipleri varsa AnyState'ten trigger'la bağlanır
        // (CharacterAnimationDriver sürer). Tanınmayan klipler eklenmez (controller temiz kalır).
        private static AnimatorController BuildController(
            string controllerPath, List<(string name, AnimationClip clip)> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Parametreleri sıfırla → tek kaynaktan kur.
            foreach (AnimatorControllerParameter p in controller.parameters)
                controller.RemoveParameter(p);
            controller.AddParameter(IsMovingParam, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states)
                sm.RemoveState(s.state);

            AnimationClip walk   = FindClip(clips, "walk", "yuru");
            AnimationClip idle   = FindClip(clips, "idle", "bekle", "dur");
            AnimationClip attack = FindClip(clips, "attack", "saldiri", "vur", "punch", "slash");
            AnimationClip death  = FindClip(clips, "death", "die", "olum");

            // Idle — varsayılan durum. Gerçek idle klibi yoksa yürüyüşün ilk karesi dondurulur.
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idle != null ? idle : walk;
            if (idle == null) idleState.speed = 0f;
            sm.defaultState = idleState;

            if (walk != null)
            {
                AnimatorState walkState = sm.AddState("Walk");
                walkState.motion = walk;
                // Yürüyüş temposu. Komşu karo mesafesi = 2*InnerRadius ≈ 1.73m, PlayerController
                // _moveSpeed 3.5 → karo geçişi ~0.5sn. 0.75'te klibin ancak ~%30'u oynuyordu
                // (karo başına yarım adım, ayak kayıyordu); 1.5 → karo başına ~1.2 adım.
                walkState.speed = 1.5f;

                AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
                toWalk.hasExitTime = false;
                toWalk.duration    = 0.06f;
                toWalk.AddCondition(AnimatorConditionMode.If, 0f, IsMovingParam);

                // Durunca yürüyüşten hızlı çık (ayaklar arkadan sürüklenmesin).
                AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
                toIdle.hasExitTime = false;
                toIdle.duration    = 0.06f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, IsMovingParam);
            }

            WireAttack(controller, sm, attack, idleState);
            WireDeath(controller, sm, death);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Karakter] Controller kuruldu: {controllerPath} " +
                      $"(idle={idle != null}, walk={walk != null}, attack={attack != null}, death={death != null})");
            return controller;
        }

        // Saldırı: HERHANGİ bir durumdan "Attack" trigger'ıyla girilir, klip bitince Idle'a döner.
        // Kendine geçiş kapalı (canTransitionToSelf=false) → hızlı ard arda tetikleme klibi kesmez.
        private static void WireAttack(AnimatorController controller, AnimatorStateMachine sm,
                                       AnimationClip attack, AnimatorState idleState)
        {
            if (attack == null) return;

            AnimatorState state = sm.AddState("Attack");
            state.motion = attack;
            controller.AddParameter(AttackParam, AnimatorControllerParameterType.Trigger);

            AnimatorStateTransition toAttack = sm.AddAnyStateTransition(state);
            toAttack.hasExitTime         = false;
            toAttack.duration            = 0.05f;
            toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackParam);

            AnimatorStateTransition back = state.AddTransition(idleState);
            back.hasExitTime = true;   // klip bitince
            back.exitTime    = 0.9f;
            back.duration    = 0.1f;
        }

        // Ölüm: HERHANGİ bir durumdan "Death" trigger'ıyla girilir ve ÇIKIŞ YOK (son pozda kalır —
        // birim zaten kısa süre sonra sahneden silinir).
        private static void WireDeath(AnimatorController controller, AnimatorStateMachine sm,
                                      AnimationClip death)
        {
            if (death == null) return;

            AnimatorState state = sm.AddState("Death");
            state.motion = death;
            controller.AddParameter(DeathParam, AnimatorControllerParameterType.Trigger);

            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(state);
            toDeath.hasExitTime         = false;
            toDeath.duration            = 0.05f;
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, DeathParam);
        }

        private static AnimationClip FindClip(List<(string name, AnimationClip clip)> clips,
                                              params string[] keys)
        {
            foreach ((string name, AnimationClip clip) in clips)
                if (Has(name, keys)) return clip;
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
