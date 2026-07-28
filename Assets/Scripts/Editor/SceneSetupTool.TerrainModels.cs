using UnityEngine;
using UnityEditor;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Prosedürel terrain karolarına 3B MODEL atar — "her karonun adına uygun bir modeli olsun"
    /// (kullanıcı isteği 2026-07-28). Üretilen haritadaki HER karo tipi artık düz renk değil, model.
    ///
    /// İKİ KAYNAK:
    ///   1. **Mevcut modeller yeniden kullanılır** (eski harita tasarımından): ova→standart karo,
    ///      az ağaçlı→ağaç1, orman→ağaç2, yüksek orman→ağaç3, göl/nehir→su, köprü→köprü, kule→kule.
    ///   2. **Eksik 4 tip koddan ÜRETİLİR** (taşlık, bol taşlık, sık orman, dağ): standart karo taban
    ///      alınıp üstüne kaya/köknar/zirve geometrisi eklenir ve gerçek `.prefab` olarak kaydedilir.
    ///
    /// NEDEN indirilen hazır model değil: FBX içe aktarma + prefab üretimi Unity'nin ÇALIŞMASINI
    /// gerektiriyor (kod tarafı bunu tetikleyemez), ayrıca lisans/boyut derdi var. Buradaki üretim
    /// tamamen offline, deterministik ve lisanssız. Daha sonra gerçek FBX'ler gelirse hiçbir şey
    /// bozulmaz: `Assets/Art/Models/Tiles/` klasörüne atılıp Tile Painter'ın "Klasörü Tara" hattından
    /// palete bağlanır, bu araç sadece BOŞ girişleri doldurur (dolu olanı EZMEZ).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string TileFolder   = "Assets/Prefabs/Grid";
        private const string MatFolder    = "Assets/Art/Materials";
        private const string MeshFolder   = "Assets/Art/Meshes";

        [MenuItem("TacticalRPG/Karo/Terrain Karolarina Model Ata", false, 30)]
        public static void AssignTerrainTileModelsMenu()
        {
            int n = AssignTerrainTileModels(force: false);
            EditorUtility.DisplayDialog("Terrain Modelleri",
                n > 0 ? $"{n} karoya model atandi.\n\nTile Painter'i acip bakabilirsin; harita bir sonraki\n" +
                        "uretimde (Play ya da TAM KURULUM) modelli gelir."
                      : "Tum terrain karolarinin modeli zaten atanmis (hicbiri ezilmedi).",
                "Tamam");
        }

        /// <summary>Terrain karolarının prefab'larını palete atar. <paramref name="force"/> false ise
        /// ZATEN MODELİ OLAN girişe DOKUNMAZ (kullanıcının kendi atadığı model ezilmesin).</summary>
        public static int AssignTerrainTileModels(bool force)
        {
            var grid = FindComponentAnywhere<HexGridManager>();
            TilePaletteSO palette = grid != null ? grid.TilePalette : null;
            if (palette == null)
                palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>("Assets/Data/Map/TilePalette.asset");
            if (palette == null) { Debug.LogError("[Model] TilePalette bulunamadi."); return 0; }

            // ── 1) Eksik tipler icin prefab uret (varsa yeniden uretmez) ─────
            GameObject rocky1 = EnsureRockyTile("Tile_taslik_ova",     2, 0.22f, seed: 11);
            GameObject rocky2 = EnsureRockyTile("Tile_bol_taslik_ova", 5, 0.28f, seed: 22);
            GameObject dense  = EnsureConiferTile("Tile_sik_orman",    5, seed: 33);
            GameObject mount  = EnsureMountainTile("Tile_dag");

            // ── 2) id → prefab eslemesi ──────────────────────────────────────
            // Yurunur karolarda yuzey yuksekligi ELLE TileHeight: birim karonun USTUNDE durur,
            // kayanin/agacin tepesinde degil.
            var map = new (string id, GameObject prefab, float surface)[]
            {
                (TerrainGenerator.OvaId,              Load("Tile_default"), 0f),
                (TerrainGenerator.TaslikOvaId,        rocky1,               HexMetrics.TileHeight),
                (TerrainGenerator.BolTaslikOvaId,     rocky2,               HexMetrics.TileHeight),
                (TerrainGenerator.AzAgacliOvaId,      Load("Tile_agac1"),   HexMetrics.TileHeight),
                (TerrainGenerator.OrmanId,            Load("Tile_agac2"),   HexMetrics.TileHeight),
                (TerrainGenerator.NadirYuksekOrmanId, Load("Tile_agac3"),   HexMetrics.TileHeight),
                (TerrainGenerator.SikOrmanId,         dense,                HexMetrics.TileHeight),
                (TerrainGenerator.DagId,              mount,                HexMetrics.TileHeight),
                (TerrainGenerator.GolId,              Load("Tile_su"),      0f),
                (TerrainGenerator.NehirId,            Load("Tile_su"),      0f),
            };

            var so    = new SerializedObject(palette);
            var tiles = so.FindProperty("tiles");
            int assigned = 0;

            foreach (var (id, prefab, surface) in map)
            {
                if (prefab == null) { Debug.LogWarning($"[Model] '{id}' icin prefab yok, atlandi."); continue; }

                for (int i = 0; i < tiles.arraySize; i++)
                {
                    SerializedProperty e = tiles.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("id").stringValue != id) continue;

                    SerializedProperty p = e.FindPropertyRelative("prefab");
                    if (!force && p.objectReferenceValue != null) break;   // kullanicinin modeli EZILMEZ

                    p.objectReferenceValue = prefab;
                    e.FindPropertyRelative("surfaceHeightOverride").floatValue = surface;
                    assigned++;
                    break;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            if (assigned > 0) Debug.Log($"[Model] {assigned} terrain karosuna model atandi.");
            return assigned;
        }

        private static GameObject Load(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{TileFolder}/{name}.prefab");

        // ─────────────────────────────────────────────────────────────────────
        // Uretilen karolar — standart karo TABAN + ustune geometri
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Standart karonun bir kopyasını alır (footprint/pivot zaten doğru).</summary>
        private static GameObject NewTileBase(string name)
        {
            GameObject baseTile = Load("Tile_default");
            if (baseTile == null) { Debug.LogError("[Model] Tile_default.prefab yok."); return null; }
            GameObject go = Object.Instantiate(baseTile);
            go.name = name;
            return go;
        }

        /// <summary>Süslemeyi karoya ekler; collider'ı KALDIRIR (tıklama karoya geçsin).</summary>
        private static GameObject AddDecor(Transform parent, PrimitiveType type, Vector3 pos,
                                           Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject d = GameObject.CreatePrimitive(type);
            d.transform.SetParent(parent, false);
            d.transform.localPosition = pos;
            d.transform.localScale    = scale;
            d.transform.localRotation = rot;
            Collider col = d.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            d.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return d;
        }

        private static GameObject SaveTile(GameObject go, string name)
        {
            EnsureFolder(TileFolder);
            string path = $"{TileFolder}/{name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>Taşlık ova: karonun üstüne dağınık kaya blokları (deterministik yerleşim).</summary>
        private static GameObject EnsureRockyTile(string name, int rockCount, float rockSize, int seed)
        {
            GameObject existing = Load(name);
            if (existing != null) return existing;

            GameObject go = NewTileBase(name);
            if (go == null) return null;
            Material rockMat = EnsureMaterial("Terrain_Rock", new Color(0.56f, 0.54f, 0.50f));
            var rnd = new System.Random(seed);

            for (int i = 0; i < rockCount; i++)
            {
                float ang = (float)(rnd.NextDouble() * System.Math.PI * 2.0);
                float rad = 0.15f + (float)rnd.NextDouble() * 0.45f;
                float s   = rockSize * (0.7f + (float)rnd.NextDouble() * 0.7f);

                AddDecor(go.transform, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(ang) * rad, HexMetrics.TileHeight + s * 0.35f, Mathf.Sin(ang) * rad),
                    new Vector3(s * 1.3f, s * 0.85f, s),                       // yassı = kaya hissi
                    Quaternion.Euler((float)rnd.NextDouble() * 30f,
                                     (float)rnd.NextDouble() * 360f,
                                     (float)rnd.NextDouble() * 30f),
                    rockMat);
            }
            return SaveTile(go, name);
        }

        /// <summary>Sık orman: karonun üstüne birbirine yakın köknarlar (gövde + iki koni katman).</summary>
        private static GameObject EnsureConiferTile(string name, int treeCount, int seed)
        {
            GameObject existing = Load(name);
            if (existing != null) return existing;

            GameObject go = NewTileBase(name);
            if (go == null) return null;
            Material bark   = EnsureMaterial("Terrain_Bark",    new Color(0.30f, 0.21f, 0.13f));
            Material needle = EnsureMaterial("Terrain_Conifer", new Color(0.13f, 0.31f, 0.18f));
            Mesh cone = EnsureConeMesh();
            var rnd = new System.Random(seed);

            for (int i = 0; i < treeCount; i++)
            {
                float ang = (float)(i / (float)treeCount * System.Math.PI * 2.0 + rnd.NextDouble() * 0.6);
                float rad = i == 0 ? 0f : 0.30f + (float)rnd.NextDouble() * 0.25f;
                float h   = 0.75f + (float)rnd.NextDouble() * 0.45f;
                var baseP = new Vector3(Mathf.Cos(ang) * rad, HexMetrics.TileHeight, Mathf.Sin(ang) * rad);

                AddDecor(go.transform, PrimitiveType.Cylinder,
                    baseP + new Vector3(0f, h * 0.14f, 0f),
                    new Vector3(0.07f, h * 0.14f, 0.07f), Quaternion.identity, bark);

                AddCone(go.transform, cone, baseP + new Vector3(0f, h * 0.24f, 0f),
                        new Vector3(0.34f, h * 0.55f, 0.34f), needle);
                AddCone(go.transform, cone, baseP + new Vector3(0f, h * 0.60f, 0f),
                        new Vector3(0.25f, h * 0.45f, 0.25f), needle);
            }
            return SaveTile(go, name);
        }

        /// <summary>Dağ: geniş bir ana zirve + iki küçük yan zirve.</summary>
        private static GameObject EnsureMountainTile(string name)
        {
            GameObject existing = Load(name);
            if (existing != null) return existing;

            GameObject go = NewTileBase(name);
            if (go == null) return null;
            Material rockMat = EnsureMaterial("Terrain_Mountain", new Color(0.44f, 0.42f, 0.41f));
            Material snowMat = EnsureMaterial("Terrain_Snow",     new Color(0.90f, 0.92f, 0.95f));
            Mesh cone = EnsureConeMesh();

            AddCone(go.transform, cone, new Vector3(0f, HexMetrics.TileHeight, 0f),
                    new Vector3(1.35f, 1.30f, 1.35f), rockMat);
            AddCone(go.transform, cone, new Vector3(0f, HexMetrics.TileHeight + 0.98f, 0f),
                    new Vector3(0.42f, 0.34f, 0.42f), snowMat);          // karlı zirve
            AddCone(go.transform, cone, new Vector3(-0.45f, HexMetrics.TileHeight, 0.35f),
                    new Vector3(0.70f, 0.62f, 0.70f), rockMat);
            AddCone(go.transform, cone, new Vector3(0.48f, HexMetrics.TileHeight, -0.30f),
                    new Vector3(0.58f, 0.50f, 0.58f), rockMat);
            return SaveTile(go, name);
        }

        private static void AddCone(Transform parent, Mesh cone, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.GetComponent<MeshFilter>().sharedMesh        = cone;
            go.GetComponent<MeshRenderer>().sharedMaterial  = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Paylasilan asset'ler
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Unity'de koni primitifi YOK → 8 kenarlı düşük-poli koni üretip asset olarak
        /// kaydeder (taban y=0, tepe y=1; ölçekle boyutlandırılır).</summary>
        private static Mesh EnsureConeMesh()
        {
            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/ConeMesh.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const int sides = 8;
            var verts = new Vector3[sides + 2];
            verts[0] = Vector3.zero;          // taban merkezi
            verts[1] = Vector3.up;            // tepe
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts[2 + i] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
            }

            var tris = new System.Collections.Generic.List<int>(sides * 6);
            for (int i = 0; i < sides; i++)
            {
                int cur = 2 + i, next = 2 + (i + 1) % sides;
                tris.Add(1);   tris.Add(next); tris.Add(cur);   // yan yüz
                tris.Add(0);   tris.Add(cur);  tris.Add(next);  // taban
            }

            var mesh = new Mesh { name = "ConeMesh", vertices = verts, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>URP/Lit materyali üretir (varsa mevcut olanı döner).</summary>
        private static Material EnsureMaterial(string name, Color color)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);  // URP
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);      // Built-in yedek
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
