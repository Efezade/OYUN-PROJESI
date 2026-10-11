using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using Object = UnityEngine.Object;

namespace TacticalRPG.Editor
{
    public static class BiomeArtSetup
    {
        public const string Models = "Assets/Art/Models/Tiles/Biomes";
        public const string Prefabs = "Assets/Prefabs/Grid/Biomes";
        public const string Materials = "Assets/Art/Materials/Biomes";
        public const string ConfigPath = "Assets/Data/Config/BayterekRoots.asset";
        public const string BoundaryConfigPath = "Assets/Data/Config/BiomeBoundaries.asset";
        public const string TerrainStylePath = "Assets/Data/Config/BiomeTerrainStyle.asset";
        private const string PalettePath = "Assets/Data/Map/TilePalette.asset";
        [Serializable] private class Manifest { public int version; public Item[] entries; }
        [Serializable] private class Item { public string id; public bool landmark; public string[] textures; }

        [MenuItem("TacticalRPG/Biyomlar - Modeller ve Bayterek Koklerini Kur", false, 32)]
        public static void Install()
        {
            EnsureArt(); SceneSetupTool.SetupBiomeWorld();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
        public static void EnsureArt()
        {
            string manifestPath = Models + "/manifest.json";
            if (!File.Exists(manifestPath)) return;
            Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            // Explicit settings also repair newly copied textures with incomplete metadata.
            foreach (string texturePath in Directory.GetFiles(Models, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(texturePath.Replace('\\', '/')) as TextureImporter;
                if (importer == null || (importer.textureShape == TextureImporterShape.Texture2D &&
                    importer.textureType == TextureImporterType.Default)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.SaveAndReimport();
            }
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            if (palette == null) throw new InvalidOperationException("TilePalette missing");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            // Ground must exist before the multi-tile Bayterek prefab is built.
            foreach (var item in manifest.entries.OrderBy(e => e.landmark))
            {
                var prefab = EnsurePrefab(item);
                if (prefab == null) throw new InvalidOperationException("Biome model missing: " + item.id);
                Bind(palette, item.id, prefab);
            }
            EnsureVillageFoundation();
            // Secondary tiles reuse the matching theme's art instead of old generic cubes.
            foreach (var pair in new[] {
                ("bataklik_otu","sazlik"), ("batik_tas","bataklik__adacik"), (TileCatalog.Mandragora,"sazlik"),
                ("yuksek_orman","kadim_orman"), ("dev_agac","kadim_orman"), ("kehribar_damari","kok_agi__kehribar"),
                ("mantar_ormani","kodama_yuvasi"), ("dikenli_calilik","kurumus_koru"),
                ("karanlik_orman","sik_orman"), ("cakil_yatagi","taslik_ova") })
                Bind(palette, pair.Item1, AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{pair.Item2}.prefab"));
            EditorUtility.SetDirty(palette);
            EnsureConfig(); EnsureTerrainStyle(); UpgradeTerrainMaterials(); AssetDatabase.SaveAssets();
        }
        private static void Bind(TilePaletteSO palette, string id, GameObject prefab)
        {
            if (prefab == null) return;
            var split = id.Split(new[] { "__" }, StringSplitOptions.None);
            if (split[0] == "yosun_tarla") split[0] = TileCatalog.YosunTarla;
            var cat = TileCatalog.Get(split[0]);
            var entry = palette.GetById(split[0]);
            if (entry == null)
            {
                if (cat == null) return;
                entry = new TilePaletteSO.TileEntry { id = cat.Id, displayName = cat.Name,
                    isWalkable = cat.Walkable, editorColor = new Color(cat.R,cat.G,cat.B) };
                palette.tiles.Add(entry);
            }
            string current = AssetDatabase.GetAssetPath(entry.prefab);
            // Keep personally authored art and future replacements. Only migrate old factory placeholders.
            bool aliasUpgrade = current.StartsWith(Prefabs + "/", StringComparison.Ordinal) &&
                split.Length == 1 && Path.GetFileNameWithoutExtension(current) != split[0] && split[0] != TileCatalog.YosunTarla;
            bool replace = entry.prefab == null || current.StartsWith("Assets/Prefabs/Grid/Terrain/", StringComparison.Ordinal) ||
                current.StartsWith("Assets/Prefabs/Grid/Augments/", StringComparison.Ordinal) || aliasUpgrade ||
                new[]{"Tile_han","Tile_magara","Tile_kamp","Tile_gorev_alani","Tile_gorev_tamam"}
                    .Any(old => current == "Assets/Prefabs/Grid/" + old + ".prefab");
            if (split.Length == 1)
            {
                if (replace)
                {
                    entry.prefab = prefab; entry.surfaceHeightOverride = split[0] == "kopru" ? .635f : .3158f;
                    if (cat != null) entry.isWalkable = cat.Walkable;
                }
            }
            else if (replace || current.StartsWith(Prefabs + "/", StringComparison.Ordinal))
            {
                if (!entry.variants.Any(v => v.name == split[1]))
                    entry.variants.Add(new TilePaletteSO.VisualVariant { name = split[1], prefab = prefab, weight = 1 });
            }
        }
        private static GameObject EnsurePrefab(Item item)
        {
            string path = $"{Prefabs}/{item.id}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            string fbxPath = $"{Models}/{item.id}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return null;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject(item.id);
            try
            {
                if (item.id == TileCatalog.Bayterek)
                {
                    var ground = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/kok_agi.prefab");
                    var floor = (GameObject)PrefabUtility.InstantiatePrefab(ground);
                    floor.transform.SetParent(root.transform, false);
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);
                var renderers = inst.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                if (!item.landmark)
                {
                    // FBX is exported upright, in metres; do not use the legacy importer's X flip.
                    float scale = 2f / Mathf.Max(bounds.size.x, bounds.size.z);
                    inst.transform.localScale = Vector3.one * scale;
                    inst.transform.localPosition = Vector3.up * (-bounds.min.y * scale);
                }
                foreach (var r in renderers)
                {
                    string textureName = item.landmark ? r.gameObject.name.Split('.')[0] : item.id;
                    r.sharedMaterial = EnsureMaterial(textureName, item.landmark && textureName.Contains("Tac"));
                }
                if (!item.landmark)
                {
                    const string colliderPath = Prefabs + "/HexCollider.asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(colliderPath);
                    if (mesh == null) { mesh = HexMetrics.CreateHexMesh(1f); AssetDatabase.CreateAsset(mesh, colliderPath); }
                    root.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
                else if (item.id == TileCatalog.Bayterek)
                {
                    var visibility = root.AddComponent<BiomeLandmarkVisibility>();
                    var so = new SerializedObject(visibility); var list = so.FindProperty("_canopy");
                    var crowns = renderers.Where(r => r.name.Contains("Tac")).ToArray();
                    list.arraySize = crowns.Length;
                    for (int i=0;i<crowns.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue = crowns[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var appearance = root.AddComponent<BiomeTileAppearance>();
                var app = new SerializedObject(appearance);
                app.FindProperty("_groundColor").colorValue = GroundColor(item.id);
                app.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static Color GroundColor(string id)
        {
            // Average endpoints of the authored ground textures (not the white tint of a baked atlas).
            if (id.StartsWith("halka") || id == "fener_yolu" || id == "dal_ev") return new(.54f,.395f,.215f);
            if (id.StartsWith("batak") || id == "sazlik") return new(.22f,.255f,.195f);
            if (id.StartsWith("solgun") || id == "kurumus_koru" || id == "zar_yirtigi") return new(.34f,.315f,.21f);
            if (id.StartsWith("kok_sirti") || id == "agac_kovugu" || id == "ruh_tapinagi" || id == "killi_yamac" || id == "hus_korusu" || id == "kaya_yigini") return new(.46f,.31f,.20f);
            if (id.StartsWith("yosun") || id == "dusmus_dev" || id == "kodama_yuvasi" || id == "kadim_orman") return new(.265f,.41f,.28f);
            return new(.255f,.37f,.185f);
        }
        private static void EnsureVillageFoundation()
        {
            string path = Prefabs + "/halka_meclisi.prefab";
            var foundation = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/halka_kutuk_aniti.prefab");
            if (foundation == null || !File.Exists(path)) return;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.transform.Find("Dev_Kutuk_Tabani") != null) return;
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(foundation, contents.scene);
                obj.name = "Dev_Kutuk_Tabani"; obj.transform.SetParent(contents.transform, false);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static Material EnsureMaterial(string name, bool transparent = false)
        {
            string path = $"{Materials}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Models}/{name}_color.png");
            if (texture == null) throw new InvalidOperationException("Baked texture missing: " + name);
            mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Cull", 0);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            AssetDatabase.CreateAsset(mat,path); return mat;
        }
        public static QuestRootConfigSO EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<QuestRootConfigSO>(ConfigPath);
            if (config != null) return config;
            string path = Materials + "/GorevKoku.mat";
            var bark = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (bark == null)
            {
                bark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Bayterek Gorev Koku" };
                bark.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "/canli_kabuk.png"));
                bark.SetColor("_BaseColor", Color.white); bark.SetFloat("_Smoothness", .16f);
                AssetDatabase.CreateAsset(bark,path);
            }
            config = ScriptableObject.CreateInstance<QuestRootConfigSO>();
            var so = new SerializedObject(config); so.FindProperty("_barkMaterial").objectReferenceValue = bark;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(config, ConfigPath); return config;
        }
        public static BiomeBoundaryConfigSO EnsureBoundaryConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BiomeBoundaryConfigSO>(BoundaryConfigPath);
            if (config != null) return config;
            string path = Materials + "/BiyomGecisi.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("TacticalRPG/Biome Boundary");
                if (shader == null) throw new InvalidOperationException("Biome boundary shader missing");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            config = ScriptableObject.CreateInstance<BiomeBoundaryConfigSO>();
            var so = new SerializedObject(config); so.FindProperty("_material").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(config, BoundaryConfigPath); return config;
        }
        public static BiomeTerrainStyleSO EnsureTerrainStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<BiomeTerrainStyleSO>(TerrainStylePath);
            if (style != null) return style;
            style = ScriptableObject.CreateInstance<BiomeTerrainStyleSO>();
            var so = new SerializedObject(style);
            so.FindProperty("_shader").objectReferenceValue = Shader.Find("TacticalRPG/Biome Terrain");
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(style,TerrainStylePath);
            var boundary=AssetDatabase.LoadAssetAtPath<BiomeBoundaryConfigSO>(BoundaryConfigPath);
            if(boundary!=null)
            {
                var bs=new SerializedObject(boundary);bs.FindProperty("_halfWidth").floatValue=.10f;
                bs.FindProperty("_opacity").floatValue=.45f;bs.ApplyModifiedPropertiesWithoutUndo();
            }
            return style;
        }
        private static void UpgradeTerrainMaterials()
        {
            var shader=Shader.Find("TacticalRPG/Biome Terrain");
            if(shader==null)throw new InvalidOperationException("Biome terrain shader missing");
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Materials}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material==null || material.name=="Bayterek_Tac" || !material.HasProperty("_BaseMap") || material.mainTexture==null ||
                    !AssetDatabase.GetAssetPath(material.mainTexture).EndsWith("_color.png",StringComparison.Ordinal))continue;
                if(material.shader==shader)continue;
                var texture=material.mainTexture;
                material.shader=shader;material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
                material.SetFloat("_UseHeightMask",0);material.SetFloat("_GroundReference",.095f);
                EditorUtility.SetDirty(material);
            }
        }
        public static void BatchInstall()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");
            Install(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Biome] INSTALL COMPLETE");
        }
    }

    public static partial class SceneSetupTool
    {
        public static void SetupBiomeWorld()
        {
            BiomeArtSetup.EnsureArt();
            var map = FindComponentAnywhere<ChapterMapGenerator>();
            if (map == null) return;
            var roots = Ensure<BayterekQuestRoots>(map.gameObject);
            var config = BiomeArtSetup.EnsureConfig();
            var so = new SerializedObject(roots);
            Set(so,"_map",map);Set(so,"_grid",FindComponentAnywhere<HexGridManager>());
            Set(so,"_nodes",FindComponentAnywhere<ChapterNodeManager>());Set(so,"_player",FindComponentAnywhere<PlayerController>());
            Set(so,"_state",FindComponentAnywhere<GameStateManager>());Set(so,"_fog",FindComponentAnywhere<FogOfWarManager>());
            Set(so,"_collapse",FindComponentAnywhere<MapCollapseManager>());
            if (so.FindProperty("_config").objectReferenceValue == null) Set(so,"_config",config);
            so.ApplyModifiedPropertiesWithoutUndo();
            var boundaries = Ensure<BiomeBoundaryVisuals>(map.gameObject);
            var bs = new SerializedObject(boundaries);
            Set(bs,"_map",map); Set(bs,"_grid",FindComponentAnywhere<HexGridManager>());
            Set(bs,"_fog",FindComponentAnywhere<FogOfWarManager>());
            Set(bs,"_collapse",FindComponentAnywhere<MapCollapseManager>());
            Set(bs,"_terrainStyle",BiomeArtSetup.EnsureTerrainStyle());
            if (bs.FindProperty("_config").objectReferenceValue == null)
                Set(bs,"_config",BiomeArtSetup.EnsureBoundaryConfig());
            bs.ApplyModifiedPropertiesWithoutUndo();
            var terrain = Ensure<BiomeTerrainStyling>(map.gameObject);
            var ts = new SerializedObject(terrain);
            Set(ts,"_grid",FindComponentAnywhere<HexGridManager>()); Set(ts,"_map",map);
            if(ts.FindProperty("_style").objectReferenceValue==null)Set(ts,"_style",BiomeArtSetup.EnsureTerrainStyle());
            ts.ApplyModifiedPropertiesWithoutUndo();
            var minimap = FindComponentAnywhere<MinimapRenderer>();
            if (minimap != null)
            {
                var m = new SerializedObject(minimap);
                if (m.FindProperty("_roots").objectReferenceValue == null) Set(m,"_roots",config);
                if (m.FindProperty("_terrainStyle").objectReferenceValue == null) Set(m,"_terrainStyle",BiomeArtSetup.EnsureTerrainStyle());
                Set(m,"_map",map);
                m.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
