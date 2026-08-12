using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Planlanan 5 asker sınıfını (Barbar · Okçu · Büyücü · Rahip · Serseri) VERİ + MODEL + TARİF
    /// olarak üretir. Mevcut Savaşçı/Ranger/Kam'a dokunmaz.
    ///
    /// MODELLER NEDEN KODDAN: internetten hazır FBX indirip içe aktaramıyorum (ikili dosya çekme +
    /// lisans doğrulaması bu ortamda güvenilir değil). Onun yerine her sınıf için AYIRT EDİLEBİLİR
    /// silüetli, renkli, düşük-poli bir placeholder üretiliyor: gövde + baş + sınıfı belli eden bir
    /// aksesuar (balta, yay, asa, halo, kukuleta). Amaç savaş ekranını test edebilmek — kullanıcı
    /// sonradan kendi modelini prefab alanına atınca hiçbir şey bozulmaz (2026-08-12 not).
    ///
    /// Statlar yeni hasar formülüne (CombatMath) göre seçildi; menzilli sınıflar `attackRange > 1`
    /// alıyor ve bu ZATEN ÇALIŞIYOR (TurnManager menzili kontrol ediyor) — görüş hattı yok, o ayrı iş.
    /// </summary>
    public static class CharacterClassFactory
    {
        private const string ClassFolder  = "Assets/Data/Characters";
        private const string RecipeFolder = "Assets/Data/Recipes";
        private const string ModelFolder  = "Assets/Prefabs/Units";
        private const string MatFolder    = "Assets/Art/Materials/Units";

        private readonly struct Def
        {
            public readonly string Name, Lore;
            public readonly int HP, Atk, Defense, Move, Speed, Range;
            public readonly Color Color;
            public readonly Prop Prop;
            public readonly bool Mana;
            public readonly int  MaxMana;
            public readonly int  CostTas, CostDoga;

            public Def(string name, string lore, int hp, int atk, int def, int move, int speed, int range,
                       Color color, Prop prop, int costTas, int costDoga, bool mana = false, int maxMana = 0)
            { Name = name; Lore = lore; HP = hp; Atk = atk; Defense = def; Move = move; Speed = speed;
              Range = range; Color = color; Prop = prop; CostTas = costTas; CostDoga = costDoga;
              Mana = mana; MaxMana = maxMana; }
        }

        private enum Prop { Axe, Bow, Staff, Halo, Daggers }

        // Statlar: ortalama ~2.1 GE (Goblin Esdegeri) hedefiyle, her sinifa AYRI bir rol.
        // Menzilli siniflar dusuk HP ile dengeleniyor — 3 menzil buyuk avantaj.
        private static readonly Def[] Defs =
        {
            new Def("Barbar",  "Öfkesini savaş çığlığına çeviren dağ halkının savaşçısı.",
                    16, 6, 2, 3, 4, 1, new Color(0.72f, 0.30f, 0.22f), Prop.Axe,     2, 3),
            new Def("Okcu",    "Uzaktan vuran, yaklaşınca kırılgan bir avcı.",
                     9, 5, 1, 3, 6, 3, new Color(0.35f, 0.62f, 0.35f), Prop.Bow,     1, 4),
            new Def("Buyucu",  "Ateşi ve buzu çağıran, zırhsız ama yıkıcı.",
                     8, 6, 0, 3, 5, 3, new Color(0.45f, 0.38f, 0.78f), Prop.Staff,   2, 4, true, 8),
            new Def("Rahip",   "Yaraları kapatan, safları ayakta tutan şifacı.",
                    10, 3, 2, 3, 5, 2, new Color(0.88f, 0.84f, 0.62f), Prop.Halo,    1, 5, true, 10),
            new Def("Serseri", "Hızlı, sinsi, arkadan vuran. Zırha güvenmez.",
                     9, 6, 1, 5, 8, 1, new Color(0.32f, 0.34f, 0.42f), Prop.Daggers, 2, 3),
        };

        [MenuItem("TacticalRPG/Karakter - 5 Sinifi Uret (Barbar/Okcu/Buyucu/Rahip/Serseri)", false, 40)]
        public static void BuildMenu()
        {
            var made = BuildAll();
            EditorUtility.DisplayDialog("Sinif Uretimi",
                $"{made.Count} sinif hazir (veri + model + tarif).\n\n" +
                "Savas ekraninda Deployment panelinden uretip sahaya surebilirsin.\n" +
                "Modeller PLACEHOLDER — kendi FBX'ini CharacterClassData'daki 'Unit Model'\n" +
                "alanina atinca otomatik devralir.", "Tamam");
        }

        /// <summary>Sınıfları üretir (varsa dokunmaz) ve tariflerini döner.</summary>
        public static List<UnitRecipe> BuildAll()
        {
            EnsureFolder(ClassFolder); EnsureFolder(RecipeFolder);
            EnsureFolder(ModelFolder); EnsureFolder(MatFolder);

            var recipes = new List<UnitRecipe>();
            foreach (var d in Defs)
            {
                GameObject model = EnsureModel(d);
                CharacterClassData data = EnsureClass(d, model);
                recipes.Add(EnsureRecipe(d, data));
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Sinif] {Defs.Length} sinif hazir (veri + placeholder model + tarif).");
            return recipes;
        }

        // ── Sınıf verisi ─────────────────────────────────────────────────────

        private static CharacterClassData EnsureClass(in Def d, GameObject model)
        {
            string path = $"{ClassFolder}/{d.Name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterClassData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterClassData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            so.FindProperty("_className").stringValue = d.Name;
            so.FindProperty("_lore").stringValue      = d.Lore;
            so.FindProperty("_maxHP").intValue        = d.HP;
            so.FindProperty("_attack").intValue       = d.Atk;
            so.FindProperty("_defense").intValue      = d.Defense;
            so.FindProperty("_moveRange").intValue    = d.Move;
            so.FindProperty("_speed").intValue        = d.Speed;
            so.FindProperty("_attackRange").intValue  = d.Range;
            // Deploy maliyeti: 2026-08-12 hesabi — 50 birim-indirme x 3 oz = 150, butce 151.
            so.FindProperty("_deployCost").intValue   = 3;
            so.FindProperty("_isCommander").boolValue = false;
            so.FindProperty("_unitColor").colorValue  = d.Color;
            so.FindProperty("_hasManaSystem").boolValue = d.Mana;
            so.FindProperty("_maxMana").intValue        = d.MaxMana;

            // MODEL: kullanicinin kendi atadigi model EZILMEZ (placeholder yalniz bosluğu doldurur).
            var modelProp = so.FindProperty("_unitModel");
            if (modelProp.objectReferenceValue == null) modelProp.objectReferenceValue = model;
            so.FindProperty("_unitModelHeight").floatValue = 1.5f;
            // YÖN: CharacterClassData'nin VARSAYILANI (90,0,0) — eski FBX'ler icin konmus. Placeholder
            // modeller Y-yukari DIK uretiliyor; 90 derece uygulanirsa YATIYORLAR ("fuzeye benziyor")
            // ve auto-scale yatik bounds'u olcup DEVASA buyutuyor (2026-08-12 hata raporu).
            so.FindProperty("_unitModelEuler").vector3Value   = Vector3.zero;
            so.FindProperty("_unitModelYOffset").floatValue   = 0f;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static UnitRecipe EnsureRecipe(in Def d, CharacterClassData data)
        {
            string path = $"{RecipeFolder}/{d.Name}Recipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UnitRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<UnitRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            var so = new SerializedObject(recipe);
            so.FindProperty("_displayName").stringValue        = d.Name;
            so.FindProperty("_unitClass").objectReferenceValue = data;
            var cost = so.FindProperty("_cost");
            cost.arraySize = 2;
            SetAmount(cost.GetArrayElementAtIndex(0), EssenceType.Tas,  d.CostTas);
            SetAmount(cost.GetArrayElementAtIndex(1), EssenceType.Doga, d.CostDoga);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static void SetAmount(SerializedProperty el, EssenceType type, int amount)
        {
            el.FindPropertyRelative("type").enumValueIndex = (int)type;
            el.FindPropertyRelative("amount").intValue     = amount;
        }

        // ── Placeholder model ────────────────────────────────────────────────

        private static GameObject EnsureModel(in Def d)
        {
            string path = $"{ModelFolder}/Unit_{d.Name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject($"Unit_{d.Name}");
            Material body  = Mat($"Unit_{d.Name}_Body", d.Color);
            Material skin  = Mat("Unit_Skin",   new Color(0.85f, 0.72f, 0.60f));
            Material metal = Mat("Unit_Metal",  new Color(0.62f, 0.64f, 0.68f), 0.55f);
            Material wood  = Mat("Unit_Wood",   new Color(0.42f, 0.30f, 0.18f));
            Material glow  = Mat("Unit_Glow",   new Color(0.55f, 0.90f, 0.95f), 0.7f);

            // Gövde (kapsül) + baş (küre) — ayak yerde (y=0), boy ~1.5
            Prim(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.60f, 0f),
                 new Vector3(0.42f, 0.42f, 0.42f), Vector3.zero, body);
            Prim(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.10f, 0f),
                 new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, skin);

            switch (d.Prop)
            {
                case Prop.Axe:      // Barbar: kalın omuzlar + iki yüzlü balta
                    Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f),
                         new Vector3(0.70f, 0.16f, 0.34f), Vector3.zero, body);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.34f, 0.72f, 0.10f),
                         new Vector3(0.05f, 0.55f, 0.05f), new Vector3(12f, 0f, 14f), wood);
                    Prim(root.transform, PrimitiveType.Cube, new Vector3(0.42f, 1.24f, 0.12f),
                         new Vector3(0.34f, 0.26f, 0.06f), new Vector3(0f, 0f, 14f), metal);
                    break;

                case Prop.Bow:      // Okçu: yay (üç segment) + sırt sadağı
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.36f, 0.92f, 0.04f),
                         new Vector3(0.045f, 0.34f, 0.045f), new Vector3(0f, 0f, 8f), wood);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.30f, 1.24f, 0.04f),
                         new Vector3(0.04f, 0.16f, 0.04f), new Vector3(0f, 0f, 34f), wood);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.30f, 0.60f, 0.04f),
                         new Vector3(0.04f, 0.16f, 0.04f), new Vector3(0f, 0f, -34f), wood);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(-0.20f, 0.92f, -0.16f),
                         new Vector3(0.10f, 0.24f, 0.10f), new Vector3(18f, 0f, -14f), metal);
                    break;

                case Prop.Staff:    // Büyücü: sivri şapka + asa + küre
                    Cone(root.transform, new Vector3(0f, 1.26f, 0f), new Vector3(0.42f, 0.52f, 0.42f), body);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.34f, 0.78f, 0f),
                         new Vector3(0.045f, 0.72f, 0.045f), new Vector3(0f, 0f, 6f), wood);
                    Prim(root.transform, PrimitiveType.Sphere, new Vector3(0.40f, 1.52f, 0f),
                         new Vector3(0.20f, 0.20f, 0.20f), Vector3.zero, glow);
                    break;

                case Prop.Halo:     // Rahip: geniş cübbe + halka
                    Cone(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.95f, 0.78f), body);
                    Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.40f, 0f),
                         new Vector3(0.34f, 0.015f, 0.34f), Vector3.zero, glow);
                    Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.20f),
                         new Vector3(0.10f, 0.30f, 0.04f), Vector3.zero, glow);
                    break;

                case Prop.Daggers:  // Serseri: kukuleta + iki hançer, ince gövde
                    Cone(root.transform, new Vector3(0f, 1.02f, -0.04f), new Vector3(0.40f, 0.36f, 0.40f), body);
                    Prim(root.transform, PrimitiveType.Cube, new Vector3(0.30f, 0.70f, 0.10f),
                         new Vector3(0.05f, 0.30f, 0.02f), new Vector3(0f, 0f, 24f), metal);
                    Prim(root.transform, PrimitiveType.Cube, new Vector3(-0.30f, 0.70f, 0.10f),
                         new Vector3(0.05f, 0.30f, 0.02f), new Vector3(0f, 0f, -24f), metal);
                    break;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale,
                                 Vector3 euler, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition    = pos;
            go.transform.localScale       = scale;
            go.transform.localEulerAngles = euler;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // tiklama karoya gecsin
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void Cone(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            Mesh cone = SceneSetupTool.EnsureConeMesh();
            if (cone == null) return;
            var go = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.GetComponent<MeshFilter>().sharedMesh       = cone;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Material Mat(string name, Color c, float smooth = 0.1f)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))      mat.SetColor("_Color", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
