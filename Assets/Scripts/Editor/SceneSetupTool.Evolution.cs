using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SINIF EVRİMLERİ + SINIF YETENEKLERİ kurulumu (Efe'nin isteği 2026-10-03). Unity KAPALIYKEN:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupClassProgressionBatch -logFile log.txt
    /// </code>
    ///
    /// Yapar:
    ///   • Her oynanabilir sınıfın asset'ine 3 EVRİM + 3 YETENEK yazar — YALNIZ BOŞSA
    ///     (CLAUDE.md §9.1: Efe'nin ayarları ezilmez). Bedeller ŞİMDİLİK YER TUTUCU (Efe: "rastgele").
    ///   • <see cref="ClassEvolutionProgress"/> bileşeni (cüzdanın yanında).
    ///   • <see cref="UnitAbilityCaster"/> + <see cref="UnitAbilityFx"/> ve bağlantıları
    ///     (TurnManager, harita girdisi, savaş HUD'u, yerleştirme).
    /// KİTAP'taki evrim slotları karakter sayfasıyla birlikte (SetupUIShell) kurulur.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string CharacterDataFolder = "Assets/Data/Characters";

        [MenuItem("TacticalRPG/Savas - Sinif Evrim + Yetenek Kur", false, 31)]
        public static void SetupClassProgressionMenu()
        {
            int n = ApplyClassProgression();
            EditorUtility.DisplayDialog("Sinif Evrim + Yetenek",
                n > 0 ? "Kuruldu: sinif evrimleri/yetenekleri, evrim ilerlemesi, yetenek kullanicisi.\n\n" +
                        "KITAP sayfasindaki slotlar icin 'UI - Kam Yetenek Agacini Kur' da kostur.\n" +
                        "SAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede TurnManager yok. Once TAM KURULUM.",
                "Tamam");
        }

        public static void SetupClassProgressionBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { EditorApplication.Exit(1); return; }

            if (ApplyClassProgression() == 0) { EditorApplication.Exit(1); return; }
            // KİTAP kabuğu (karakter sayfası + evrim slotları + karo ağacı) ağaç kurulumuyla yeniden çizilir.
            if (ApplySkillTree() == 0) { EditorApplication.Exit(1); return; }
            ApplyChapterRules();   // davul manası + host durum bağı (idempotent)

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TAM KURULUM zincirindeki adım.</summary>
        public static void SetupClassProgression() => ApplyClassProgression();

        private static int ApplyClassProgression()
        {
            int seeded = SeedClassData();

            var turns = FindComponentAnywhere<TurnManager>();
            if (turns == null) { Debug.LogError("[Evrim] Sahnede TurnManager yok — once TAM KURULUM."); return 0; }

            ClassEvolutionProgress evo = EnsureClassEvolutionProgress();

            // Yetenek kullanıcısı + yer tutucu animasyonlar: TurnManager'ın nesnesinde.
            var caster = turns.GetComponent<UnitAbilityCaster>();
            if (caster == null) caster = turns.gameObject.AddComponent<UnitAbilityCaster>();
            var fx = turns.GetComponent<UnitAbilityFx>();
            if (fx == null) fx = turns.gameObject.AddComponent<UnitAbilityFx>();

            var cso = new SerializedObject(caster);
            cso.FindProperty("_turns").objectReferenceValue = turns;
            cso.FindProperty("_units").objectReferenceValue = FindComponentAnywhere<UnitManager>();
            cso.FindProperty("_grid").objectReferenceValue  = FindComponentAnywhere<TacticalRPG.Grid.HexGridManager>();
            cso.FindProperty("_fx").objectReferenceValue    = fx;
            cso.ApplyModifiedProperties();

            var input = FindComponentAnywhere<MapInputHandler>();
            if (input != null)
            {
                var so = new SerializedObject(input);
                so.FindProperty("_unitAbilities").objectReferenceValue = caster;
                so.ApplyModifiedProperties();
            }

            var hud = FindComponentAnywhere<TacticalRPG.UI.CombatHUD>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                so.FindProperty("_unitAbilities").objectReferenceValue = caster;
                so.ApplyModifiedProperties();
            }

            var deploy = FindComponentAnywhere<DeploymentManager>();
            if (deploy != null)
            {
                var so = new SerializedObject(deploy);
                so.FindProperty("_evolutions").objectReferenceValue = evo;
                so.ApplyModifiedProperties();
            }

            Debug.Log($"[Evrim] DOGRULAMA — sinif-verisi-yazilan:{seeded} ilerleme:{(evo != null)} " +
                      $"yetenek:{(caster != null)} fx:{(fx != null)} girdi:{(input != null)} " +
                      $"hud:{(hud != null)} yerlestirme:{(deploy != null)}");
            return 1;
        }

        private static ClassEvolutionProgress EnsureClassEvolutionProgress()
        {
            EssenceWallet wallet = FindComponentAnywhere<EssenceWallet>();
            GameObject host = wallet != null ? wallet.gameObject : GameObject.Find(SceneRootName);
            if (host == null) return null;

            var evo = host.GetComponent<ClassEvolutionProgress>();
            if (evo == null) evo = host.AddComponent<ClassEvolutionProgress>();
            var so = new SerializedObject(evo);
            so.FindProperty("_wallet").objectReferenceValue = wallet;
            so.ApplyModifiedProperties();
            return evo;
        }

        // ── Sınıf verisi ─────────────────────────────────────────────────────

        private struct EvoSpec
        {
            public string Name, Desc; public EvolutionTrait Trait; public int Amount;
            public EvoSpec(string n, string d, EvolutionTrait t, int a) { Name = n; Desc = d; Trait = t; Amount = a; }
        }

        private struct AbilitySpec
        {
            public string Name, Desc; public UnitAbilityKind Kind;
            public int Power, Hits, Range, Radius, Cooldown; public Color Color;
            public AbilitySpec(string n, string d, UnitAbilityKind k, int power, int range, int cd, Color c,
                               int hits = 1, int radius = 0)
            { Name = n; Desc = d; Kind = k; Power = power; Range = range; Cooldown = cd; Color = c; Hits = hits; Radius = radius; }
        }

        private static readonly Color Fire  = new(1.00f, 0.45f, 0.15f);
        private static readonly Color Steel = new(0.80f, 0.85f, 0.95f);
        private static readonly Color Blood = new(0.85f, 0.15f, 0.15f);
        private static readonly Color Arcane = new(0.65f, 0.40f, 1.00f);
        private static readonly Color Holy  = new(1.00f, 0.92f, 0.55f);
        private static readonly Color Nature = new(0.45f, 0.90f, 0.40f);
        private static readonly Color Frost = new(0.55f, 0.85f, 1.00f);
        private static readonly Color Shadow = new(0.45f, 0.35f, 0.55f);

        /// <summary>
        /// Sınıf → (3 evrim, 3 yetenek). Evrimler sınıfın KİMLİĞİNİ büyütür (Okçu: menzil → çift ok →
        /// ölümcül nişan); yetenekler savaşta "düz vuruş"un yerine seçenek verir. Menzil -1 = birimin
        /// kendi saldırı menzili, 0 = kendi çevresi. SAYILAR ÖRNEK.
        /// </summary>
        private static readonly Dictionary<string, (EvoSpec[] evo, AbilitySpec[] ab)> ClassSpecs = new()
        {
            ["Savasci"] = (new[]
            {
                new EvoSpec("Demir Deri",  "+2 savunma",                         EvolutionTrait.BonusDefense, 2),
                new EvoSpec("Diken Zırh",  "Yakından vurana 2 hasar döner",      EvolutionTrait.Thorns, 2),
                new EvoSpec("Yarma",       "Vuruşu yandakilere %50 sıçrar",      EvolutionTrait.Cleave, 50),
            }, new[]
            {
                new AbilitySpec("Güçlü Darbe",   "1.6 kat sert vuruş",            UnitAbilityKind.PowerStrike, 160, -1, 2, Steel),
                new AbilitySpec("Kalkan Duvarı", "Bir dosta 5 kalkan",            UnitAbilityKind.ShieldAlly,    5,  1, 3, Steel),
                new AbilitySpec("Sersemletici",  "Vurur ve 1 tur sersemletir",    UnitAbilityKind.StunStrike,   80, -1, 3, Frost),
            }),
            ["Barbar"] = (new[]
            {
                new EvoSpec("Kalın Kemik", "Savaşa 4 kalkanla iner",             EvolutionTrait.StartShield, 4),
                new EvoSpec("Kan Emici",   "Verdiği hasarın %40'ı cana döner",   EvolutionTrait.Lifesteal, 40),
                new EvoSpec("Çılgınlık",   "Her saldırıda ikinci darbe (%60)",   EvolutionTrait.DoubleStrike, 60),
            }, new[]
            {
                new AbilitySpec("Döner Balta", "Çevresindeki tüm düşmanlara %80", UnitAbilityKind.AreaBlast,   80,  0, 2, Blood, radius: 1),
                new AbilitySpec("Kan Öfkesi",  "2 kat yıkıcı vuruş",              UnitAbilityKind.PowerStrike, 200, -1, 3, Blood),
                new AbilitySpec("Kafa Atma",   "Vurur ve sersemletir",            UnitAbilityKind.StunStrike,   60, -1, 3, Steel),
            }),
            ["Okcu"] = (new[]
            {
                new EvoSpec("Keskin Göz",    "+1 menzil",                        EvolutionTrait.BonusRange, 1),
                new EvoSpec("Çift Ok",       "Her atışta ikinci ok (%70)",       EvolutionTrait.DoubleStrike, 70),
                new EvoSpec("Ölümcül Nişan", "+2 saldırı",                       EvolutionTrait.BonusAttack, 2),
            }, new[]
            {
                new AbilitySpec("Çift Atış",    "Aynı hedefe 2 ok (%70)",         UnitAbilityKind.MultiStrike,  70, -1, 2, Steel, hits: 2),
                new AbilitySpec("Ok Yağmuru",   "Alana %60 hasar (7 karo)",       UnitAbilityKind.AreaBlast,    60,  4, 3, Fire, radius: 1),
                new AbilitySpec("Nişancı Atışı","1.7 kat isabetli atış",          UnitAbilityKind.PowerStrike, 170,  4, 3, Steel),
            }),
            ["Ranger"] = (new[]
            {
                new EvoSpec("Hafif Adım", "+1 hareket",                          EvolutionTrait.BonusMove, 1),
                new EvoSpec("Doğa Bağı",  "Her turun başında 2 can",             EvolutionTrait.Regen, 2),
                new EvoSpec("Delici Ok",  "Vuruşu yandakilere %40 sıçrar",       EvolutionTrait.Cleave, 40),
            }, new[]
            {
                new AbilitySpec("Tuzak Oku",   "Uzaktan vurur, sersemletir",      UnitAbilityKind.StunStrike,   50,  3, 3, Nature),
                new AbilitySpec("Hızlı Darbe", "Aynı hedefe 3 darbe (%45)",       UnitAbilityKind.MultiStrike,  45, -1, 3, Steel, hits: 3),
                new AbilitySpec("Doğa Şifası", "Bir dosta 5 can",                 UnitAbilityKind.HealAlly,      5,  2, 3, Nature),
            }),
            ["Buyucu"] = (new[]
            {
                new EvoSpec("Ruh Kalkanı",   "Savaşa 4 kalkanla iner",           EvolutionTrait.StartShield, 4),
                new EvoSpec("Büyü Gücü",     "+2 saldırı",                       EvolutionTrait.BonusAttack, 2),
                new EvoSpec("Zincir Şimşek", "Vuruşu yandakilere %60 sıçrar",    EvolutionTrait.Cleave, 60),
            }, new[]
            {
                new AbilitySpec("Ateş Topu",     "Alana %90 hasar (7 karo)",      UnitAbilityKind.AreaBlast,    90,  4, 2, Fire, radius: 1),
                new AbilitySpec("Buz Mızrağı",   "Vurur ve dondurur",             UnitAbilityKind.StunStrike,   70,  4, 3, Frost),
                new AbilitySpec("Arkan Patlama", "1.8 kat saf büyü",              UnitAbilityKind.PowerStrike, 180,  3, 3, Arcane),
            }),
            ["Rahip"] = (new[]
            {
                new EvoSpec("Kutsanmış",  "Her turun başında 2 can",             EvolutionTrait.Regen, 2),
                new EvoSpec("Işık Zırhı", "Savaşa 5 kalkanla iner",              EvolutionTrait.StartShield, 5),
                new EvoSpec("Hızlı Dua",  "Yetenek beklemesi 1 tur kısalır",     EvolutionTrait.CooldownReduction, 1),
            }, new[]
            {
                new AbilitySpec("Şifa",          "Bir dosta 7 can",               UnitAbilityKind.HealAlly,      7,  3, 2, Holy),
                new AbilitySpec("Kutsal Kalkan", "Bir dosta 6 kalkan",            UnitAbilityKind.ShieldAlly,    6,  3, 3, Holy),
                new AbilitySpec("Işık Çarpması", "1.3 kat kutsal vuruş",          UnitAbilityKind.PowerStrike, 130,  3, 2, Holy),
            }),
            ["Serseri"] = (new[]
            {
                new EvoSpec("Çevik",       "+2 hız (sırada öne geçer)",          EvolutionTrait.BonusSpeed, 2),
                new EvoSpec("Gölge Adım",  "+1 hareket",                         EvolutionTrait.BonusMove, 1),
                new EvoSpec("Çift Hançer", "Her saldırıda ikinci darbe (%80)",   EvolutionTrait.DoubleStrike, 80),
            }, new[]
            {
                new AbilitySpec("Sırttan Bıçak",  "2.2 kat sinsi vuruş",          UnitAbilityKind.PowerStrike, 220, -1, 3, Shadow),
                new AbilitySpec("Hançer Yağmuru", "Aynı hedefe 2 darbe (%60)",    UnitAbilityKind.MultiStrike,  60, -1, 2, Steel, hits: 2),
                new AbilitySpec("Sis Bombası",    "Alana %40 + karmaşa (7 karo)", UnitAbilityKind.AreaBlast,    40,  2, 3, Shadow, radius: 1),
            }),
        };

        /// <summary>Sınıf asset'lerine evrim + yetenek yazar (BOŞSA). Yazılan sınıf sayısını döndürür.</summary>
        private static int SeedClassData()
        {
            int written = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterClassData", new[] { CharacterDataFolder }))
            {
                var data = AssetDatabase.LoadAssetAtPath<CharacterClassData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.IsCommander) continue;
                if (!ClassSpecs.TryGetValue(data.ClassName, out var spec)) continue;   // düşman sınıfları

                var so = new SerializedObject(data);
                bool changed = false;

                SerializedProperty evos = so.FindProperty("_evolutions");
                if (evos != null && evos.arraySize == 0)
                {
                    evos.arraySize = spec.evo.Length;
                    var rnd = new System.Random(data.ClassName.GetHashCode());
                    for (int i = 0; i < spec.evo.Length; i++)
                    {
                        SerializedProperty el = evos.GetArrayElementAtIndex(i);
                        el.FindPropertyRelative("_name").stringValue        = spec.evo[i].Name;
                        el.FindPropertyRelative("_description").stringValue = spec.evo[i].Desc;
                        el.FindPropertyRelative("_trait").enumValueIndex    = (int)spec.evo[i].Trait;
                        el.FindPropertyRelative("_amount").intValue         = spec.evo[i].Amount;
                        // YER TUTUCU BEDEL (Efe: "şimdilik rastgele"): basamak büyüdükçe pahalanır.
                        int tas  = (i == 0 ? 4 : i == 1 ? 9 : 15) + rnd.Next(0, 3);
                        int doga = (i == 0 ? 3 : i == 1 ? 6 : 11) + rnd.Next(0, 3);
                        WriteCost(el.FindPropertyRelative("_cost"), tas, doga);
                    }
                    changed = true;
                }

                SerializedProperty abs = so.FindProperty("_classAbilities");
                if (abs != null && abs.arraySize == 0)
                {
                    abs.arraySize = spec.ab.Length;
                    for (int i = 0; i < spec.ab.Length; i++)
                    {
                        AbilitySpec a = spec.ab[i];
                        SerializedProperty el = abs.GetArrayElementAtIndex(i);
                        el.FindPropertyRelative("_name").stringValue        = a.Name;
                        el.FindPropertyRelative("_description").stringValue = a.Desc;
                        el.FindPropertyRelative("_kind").enumValueIndex     = (int)a.Kind;
                        el.FindPropertyRelative("_power").intValue          = a.Power;
                        el.FindPropertyRelative("_hits").intValue           = a.Hits;
                        el.FindPropertyRelative("_range").intValue          = a.Range;
                        el.FindPropertyRelative("_radius").intValue         = a.Radius;
                        el.FindPropertyRelative("_cooldown").intValue       = a.Cooldown;
                        el.FindPropertyRelative("_color").colorValue        = a.Color;
                    }
                    changed = true;
                }

                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                written++;
            }
            if (written > 0) AssetDatabase.SaveAssets();
            return written;
        }
    }
}
