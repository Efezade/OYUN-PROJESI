using System.Linq;
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
        /// Karoların haritada birbirine nasıl oturduğunu Unity açmadan görmek için: boş sahnede
        /// <paramref name="id"/> karosundan 2 halkalık bir yama (oyundaki HexCoordinate yerleşimiyle)
        /// + çevresinde beyaz kutu karolar kurar, oyun kamerasının açısından (50°, -30°, ortografik)
        /// yakın ve geniş iki PNG yazar. Komut satırı: <c>-snapOut &lt;klasör&gt; -snapId &lt;id&gt;</c>.
        /// Sahne kaydedilmez.
        /// </summary>
        public static void Snapshot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string Arg(string key, string def)
            {
                int i = System.Array.IndexOf(args, key);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
            }
            string outDir = Arg("-snapOut", "Temp/karo_kontrol");
            string id     = Arg("-snapId", "cayir");
            string only   = Arg("-snapVariant", null);   // verilirse yalnız o varyant ("ana" = ana prefab)
            System.IO.Directory.CreateDirectory(outDir);

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            var entry = palette.GetById(id);
            if (entry?.prefab == null) { Debug.LogError($"[KokAhdi] snapshot: '{id}' prefab yok"); return; }
            GameObject forced = only == null ? null
                : only == "ana" ? entry.prefab
                : entry.variants.Find(v => v.name == only)?.prefab;
            if (only != null && forced == null) { Debug.LogError($"[KokAhdi] snapshot: varyant '{only}' yok"); return; }

            var sun = new GameObject("Gunes").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -60f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);

            var boxMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.72f, 0.45f) };
            Mesh box = TacticalRPG.Grid.HexMetrics.CreateHexMesh(0.95f);
            // Oyundaki gibi satır satır (r dış, q iç) — komşu kaçınma sırası HexGridManager'la aynı olsun.
            var chosen = new System.Collections.Generic.Dictionary<TacticalRPG.Grid.HexCoordinate, GameObject>();
            for (int r = -4; r <= 4; r++)
                for (int q = -4; q <= 4; q++)
                {
                    var c = new TacticalRPG.Grid.HexCoordinate(q, r);
                    int d = c.DistanceTo(new TacticalRPG.Grid.HexCoordinate(0, 0));
                    if (d > 4) continue;
                    Vector3 p = c.ToWorldPosition(1f);
                    GameObject go;
                    if (d <= 2)
                    {
                        GameObject look = forced ?? TacticalRPG.Grid.HexGridManager.PickLook(entry, c, chosen);
                        go = (GameObject)PrefabUtility.InstantiatePrefab(look);
                        bool mir = TacticalRPG.Grid.HexGridManager.ShouldMirror(entry, c);
                        if (mir) go.transform.localScale = new Vector3(-1f, 1f, 1f);
                        Debug.Log($"[KokAhdi] snapshot hucre {q},{r}: {look.name}{(mir ? " (ayna)" : "")}");
                    }
                    else
                    {
                        go = new GameObject("Kutu", typeof(MeshFilter), typeof(MeshRenderer));
                        go.GetComponent<MeshFilter>().sharedMesh = box;
                        go.GetComponent<MeshRenderer>().sharedMaterial = boxMat;
                    }
                    go.transform.position = p;
                }

            var cam = new GameObject("Kamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.18f);
            cam.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            foreach (var (name, size) in new[] { ("yakin", 2.6f), ("genis", 6.5f) })
            {
                cam.orthographicSize = size;
                cam.transform.position = new Vector3(0f, 0.3f, 0f) - cam.transform.forward * 22f;
                var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                string path = System.IO.Path.Combine(outDir, $"{id}{(only != null ? "__" + only : "")}_{name}.png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(rt);
                Debug.Log($"[KokAhdi] snapshot: {path}");
            }
        }

        /// <summary>
        /// Gerçek bölüm haritasında (Bolum1_Uretilen) görsel varyant komşuluğunu sayar: aynı görünüşün
        /// yan yana geldiği komşu çiftleri (aynalı dahil/hariç). Batch: -executeMethod ...CheckAdjacency.
        /// </summary>
        public static void CheckAdjacency()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(PalettePath);
            var map = AssetDatabase.LoadAssetAtPath<TileMapSO>("Assets/Data/Map/Bolum1_Uretilen.asset");
            var chosen = new System.Collections.Generic.Dictionary<TacticalRPG.Grid.HexCoordinate, GameObject>();
            var mirror = new System.Collections.Generic.Dictionary<TacticalRPG.Grid.HexCoordinate, bool>();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            for (int r = 0; r < map.GridSize.y; r++)
                for (int col = 0; col < map.GridSize.x; col++)
                {
                    var c = TacticalRPG.Grid.HexCoordinate.FromOffset(col, r);
                    var e = palette.GetById(map.GetTileId(c));
                    if (e?.prefab == null || e.variants.Count == 0) continue;
                    GameObject look = TacticalRPG.Grid.HexGridManager.PickLook(e, c, chosen);
                    mirror[c] = TacticalRPG.Grid.HexGridManager.ShouldMirror(e, c);
                    counts[look.name] = counts.TryGetValue(look.name, out int k) ? k + 1 : 1;
                }
            int pairs = 0, same = 0;
            foreach (var kv in chosen)
                for (int i = 0; i < 3; i++)   // her çift bir kez
                    if (chosen.TryGetValue(kv.Key.GetNeighbor(i), out GameObject n))
                    {
                        pairs++;
                        if (n == kv.Value) same++;
                    }
            Debug.Log($"[KokAhdi] komsuluk: {chosen.Count} varyantli hucre, {pairs} komsu cift, ayni gorunus yan yana = {same}; " +
                      $"aynali = {mirror.Values.Count(m => m)} · dagilim: " +
                      string.Join(", ", counts.Select(x => $"{x.Key}={x.Value}")));
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
                int sep = id.IndexOf("__", System.StringComparison.Ordinal);
                var entry = palette.GetById(sep > 0 ? id.Substring(0, sep) : id);
                GameObject prefab = sep > 0
                    ? entry?.variants.Find(v => v.name == id.Substring(sep + 2))?.prefab
                    : entry?.prefab;
                if (prefab == null) { Debug.LogWarning($"[KokAhdi] {id}: palette yok"); continue; }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
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
                Debug.Log($"[KokAhdi] {id}: prefab={AssetDatabase.GetAssetPath(prefab)} " +
                          $"boyut={b.size.x:F3}x{b.size.y:F3}x{b.size.z:F3} altY={b.min.y:F3} " +
                          $"ust_yari={up}/{total} → {(up > total / 2 ? "DIK" : "TERS")} " +
                          $"shader={mat?.shader.name} doku={(mat != null && mat.mainTexture != null ? mat.mainTexture.name : "YOK")} " +
                          $"yurunur={entry.isWalkable} yuzey={entry.surfaceHeightOverride}");
                Object.DestroyImmediate(inst);
            }
        }
    }
}
