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

        /// <summary>
        /// BATCH: controller'ları YENİDEN kurar ve öncesi/sonrası klip bağlarını loglar.
        /// <c>-executeMethod TacticalRPG.Editor.CharacterAnimationImporter.RebuildAndReportBatch</c>
        ///
        /// Neden rapor: "düşman yanlış animasyon oynatıyor" gibi bir hatayı Play'e basmadan
        /// kanıtlamanın tek yolu, controller'daki HANGİ DURUMUN HANGİ KLİBE bağlı olduğunu
        /// okumaktır. Bu satırlar hatanın hem teşhisi hem düzeldiğinin kanıtıdır.
        /// </summary>
        public static void RebuildAndReportBatch()
        {
            ReportControllers("ONCE ");
            ReportRootMotion("ONCE ");
            SetupAllCharacters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReportControllers("SONRA");
            ReportRootMotion("SONRA");
        }

        /// <summary>
        /// Her karakterin her klibinin KAÇ BİRİM yol aldığını loglar — "yürürken kayıyor/zıplıyor"
        /// şikâyetini Play'e basmadan kanıtlamanın yolu. Düzeltmeden ÖNCE yürüyüş klibi metrelerce
        /// yol alır; düzeltmeden SONRA ~0 olmalıdır (yer değiştirme poza gömülür).
        /// </summary>
        [MenuItem("TacticalRPG/Karakter - Animasyon Kok Hareketi Raporu", false, 41)]
        public static void ReportRootMotionMenu() => ReportRootMotion("RAPOR");

        public static void ReportRootMotion(string tag)
        {
            foreach (string folder in CharacterFolders())
            {
                string name = Path.GetFileName(folder);
                var sb = new System.Text.StringBuilder($"[Anim {tag}] {name} kok yolu: ");
                bool any = false;

                foreach (string path in AllFbxPaths(folder))
                    foreach (AnimationClip clip in LoadAllClips(path))
                    {
                        float travel = MeasureRootTravel(clip, out string node);
                        sb.Append($"{clip.name}={travel:F2}");
                        if (travel > RootTravelThreshold) sb.Append($" (YER DEGISTIRIYOR @ {node})");
                        sb.Append("  ");
                        any = true;
                    }

                Debug.Log(any ? sb.ToString() : $"[Anim {tag}] {name}: klip yok");
            }
        }

        /// <summary>Her karakterin controller'ındaki durum → klip eşlemesini loglar.</summary>
        public static void ReportControllers(string tag)
        {
            foreach (string folder in CharacterFolders())
            {
                string name = Path.GetFileName(folder);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPathFor(folder));
                if (ctrl == null) { Debug.Log($"[Anim {tag}] {name}: controller YOK"); continue; }

                var sb = new System.Text.StringBuilder($"[Anim {tag}] {name}: ");
                foreach (AnimatorControllerLayer layer in ctrl.layers)
                    foreach (ChildAnimatorState st in layer.stateMachine.states)
                        sb.Append($"{st.state.name}=[{(st.state.motion != null ? st.state.motion.name : "KLIP YOK")}]  ");
                Debug.Log(sb.ToString());
            }
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
            !IsExcludedClip(name) &&
            Has(name, "walk", "yuru", "idle", "bekle", "dur", "run", "kos",
                      "attack", "saldiri", "vur", "punch", "slash", "death", "die", "olum");

        /// <summary>
        /// ANAHTAR KELİMEYİ İÇERSE BİLE ALINMAYACAK klipler.
        ///
        /// 2026-08-13 HATA RAPORU ("düşmanlar sürekli zıplıyor"): Quaternius Orc.fbx içinde
        /// <c>Jump_Idle</c> adlı bir take var — havada asılı kalma pozu. Adı "idle" içerdiği için
        /// süzgeçten geçiyordu ve <see cref="FindClip"/> ilk eşleşeni aldığı için (asset yükleme
        /// sırası GARANTİ DEĞİLDİR) bazen GERÇEK Idle yerine BU bağlanıyordu → düşman savaş
        /// boyunca zıplama pozunda titriyordu. Aynı tuzak: <c>Jump_Land</c>, <c>Fall_Idle</c>,
        /// <c>Crouch_Idle</c>, <c>Swim_Idle</c>…
        /// </summary>
        private static bool IsExcludedClip(string name) =>
            Has(name, "jump", "zipla", "fall", "land", "crouch", "duck", "sit", "swim", "climb",
                      "dance", "wave", "hitreact");

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

        /// <summary>
        /// LOOP POSE NEDEN KAPALI (2026-08-13 hata raporu: "düşmanlar sürekli zıplıyor").
        ///
        /// Eski kod döngülü kliplerde <c>loopTime</c> ile birlikte <c>loopPose</c>'u da açıyordu.
        /// İkisi AYNI ŞEY DEĞİL:
        ///   • loopTime = "klip bitince baştan başla" — istediğimiz bu.
        ///   • loopPose = "son kareyi ilk kareye ZORLA uydur" — Unity klibin tamamına düzeltme
        ///     uygular. Zaten temiz döngülen bir klipte (Quaternius take'leri böyle) bu düzeltme
        ///     kökü her döngüde kaydırır → karakter her tur başında yukarı seker. Yerinde duran
        ///     bir düşmanda bu, saniyede bir zıplama gibi görünür.
        /// loopPose YALNIZCA kötü döngülenen (Mixamo'dan kesilmiş) kliplerde açılmalı, o da
        /// tek tek elle. Bu yüzden araç artık ASLA zorlamıyor; kapalıya çekiyor.
        /// </summary>
        private static void LoopPoseWarning() { /* yalnızca yukarıdaki açıklamayı taşır */ }

        /// <summary>
        /// KÖK HAREKETİ (root motion) NEDEN POZA GÖMÜLÜR — 2026-08-17 hata raporu:
        /// "Kam yürürken buga girerek yürüyor, loopta bir sıkıntı var".
        ///
        /// Mixamo'dan "In Place" işaretlenmeden indirilen bir yürüyüş klibi kalçayı ileri TAŞIR:
        /// klip boyunca karakter birkaç metre yol alır, klip başa sarınca da bir anda geri sıçrar.
        /// Konumu KOD sürdüğü için (PlayerController) Animator'de <c>applyRootMotion</c> kapalı;
        /// ama Generic rig'te "Root Motion Node = None" iken bu hareket kök hareketi olarak
        /// AYRIŞTIRILMAZ, sıradan bir transform eğrisi gibi kalçaya uygulanır → model kendi
        /// GameObject'inden kayar ve her döngüde geri zıplar. Görünen tam olarak "bug gibi yürüme".
        ///
        /// Çözüm: bir kök düğümü (hips) seçilir ve döngülü kliplerde "Bake Into Pose" açılır
        /// (<c>lockRootPositionXZ/HeightY/Rotation</c>) → yer değiştirme poza gömülür, karakter
        /// YERİNDE yürür, döngüde sıçrama kalmaz.
        ///
        /// KÖRLEMESİNE UYGULANMAZ: klip GERÇEKTEN yol alıyor mu diye ölçülür
        /// (<see cref="MeasureRootTravel"/>). Zaten yerinde olan klipler (Quaternius düşmanları)
        /// hiç ellenmez — 2026-08-13'te güçlükle düzeltilen düşman animasyonları bozulmasın.
        /// </summary>
        private const float RootTravelThreshold = 0.25f;

        /// <summary>Klibin kök/kalça eğrilerinde kaç birim YATAY yol aldığı. 0'a yakınsa klip
        /// "in place"tir. Ölçüm hem Generic transform eğrilerinde hem Humanoid RootT eğrilerinde
        /// çalışır — hangi düğümün taşıdığı <paramref name="node"/> ile bildirilir.</summary>
        private static float MeasureRootTravel(AnimationClip clip, out string node)
        {
            node = null;
            if (clip == null) return 0f;

            float best = 0f;
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            {
                bool horizontal = b.propertyName.EndsWith("Position.x") || b.propertyName.EndsWith("Position.z")
                               || b.propertyName == "RootT.x"           || b.propertyName == "RootT.z";
                if (!horizontal) continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null || curve.length < 2) continue;

                float travel = Mathf.Abs(curve.Evaluate(curve[curve.length - 1].time)
                                       - curve.Evaluate(curve[0].time));
                if (travel <= best) continue;
                best = travel;
                node = string.IsNullOrEmpty(b.path) ? "(kök)" : b.path;
            }
            return best;
        }

        /// <summary>Generic rig'te kök hareketinin ayrıştırılacağı düğümü seçer (Mixamo'da
        /// "mixamorig:Hips"). Zaten seçilmişse dokunmaz. Değiştirdiyse true döner.</summary>
        private static bool EnsureRootMotionNode(ModelImporter importer)
        {
            if (importer.animationType != ModelImporterAnimationType.Generic) return false;
            if (!string.IsNullOrEmpty(importer.motionNodeName)) return false;

            string first = null, hips = null;
            foreach (string p in importer.transformPaths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                int slash = p.LastIndexOf('/');
                string leaf = slash >= 0 ? p.Substring(slash + 1) : p;
                first ??= leaf;
                if (leaf.ToLowerInvariant().Contains("hips")) { hips = leaf; break; }
            }

            string pick = hips ?? first;
            if (string.IsNullOrEmpty(pick)) return false;

            importer.motionNodeName = pick;
            Debug.Log($"[Karakter] Kok hareketi dugumu secildi: '{pick}' " +
                      $"({Path.GetFileName(importer.assetPath)})");
            return true;
        }

        /// <summary>Döngülü klipte yer değiştirmeyi poza gömer. Bir şey değiştiyse true döner.</summary>
        private static bool BakeRootMotion(ModelImporterClipAnimation c)
        {
            if (c.lockRootPositionXZ && c.lockRootHeightY && c.lockRootRotation) return false;
            c.lockRootPositionXZ = true;   // ileri kayma → poza
            c.lockRootHeightY    = true;   // dikey sürüklenme → poza
            c.lockRootRotation   = true;   // sapma → poza (yönü kod sürüyor)
            return true;
        }

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

            // Klip yol alıyor mu? (bkz. RootTravelThreshold açıklaması)
            float travel   = MeasureRootTravel(LoadMainClip(path), out string travelNode);
            bool  needBake = loop && travel > RootTravelThreshold;
            if (needBake && EnsureRootMotionNode(importer)) dirty = true;

            ModelImporterClipAnimation[] clipDefs = importer.clipAnimations;
            if (clipDefs == null || clipDefs.Length == 0) clipDefs = importer.defaultClipAnimations;

            if (clipDefs.Length > 0)
            {
                // Tek take varsayımı (Mixamo hep tek klip verir) — ilk klip esas alınır.
                ModelImporterClipAnimation c = clipDefs[0];
                bool baked = needBake && BakeRootMotion(c);

                if (c.name != clipName || c.loopTime != loop || c.loopPose || baked)
                {
                    c.name     = clipName;
                    c.loopTime = loop;
                    c.loopPose = false;   // bkz. LoopPoseWarning
                    importer.clipAnimations = clipDefs;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[Karakter] Import ayarlandi: {Path.GetFileName(path)} " +
                          $"(klip='{clipName}', rig={rig}, loop={loop}, " +
                          $"kok yolu={travel:F2} birim @ {travelNode ?? "-"}, poza gomuldu={needBake})");
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

            // Hangi take gerçekten yol alıyor? Yerinde olanlara DOKUNULMAZ (Quaternius düşmanları).
            var travelByClip = new Dictionary<string, float>();
            foreach (AnimationClip existing in LoadAllClips(path))
                travelByClip[existing.name] = MeasureRootTravel(existing, out _);

            bool clipsDirty = false;
            bool anyBake    = false;
            foreach (ModelImporterClipAnimation c in clipDefs)
            {
                bool loop = IsLooping(c.name);
                travelByClip.TryGetValue(c.name, out float travel);
                bool needBake = loop && travel > RootTravelThreshold;

                bool baked = needBake && BakeRootMotion(c);
                anyBake |= baked;

                // LOOP POSE ASLA ZORLANMAZ — bkz. LoopPoseWarning.
                if (c.loopTime != loop || c.loopPose || baked)
                {
                    c.loopTime = loop;
                    c.loopPose = false;
                    clipsDirty = true;
                }
            }
            if (anyBake && EnsureRootMotionNode(importer)) dirty = true;
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

        /// <summary>
        /// Anahtar kelimeye UYAN EN İYİ klibi seçer — ilk uyanı değil.
        ///
        /// Neden puanlama: bir FBX'te aynı kelimeyi taşıyan birden çok take olabiliyor
        /// ("Idle" ve "Jump_Idle"). <c>AssetDatabase.LoadAllAssetsAtPath</c> sırası garanti
        /// olmadığı için "ilkini al" kuralı MAKİNEYE GÖRE değişen sonuç veriyordu. Puanlama:
        ///   1. Klip adının çekirdeği (Armature|/boşluk/alt çizgi ayıklanmış) anahtarla TAM EŞİTSE
        ///      en iyi eşleşme — "Idle" her zaman "Jump_Idle"ı yener.
        ///   2. Eşit değilse adı KISA olan kazanır (az ek = az süslenmiş = temel klip).
        /// </summary>
        private static AnimationClip FindClip(List<(string name, AnimationClip clip)> clips,
                                              params string[] keys)
        {
            AnimationClip best = null;
            int bestScore = int.MinValue;

            foreach ((string name, AnimationClip clip) in clips)
            {
                if (IsExcludedClip(name) || !Has(name, keys)) continue;

                string core  = CoreName(name);
                bool   exact = false;
                foreach (string k in keys)
                    if (string.Equals(core, k, System.StringComparison.OrdinalIgnoreCase)) { exact = true; break; }

                int score = (exact ? 1000 : 0) - core.Length;
                if (score <= bestScore) continue;
                bestScore = score;
                best      = clip;
            }
            return best;
        }

        /// <summary>"CharacterArmature|Jump_Idle" → "jump_idle"; kıyaslama için sadeleştirir.</summary>
        private static string CoreName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int bar = name.LastIndexOf('|');
            if (bar >= 0 && bar < name.Length - 1) name = name.Substring(bar + 1);
            return name.Trim().ToLowerInvariant();
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
