using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Bir klasördeki karo varlıklarını tarayıp TilePalette'e ekler — Tile Painter'ın
    /// "Klasörü Tara" düğmesi bunu çağırır. İki tür varlığı işler:
    ///   • FBX/model  → hex boyutuna ÖLÇEKLER (footprint = köşe-köşe 1.90 m) + pivot ALT-ORTA
    ///                  + MeshCollider ekler → bir prefab kaydeder (Assets/Prefabs/Grid/Tile_&lt;id&gt;).
    ///   • .prefab    → doğrudan referanslar (hazır karo).
    /// Her varlık için palet girişini (id'ye göre) bul/oluştur ve günceller (NON-DESTRUCTIVE:
    /// klasörde olmayan mevcut girişlere dokunmaz).
    ///
    /// GÜVENLİK: Bir model aşırı büyük/dağınıksa (footprint &gt; <see cref="MaxFootprint"/> birim)
    /// palete EKLENMEZ — "ATLANDI" uyarısı verilir. Böylece temiz olmayan export'lar (Blender'da
    /// Apply Transforms / Join / Delete Loose yapılmamış) haritayı bozmaz; rapor kullanıcıyı yönlendirir.
    ///
    /// YÖN: Otomatik eksen-döndürme YAPILMAZ (Y-up varsayılır). Bir karo yan/ters gelirse
    /// kaynak FBX'i Blender'da düzelt ve yeniden tara.
    /// </summary>
    public static class TileFolderImporter
    {
        private const string PrefabFolder = "Assets/Prefabs/Grid";

        // Görsel %95 footprint — köşe-köşe = 2*OuterRadius*0.95 = 1.90 m.
        private const float ArtScale = 0.95f;

        // Güvenlik eşikleri (temiz bir karo ~1.9 m ≈ 2 birimdir).
        private const float MaxFootprint  = 50f; // bunun üstü = bozuk model → ATLANIR
        private const float WarnFootprint = 5f;  // bunun üstü = işlenir ama uyarılır
        private const int   WarnMeshCount = 20;   // çok parçalı = uyarılır (tek mesh önerilir)

        /// <summary>Bir karo için palet girişi alanları.</summary>
        private class TileDef
        {
            public string  id;
            public string  displayName;
            public Color   color;
            public bool    walkable              = true;
            public float   surfaceHeightOverride = 0f;
        }

        // Tasarımcının bilinen karoları için GÜZEL varsayılanlar (ad/renk/yürünebilirlik).
        // Tabloda olmayan dosyalar için dosya adından genel giriş üretilir (bkz. ResolveDef).
        private static readonly Dictionary<string, TileDef> Overrides = new()
        {
            ["standartkaro"] = new TileDef { id = "default", displayName = "Standart", color = new Color(0.55f, 0.55f, 0.55f), walkable = true  },
            ["kumkaro"]      = new TileDef { id = "kum",     displayName = "Kum",      color = new Color(0.84f, 0.76f, 0.50f), walkable = true  },
            ["sukaro"]       = new TileDef { id = "su",      displayName = "Su",       color = new Color(0.24f, 0.52f, 0.82f), walkable = false },
            ["lavkaro"]      = new TileDef { id = "lav",     displayName = "Lav",      color = new Color(0.82f, 0.28f, 0.12f), walkable = false },
            ["koprukaro"]    = new TileDef { id = "kopru",   displayName = "Köprü",    color = new Color(0.55f, 0.45f, 0.35f), walkable = true  },
            ["agackaro1"]    = new TileDef { id = "agac1",  displayName = "Ağaç 1", color = new Color(0.20f, 0.45f, 0.22f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
            ["agackaro2"]    = new TileDef { id = "agac2",  displayName = "Ağaç 2", color = new Color(0.22f, 0.50f, 0.24f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
            ["agackaro3"]    = new TileDef { id = "agac3",  displayName = "Ağaç 3", color = new Color(0.18f, 0.40f, 0.20f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
            ["cicekkaro"]    = new TileDef { id = "cicek",  displayName = "Çiçek",  color = new Color(0.72f, 0.55f, 0.80f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
            ["mantarkaro"]   = new TileDef { id = "mantar", displayName = "Mantar", color = new Color(0.78f, 0.46f, 0.40f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
            ["kulekaro"]     = new TileDef { id = "kule",   displayName = "Kule",   color = new Color(0.50f, 0.48f, 0.46f), walkable = true, surfaceHeightOverride = HexMetrics.TileHeight },
        };

        /// <summary>
        /// Klasörü tarar, karoları palete ekler/günceller. Eklenen+güncellenen sayısını döndürür;
        /// satır satır raporu <paramref name="report"/> ile verir.
        /// </summary>
        public static int ImportFolder(string folder, TilePaletteSO palette, out string report)
        {
            var sb = new StringBuilder();
            if (palette == null) { report = "Palet yok."; return 0; }
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                report = $"Geçersiz klasör: {folder}";
                return 0;
            }

            EnsureFolder(PrefabFolder);

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            int count = 0;

            foreach (string g in guids)
            {
                string     path  = AssetDatabase.GUIDToAssetPath(g);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                string  stem = Path.GetFileNameWithoutExtension(path);
                TileDef def  = ResolveDef(stem);

                PrefabAssetType type   = PrefabUtility.GetPrefabAssetType(asset);
                GameObject      prefab = null;
                string          note   = null;

                if (type == PrefabAssetType.Model)
                {
                    string prefabPath = $"{PrefabFolder}/Tile_{def.id}.prefab";
                    prefab = BuildPrefabFromModel(path, prefabPath, out note);
                }
                else if (type == PrefabAssetType.Regular || type == PrefabAssetType.Variant)
                {
                    prefab = asset; // hazır prefab → doğrudan referansla
                }
                else continue; // model/prefab değil → atla

                if (prefab == null)
                {
                    sb.AppendLine($"  ✗ {stem}: {note}");
                    continue;
                }

                UpsertEntry(palette, def, prefab);
                count++;
                sb.AppendLine($"  ✓ {stem} → '{def.id}'" + (note != null ? $"   [{note}]" : ""));
            }

            if (count == 0 && sb.Length == 0)
                sb.AppendLine("  (Klasörde FBX/prefab karo bulunamadı.)");

            report = sb.ToString();
            return count;
        }

        // FBX'i işle: instantiate → footprint'e ölçekle → pivot alt-orta → collider → prefab.
        private static GameObject BuildPrefabFromModel(string fbxPath, string prefabPath, out string note)
        {
            note = null;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) { note = "FBX yüklenemedi"; return null; }

            var root = new GameObject("TMP_TileBuild");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity; // Y-up varsayılır (otomatik döndürme yok)
            inst.transform.localScale    = Vector3.one;

            Bounds b0 = ComputeBounds(root);
            if (b0.size == Vector3.zero) { Object.DestroyImmediate(root); note = "Renderer/mesh yok"; return null; }

            float footprint = Mathf.Max(b0.size.x, b0.size.z);
            int   meshCount = root.GetComponentsInChildren<MeshFilter>().Length;

            // GÜVENLİK: aşırı büyük footprint → temiz değil, palete ekleme (haritayı bozmasın).
            if (footprint > MaxFootprint)
            {
                Object.DestroyImmediate(root);
                note = $"ATLANDI — footprint {footprint:F0} birim (>{MaxFootprint:F0}). " +
                       "Blender: Join + Mesh>Clean Up>Delete Loose + Ctrl+A All Transforms, sonra tekrar tara.";
                return null;
            }

            // Ölçek: yatay footprint (köşe-köşe) = 1.90 m. Sadece X/Z — dik süsleme ölçeği bozmaz.
            float target = HexMetrics.OuterRadius * 2f * ArtScale;
            float s      = footprint > 0.0001f ? target / footprint : 1f;
            inst.transform.localScale = Vector3.one * s;

            Bounds b1 = ComputeBounds(root);

            // Pivot: X/Z merkez = 0, alt Y = 0 → zemine oturur.
            inst.transform.localPosition -= new Vector3(b1.center.x, b1.min.y, b1.center.z);

            // Collider: oyun içi tıklama + yüzey yüksekliği ışını.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            if (prefab == null) { note = "prefab kaydedilemedi"; return null; }

            // İşlendi ama dikkat çekilecek durumlar:
            var warns = new List<string>();
            if (footprint > WarnFootprint) warns.Add($"büyük model {footprint:F1}b");
            if (meshCount > WarnMeshCount) warns.Add($"{meshCount} parça (tek mesh önerilir)");
            if (warns.Count > 0) note = "UYARI: " + string.Join(", ", warns);

            return prefab;
        }

        // Bilinen karo → güzel varsayılan; bilinmeyen → dosya adından genel giriş.
        private static TileDef ResolveDef(string stem)
        {
            string key = stem.ToLowerInvariant();
            if (Overrides.TryGetValue(key, out TileDef d)) return d;
            return new TileDef
            {
                id          = Sanitize(key),
                displayName = stem,
                color       = AutoColor(key),
                walkable    = true,
            };
        }

        private static void UpsertEntry(TilePaletteSO palette, TileDef def, GameObject prefab)
        {
            TilePaletteSO.TileEntry entry = palette.tiles.FirstOrDefault(t => t.id == def.id);
            if (entry == null)
            {
                entry = new TilePaletteSO.TileEntry { id = def.id };
                palette.tiles.Add(entry);
            }
            entry.displayName           = def.displayName;
            entry.prefab                = prefab;
            entry.editorColor           = def.color;
            entry.isWalkable            = def.walkable;
            entry.surfaceHeightOverride = def.surfaceHeightOverride;
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static string Sanitize(string s)
        {
            var chars = s.Select(c => (char.IsLetterOrDigit(c) || c == '_') ? c : '_').ToArray();
            return new string(chars);
        }

        // İsimden kararlı, ayırt edilebilir bir renk (palet swatch'ı için).
        private static Color AutoColor(string key)
        {
            int   h   = Mathf.Abs(key.GetHashCode());
            float hue = (h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.5f, 0.85f);
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
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
