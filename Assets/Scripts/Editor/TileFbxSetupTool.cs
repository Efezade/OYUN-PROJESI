using System.Linq;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Tasarımcı FBX karosunu oyunun beklediği hex boyutuna otomatik uyarlar:
    /// ölçek (footprint), eksen (kalınlık → Y/üst) ve pivot (alt-orta) düzeltir,
    /// düzeltilmiş bir prefab kaydeder ve TilePalette'te SADECE "kopru" tipine atar.
    /// Diğer tipler (lav, su, çimen...) renkli placeholder kalır → Tile Painter'da
    /// hangi karoyu nereye boyayacağını sen seçersin.
    ///
    /// Boyutlar runtime'da Renderer.bounds ile ÖLÇÜLÜR — FBX import eksenine bağlı kalmaz.
    /// </summary>
    public static class TileFbxSetupTool
    {
        private const string ModelsFolder = "Assets/Art/Models";
        private const string PrefabPath   = "Assets/Prefabs/Grid/Tile_KopruKaro.prefab";
        private const string PalettePath  = "Assets/Data/Map/TilePalette.asset";
        private const string BridgeTileId = "kopru";

        // Görsel %95 footprint (placeholder mesh ile aynı boşluk) — kose-kose = 2*OuterRadius*0.95
        private const float ArtScale = 0.95f;

        [MenuItem("TacticalRPG/Karo/Kopru Karo - Prefab + 'kopru' Tipine Ata")]
        public static void BuildBridgeTileAndAssign()
        {
            string fbxPath = FindFbx();
            if (string.IsNullOrEmpty(fbxPath))
            {
                EditorUtility.DisplayDialog("Hata", $"{ModelsFolder} icinde FBX bulunamadi.", "Tamam");
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Hata", "FBX yuklenemedi: " + fbxPath, "Tamam");
                return;
            }

            // --- Gecici sahne instance'i (olcum + duzeltme icin) ---
            var root = new GameObject("Tile_KopruKaro");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale    = Vector3.one;

            Bounds  raw   = ComputeBounds(root);
            Vector3 size0 = raw.size;
            if (size0 == Vector3.zero)
            {
                Object.DestroyImmediate(root);
                EditorUtility.DisplayDialog("Hata", "FBX'te Renderer/mesh bulunamadi.", "Tamam");
                return;
            }

            // --- 1) Eksen duzelt: en ince eksen -> Y (ust/kalinlik), en uzun -> Z (kose-kose) ---
            var axes = new[]
            {
                (dir: Vector3.right,   len: size0.x),
                (dir: Vector3.up,      len: size0.y),
                (dir: Vector3.forward, len: size0.z),
            }.OrderBy(a => a.len).ToArray(); // [0]=ince(kalinlik) [1]=orta(kenar) [2]=uzun(kose)

            Vector3 thinDir = axes[0].dir;
            Vector3 longDir = axes[2].dir;

            // longDir -> +Z, thinDir -> +Y olacak donme.
            Quaternion orient = Quaternion.Inverse(Quaternion.LookRotation(longDir, thinDir));
            // Tasarimci modelinde guverte ters tarafta kaliyor (asagi bakiyor) ->
            // yatay Z ekseninde 180 cevir ki guverte yukari, taban asagi (zemine) baksin.
            inst.transform.localRotation = Quaternion.AngleAxis(180f, Vector3.forward) * orient;

            Bounds b1 = ComputeBounds(root);

            // --- 2) Olcek: kose-kose (Z) = 2*OuterRadius*ArtScale ---
            float targetZ = HexMetrics.OuterRadius * 2f * ArtScale;
            float s        = targetZ / b1.size.z;
            inst.transform.localScale = Vector3.one * s;

            Bounds b2 = ComputeBounds(root);

            // --- 3) Pivot: merkez X/Z = 0, alt Y = 0 (alt-orta, zemine oturur) ---
            Vector3 c    = b2.center;
            float   minY = b2.min.y;
            inst.transform.localPosition -= new Vector3(c.x, minY, c.z);

            Bounds bf = ComputeBounds(root);

            // --- 4) Collider (oyun ici tiklama icin) ---
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }

            // --- 5) Prefab kaydet ---
            EnsureFolder("Assets/Prefabs/Grid");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Hata", "Prefab kaydedilemedi: " + PrefabPath, "Tamam");
                return;
            }

            // --- 6) TilePalette: SADECE 'kopru' tipine ata, digerlerini placeholder'a dondur ---
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            if (palette == null)
            {
                EditorUtility.DisplayDialog("Hata", "TilePalette bulunamadi: " + PalettePath, "Tamam");
                return;
            }

            // Diger tum girisleri renkli placeholder'a geri al (onceki "hepsine ata" durumunu temizler)
            foreach (var t in palette.tiles)
                if (t.id != BridgeTileId) t.prefab = null;

            // 'kopru' girisini bul/olustur ve prefab'i ata
            var bridge = palette.tiles.FirstOrDefault(t => t.id == BridgeTileId);
            if (bridge == null)
            {
                bridge = new TilePaletteSO.TileEntry
                {
                    id          = BridgeTileId,
                    displayName = "Kopru",
                    editorColor = new Color(0.55f, 0.45f, 0.35f, 1f),
                    isWalkable  = true,
                };
                palette.tiles.Add(bridge);
            }
            bridge.prefab     = prefab;
            bridge.isWalkable = true;

            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Acik sahnedeki grid'i HEMEN yeniden uret -> yeni (duz) prefab aninda gorunur.
            int regen = RegenerateOpenGrid();

            Debug.Log(
                "[KopruKaro] Olcum & duzeltme tamam\n" +
                $"FBX               : {fbxPath}\n" +
                $"Ham boyut (m)     : X={size0.x:F2}  Y={size0.y:F2}  Z={size0.z:F2}\n" +
                $"Donme sonrasi (m) : X={b1.size.x:F2}  Y={b1.size.y:F2}  Z={b1.size.z:F2}\n" +
                $"Olcek faktoru     : {s:F4}\n" +
                $"FINAL boyut (m)   : kenar-kenar(X)={bf.size.x:F3}  kalinlik(Y)={bf.size.y:F3}  kose-kose(Z)={bf.size.z:F3}\n" +
                $"FINAL dikey       : alt Y={bf.min.y:F3}  ust Y={bf.max.y:F3}\n" +
                $"Hedef             : kenar-kenar~1.645  kose-kose=1.90  (TileHeight ref={HexMetrics.TileHeight})\n" +
                $"Prefab            : {PrefabPath}\n" +
                $"Palet             : SADECE '{BridgeTileId}' tipine atandi; diger girisler placeholder");

            EditorUtility.DisplayDialog("Tamam",
                "Kopru karo prefab'i DUZ (guverte yukari) olusturuldu ve 'Kopru' tipine atandi.\n" +
                (regen == 1
                    ? "Sahnedeki grid otomatik yenilendi -> mevcut kopru karolar artik duz.\n\n"
                    : "(Sahnede HexGridManager bulunamadi; Play'e basinca yenilenir.)\n\n") +
                $"FINAL boyut:\n  kenar-kenar = {bf.size.x:F2} m\n  kose-kose  = {bf.size.z:F2} m\n  kalinlik   = {bf.size.y:F2} m\n\n" +
                "Tile Painter: 'Kopru' sec -> kopru boya, 'Lav' sec -> baska yere boya.\n\n" +
                "Detayli olcumler: Console.", "Tamam");

            EditorGUIUtility.PingObject(prefab);
        }

        // Acik sahnedeki HexGridManager'i bulup grid gorselini yeniden uretir.
        private static int RegenerateOpenGrid()
        {
#if UNITY_2023_1_OR_NEWER
            var gm = Object.FindFirstObjectByType<HexGridManager>();
#else
            var gm = Object.FindObjectOfType<HexGridManager>();
#endif
            if (gm == null) return 0;
            gm.GenerateGrid();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            return 1;
        }

        [MenuItem("TacticalRPG/Karo/Palet Prefablarini Temizle (placeholder'a don)")]
        public static void ClearPalettePrefabs()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            if (palette == null) return;
            foreach (var t in palette.tiles) t.prefab = null;
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Debug.Log("[KopruKaro] Palet prefablari temizlendi — tint'li placeholder'lara donuldu.");
        }

        private static string FindFbx()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder });
            string first = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.ToLowerInvariant().EndsWith(".fbx")) continue;
                first ??= p;
                string name = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (name.Contains("pr")) return p; // kopru / köprü tercih
            }
            return first;
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
