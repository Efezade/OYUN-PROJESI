using UnityEngine;
using UnityEditor;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Blender'dan gelen Kök Ahdi karolarını (MODELLER/Karolar → disa_aktar.py) palete bağlayan batch girişi:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.KokAhdiTileBatch.Run -logFile log.txt
    /// </code>
    /// Yalnız <see cref="Folder"/> taranır → eski karolar (agac_karo… Tiles/ kökünde) ellenmez.
    /// FBX adı = TileCatalog id'si (cayir.fbx → "cayir") → üretilen haritadaki o karonun modeli değişir.
    /// Elle aynı iş: Tile Painter ▸ Karo Klasörü = bu klasör ▸ "Klasörü Tara".
    /// </summary>
    public static class KokAhdiTileBatch
    {
        public const string Folder = "Assets/Art/Models/Tiles/KokAhdi";
        private const string PalettePath = "Assets/Data/Map/TilePalette.asset";

        public static void Run()
        {
            AssetDatabase.Refresh();
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            int n = TileFolderImporter.ImportFolder(Folder, palette, out string report);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KokAhdi] {n} karo islendi:\n{report}");
            Report();
        }

        /// <summary>
        /// Her Kök Ahdi karosunun prefab'ını denetler: boyut, malzeme/doku, YÖN.
        /// Yön ölçütü: süslemeler (ot, kütük) üstte → köşelerin çoğu karonun üst yarısındadır.
        /// Ters gelmişse "TERS" yazar (disa_aktar.py'deki 180° X ile içe aktarıcının döndürmesi uyuşmuyor demektir).
        /// </summary>
        public static void Report()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            foreach (string g in AssetDatabase.FindAssets("t:GameObject", new[] { Folder }))
            {
                string id = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g));
                var entry = palette.GetById(id);
                if (entry == null || entry.prefab == null) { Debug.LogWarning($"[KokAhdi] {id}: palette yok"); continue; }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
                var rends = inst.GetComponentsInChildren<Renderer>();
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                int up = 0, total = 0;
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        total++;
                        if (mf.transform.TransformPoint(v).y > b.center.y) up++;
                    }
                var mat = rends[0].sharedMaterial;
                Debug.Log($"[KokAhdi] {id}: prefab={AssetDatabase.GetAssetPath(entry.prefab)} " +
                          $"boyut={b.size.x:F3}x{b.size.y:F3}x{b.size.z:F3} altY={b.min.y:F3} " +
                          $"ust_yari={up}/{total} → {(up > total / 2 ? "DIK" : "TERS")} " +
                          $"shader={mat?.shader.name} doku={(mat != null && mat.mainTexture != null ? mat.mainTexture.name : "YOK")} " +
                          $"yurunur={entry.isWalkable} yuzey={entry.surfaceHeightOverride}");
                Object.DestroyImmediate(inst);
            }
        }
    }
}
