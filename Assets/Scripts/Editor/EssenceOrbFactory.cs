using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// HARİTADAKİ ÖZ KÜRELERİNİN MODELLERİ — beş biçim, hepsi prosedürel üretilir
    /// (kullanıcı isteği 2026-08-17: "hazır model bul ya da basitçe kendin üret").
    ///
    /// TASARIM: her küre AYNI üç katmandan kurulur — dıştan yarı saydam bir kabuk, ortada bir kor,
    /// aralarında dönen parçacıklar. Ayrım PARÇACIĞIN BİÇİMİNDEN ve hareketinden gelir:
    ///   • Alev   — yukarı incelen diller, hızlı titrer ve süzülür (ateş özü)
    ///   • Su     — yassı damlalar, ağır ve yumuşak döner
    ///   • Toz    — çok sayıda ince zerre, hızlı ve düzensiz (toprak özü)
    ///   • Kristal— köşeli kırıklar, ağır ağır takla atar (taş özü)
    ///   • Yaprak — yassı yapraklar, savrularak döner (doğa özü)
    ///
    /// RENK PREFABDA YOK: hepsi tek materyal setini paylaşır, rengi çalışma zamanında
    /// <see cref="EssenceOrbVisual"/> MaterialPropertyBlock ile yazar (öz türünün rengi
    /// EssenceConfig'ten gelir). Böylece renk değiştirmek için prefab yeniden üretilmez.
    ///
    /// Üretim İDEMPOTENT: var olan prefabı yalnız <c>force</c> ile ezer (CLAUDE.md §9.1).
    /// </summary>
    public static class EssenceOrbFactory
    {
        public const string PrefabFolder = "Assets/Prefabs/Essence";
        public const string MatFolder    = "Assets/Art/Materials/Essence";

        private static Shader LitShader =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Shader UnlitShader =>
            Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? LitShader;

        /// <summary>Bir biçimin küre karakteri — beş ayrı animasyon sınıfı yerine tek parametre seti.</summary>
        private struct Recipe
        {
            public int     MoteCount;
            public Vector3 MoteScale;      // parçacığın boyutu (yerel birim)
            public float   MoteRadius;     // kabuk içindeki yörünge yarıçapı
            public PrimitiveType MoteShape;
            public float   SpinSpeed, BobAmplitude, BobSpeed;
            public float   OrbitSpeed, TumbleSpeed, Flicker, Rise;
            public float   PulsePeriod;
            public Vector2 PulseRange;
        }

        private static Recipe RecipeFor(EssenceOrbShape shape) => shape switch
        {
            // Ateş: az sayıda uzun dil, çok titrek, sürekli yukarı süzülür.
            EssenceOrbShape.Alev => new Recipe
            {
                MoteCount = 6, MoteScale = new Vector3(0.15f, 0.26f, 0.15f), MoteRadius = 0.21f,
                MoteShape = PrimitiveType.Capsule,
                SpinSpeed = 18f, BobAmplitude = 0.06f, BobSpeed = 0.75f,
                OrbitSpeed = 0.28f, TumbleSpeed = 0f, Flicker = 0.55f, Rise = 0.85f,
                PulsePeriod = 0.9f, PulseRange = new Vector2(0.85f, 2.1f)
            },

            // Su: yassı damlalar, ağır ve yumuşak — nabız yavaş.
            EssenceOrbShape.Su => new Recipe
            {
                MoteCount = 5, MoteScale = new Vector3(0.24f, 0.12f, 0.24f), MoteRadius = 0.26f,
                MoteShape = PrimitiveType.Sphere,
                SpinSpeed = 14f, BobAmplitude = 0.05f, BobSpeed = 0.4f,
                OrbitSpeed = 0.2f, TumbleSpeed = 22f, Flicker = 0.1f, Rise = 0f,
                PulsePeriod = 2.6f, PulseRange = new Vector2(0.7f, 1.4f)
            },

            // Toprak: kalabalık ince toz, hızlı ve düzensiz.
            EssenceOrbShape.Toz => new Recipe
            {
                MoteCount = 10, MoteScale = new Vector3(0.09f, 0.09f, 0.09f), MoteRadius = 0.29f,
                MoteShape = PrimitiveType.Sphere,
                SpinSpeed = 46f, BobAmplitude = 0.04f, BobSpeed = 0.6f,
                OrbitSpeed = 0.8f, TumbleSpeed = 0f, Flicker = 0.42f, Rise = 0.4f,
                PulsePeriod = 1.4f, PulseRange = new Vector2(0.75f, 1.6f)
            },

            // Taş: az sayıda köşeli kırık, ağır ağır takla atar.
            EssenceOrbShape.Kristal => new Recipe
            {
                MoteCount = 5, MoteScale = new Vector3(0.16f, 0.16f, 0.16f), MoteRadius = 0.25f,
                MoteShape = PrimitiveType.Cube,
                SpinSpeed = 22f, BobAmplitude = 0.05f, BobSpeed = 0.35f,
                OrbitSpeed = 0.16f, TumbleSpeed = 34f, Flicker = 0.05f, Rise = 0f,
                PulsePeriod = 3.0f, PulseRange = new Vector2(0.65f, 1.3f)
            },

            // Doğa: yassı yapraklar, savrularak döner ve hafifçe yükselir.
            _ => new Recipe
            {
                MoteCount = 6, MoteScale = new Vector3(0.21f, 0.03f, 0.13f), MoteRadius = 0.27f,
                MoteShape = PrimitiveType.Cube,
                SpinSpeed = 28f, BobAmplitude = 0.055f, BobSpeed = 0.5f,
                OrbitSpeed = 0.3f, TumbleSpeed = 95f, Flicker = 0.12f, Rise = 0.22f,
                PulsePeriod = 2.2f, PulseRange = new Vector2(0.7f, 1.5f)
            }
        };

        // ── Menü / batch ─────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Oz - Kure Modellerini Kur (5 bicim)", false, 32)]
        public static void BuildAllMenu()
        {
            int n = BuildAll(force: true);
            EditorUtility.DisplayDialog("Oz Kureleri",
                $"{n} oz kuresi uretildi: {PrefabFolder}\n\n" +
                "Alev · Su · Toz · Kristal · Yaprak.\n" +
                "Renk prefabda DEGIL — EssenceConfig'teki tur rengi calisma zamaninda uygulanir.\n\n" +
                "Haritada gormek icin: TAM KURULUM (ya da 'Bolum - Tek Haritali Dunya Kur') calistir,\n" +
                "sonra Play'e bas — ozlu karolar boyanir, konturlanir ve ustlerinde kure doner.",
                "Tamam");
        }

        /// <summary>Batch girişi: <c>-executeMethod TacticalRPG.Editor.EssenceOrbFactory.BuildAllBatch</c></summary>
        public static void BuildAllBatch() => BuildAll(force: true);

        /// <summary>Beş küre prefabını üretir (idempotent). Üretilen/var olan sayısını döner.</summary>
        public static int BuildAll(bool force)
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MatFolder);

            int n = 0;
            foreach (EssenceOrbShape shape in System.Enum.GetValues(typeof(EssenceOrbShape)))
                if (EnsurePrefab(shape, force) != null) n++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[Oz] {n} kure prefabi hazir → {PrefabFolder}");
            return n;
        }

        /// <summary>Bu biçimin prefabı (yoksa null — çağıran yedek küreye düşer).</summary>
        public static GameObject PrefabFor(EssenceOrbShape shape) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(shape));

        /// <summary>Öz karosunun kontur halkasının materyali (kendinden ışıklı, gölgesiz).</summary>
        public static Material RingMaterial()
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/EssenceRing.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(UnlitShader) { name = "EssenceRing", enableInstancing = true };
            SetColor(mat, Color.white);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Özü sökülmüş karonun kurak yüzeyi + çatlakları için SAYDAM materyal.
        /// Saydam olmak zorunda: kuraklık alfası 0'dan hedefe giderek "belirir" ve altındaki
        /// zemini bir miktar göstermeye devam eder.</summary>
        public static Material DrainMaterial()
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/EssenceDrain.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(UnlitShader) { name = "EssenceDrain" };
            MakeTransparent(mat, 1f);        // alfa çalışma zamanında MPB ile sürülür
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static string PrefabPath(EssenceOrbShape shape) => $"{PrefabFolder}/Oz_{shape}.prefab";

        // ── Üretim ───────────────────────────────────────────────────────────

        private static GameObject EnsurePrefab(EssenceOrbShape shape, bool force)
        {
            string path = PrefabPath(shape);
            if (!force)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return existing;
            }

            Recipe r = RecipeFor(shape);
            var root = new GameObject($"Oz_{shape}");

            // Kabuk: yarı saydam dış küre — parçacıklar "kor içinde" görünsün.
            Sphere(root.transform, EssenceOrbVisual.ShellName, Vector3.zero,
                   Vector3.one, ShellMaterial());

            // Kor: ortadaki parlak çekirdek.
            Sphere(root.transform, EssenceOrbVisual.CoreName, Vector3.zero,
                   Vector3.one * 0.4f, CoreMaterial());

            // Parçacıklar: kabuğun içine altın-açı (2.39996 rad) ile dağıtılır → hiçbiri
            // diğeriyle aynı hizaya düşmez, tek bir halkada dizilmiş gibi durmaz.
            for (int i = 0; i < r.MoteCount; i++)
            {
                float a = i * 2.399963f;
                float y = Mathf.Lerp(-0.18f, 0.18f, (i + 0.5f) / r.MoteCount);
                var pos = new Vector3(Mathf.Cos(a) * r.MoteRadius, y, Mathf.Sin(a) * r.MoteRadius);
                Prim(root.transform, EssenceOrbVisual.MotePrefix + i, r.MoteShape, pos,
                     r.MoteScale, MoteMaterial());
            }

            var orb = root.AddComponent<EssenceOrbVisual>();
            ApplyRecipe(orb, shape, r);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyRecipe(EssenceOrbVisual orb, EssenceOrbShape shape, Recipe r)
        {
            var so = new SerializedObject(orb);
            so.FindProperty("_shape").enumValueIndex   = (int)shape;
            so.FindProperty("_spinSpeed").floatValue   = r.SpinSpeed;
            so.FindProperty("_bobAmplitude").floatValue= r.BobAmplitude;
            so.FindProperty("_bobSpeed").floatValue    = r.BobSpeed;
            so.FindProperty("_orbitSpeed").floatValue  = r.OrbitSpeed;
            so.FindProperty("_tumbleSpeed").floatValue = r.TumbleSpeed;
            so.FindProperty("_flicker").floatValue     = r.Flicker;
            so.FindProperty("_rise").floatValue        = r.Rise;
            so.FindProperty("_pulsePeriod").floatValue = r.PulsePeriod;
            so.FindProperty("_pulseRange").vector2Value= r.PulseRange;
            so.ApplyModifiedProperties();
        }

        private static void Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
            => Prim(parent, name, PrimitiveType.Sphere, pos, scale, mat);

        private static GameObject Prim(Transform parent, string name, PrimitiveType type,
                                       Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;

            // Collider YOK: küre karonun üstünde duruyor, tıklamayı yutup yürümeyi engellemesin.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial    = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows    = false;
            return go;
        }

        // ── Materyaller (tüm türler paylaşır; renk runtime'da MPB ile) ───────

        private static Material CoreMaterial() => EmissiveMat("EssenceCore", 0.85f, 1f);
        private static Material MoteMaterial() => EmissiveMat("EssenceMote", 0.55f, 1f);

        /// <summary>Yarı saydam kabuk — içindeki parçacıklar görünsün diye.</summary>
        private static Material ShellMaterial()
        {
            string path = $"{MatFolder}/EssenceShell.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(LitShader) { name = "EssenceShell", enableInstancing = true };
            MakeTransparent(mat, 0.22f);
            EnableEmission(mat);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material EmissiveMat(string name, float smoothness, float alpha)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(LitShader) { name = name, enableInstancing = true };
            SetColor(mat, new Color(1f, 1f, 1f, alpha));
            EnableEmission(mat);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>URP/Lit'i saydam moda alır. Yalnız rengin alfasını düşürmek YETMEZ —
        /// yüzey tipi, harmanlama ve render sırası da ayarlanmalı, yoksa materyal opak kalır.</summary>
        private static void MakeTransparent(Material mat, float alpha)
        {
            SetColor(mat, new Color(1f, 1f, 1f, alpha));

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);

            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Emisyon keyword'ünü açar. Keyword kapalıysa MaterialPropertyBlock ile yazılan
        /// <c>_EmissionColor</c> HİÇ ETKİ ETMEZ — kürenin parlaması tam olarak buna bağlı.</summary>
        private static void EnableEmission(Material mat)
        {
            if (!mat.HasProperty("_EmissionColor")) return;
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", Color.white);
        }

        private static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
