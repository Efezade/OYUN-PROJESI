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
    ///                  + MeshCollider ekler → bir prefab kaydeder (Assets/Prefabs/Tiles/Tile_&lt;id&gt;).
    ///   • .prefab    → doğrudan referanslar (hazır karo).
    /// Her varlık için palet girişini (id'ye göre) bulur/oluşturur. NON-DESTRUCTIVE: klasörde olmayan
    /// girişlere hiç dokunmaz; klasörde OLAN mevcut girişlerde ise yalnız <c>prefab</c> tazelenir —
    /// ad/renk/yürünürlük/yükseklik tasarımcınındır ve korunur (bkz. <c>UpsertEntry</c>).
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
        // Prefablar Grid/ DIŞINA yazılır: TileVisualFactory.IsGenerated "Assets/Prefabs/Grid/…"
        // altındakileri kendi ürettiği sayıp TAM KURULUM'da yer tutucuyla EZER (2026-10-10).
        // Eski karo prefabları (Tile_agac1…) Grid/ altında duruyor; yeniden taranınca buraya taşınır.
        private const string PrefabFolder = "Assets/Prefabs/Tiles";

        // Görsel %95 footprint — köşe-köşe = 2*OuterRadius*0.95 = 1.90 m.
        private const float ArtScale = 0.95f;

        // Kök Ahdi (el boyaması) karoları hücreyi TAM doldurur: komşular arasında boşluk yok, çimen
        // kapakları birbirine değer (Efe 2026-10-10: "bu çizim tarzında hepsi birbirine tam oturmalı").
        private const string FullFitFolder = "Assets/Art/Models/Tiles/KokAhdi";
        private const float  FullArtScale  = 1f;

        // Karolar FBX'ten baş-aşağı (ters) geliyor → tarama sırasında bu döndürmeyle düzeltilir.
        // Eksen yanlış sonuç verirse Euler'ı ayarla: (0,0,180)=Z-flip, (90,0,0)=Z-up→Y-up vb.
        private static readonly Quaternion ImportFlip = Quaternion.Euler(180f, 0f, 0f);

        // Güvenlik eşikleri (temiz bir karo ~1.9 m ≈ 2 birimdir).
        // Footprint TEK BAŞINA "bozuk" demek DEĞİL: importer her boyutu 1.90'a auto-scale eder, yani
        // temiz ama 1000x Blender-ölçekli FBX'ler (köprü dahil — footprint ~1900) geçerli olmalı. Bu
        // eşik artık yalnızca absürt/dejenere değerleri eler. (v1 felaketi büyük footprint'ten DEĞİL,
        // OFF-CENTER geometriden kaynaklıydı; gerekirse ileride merkez-kayma kontrolü eklenebilir.)
        private const float MaxFootprint  = 100000f;
        private const float WarnFootprint = 5f;  // bunun üstü = işlenir ama uyarılır
        private const int   WarnMeshCount = 20;   // çok parçalı = uyarılır (tek mesh önerilir)

        private const string VariantSeparator = "__";

        // Varyantın İLK eklenişindeki ağırlık (sonra Inspector'dan değişir, tarama ezmez).
        // Çayır (Efe 2026-10-10): sade ~%55 · tek odaklı ~%33 · dolu ~%11 → tekrar göze batmasın.
        private static readonly Dictionary<string, float> VariantWeightDefaults = new()
        {
            ["cayir__acik"]         = 3f,
            ["cayir__cicekli"]      = 2f,
            ["cayir__kutuk"]        = 1.5f,
            ["cayir__yosunlu_kaya"] = 1.5f,
            ["cayir__egrelti_kok"]  = 0.5f,
            // sade ikinci set (Efe 2026-10-10: "çeşitlilik yetmedi")
            ["cayir__klasik"]       = 0.5f,   // sade DEĞİL: konseptin dolu karosunun başka dizilişi
            ["cayir__yonca"]        = 2f,
            ["cayir__yassi_tas"]    = 2f,
            ["cayir__nemli"]        = 1.5f,
        };
        private static readonly Dictionary<string, float> MainWeightDefaults = new()
        {
            ["cayir"] = 0.5f,   // ana "dolu" karo (kütük + taş + mantar)
        };

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
            float artScale = folder.Replace('\\', '/').TrimEnd('/').StartsWith(FullFitFolder) ? FullArtScale : ArtScale;

            // Ana karolar önce: "<id>__<ad>" varyantları ana girişe eklenir, o yüzden giriş hazır olmalı.
            IEnumerable<string> paths = AssetDatabase.FindAssets("t:GameObject", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => Path.GetFileNameWithoutExtension(p).Contains(VariantSeparator));
            int count = 0;

            foreach (string path in paths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                PrefabAssetType type   = PrefabUtility.GetPrefabAssetType(asset);
                GameObject      prefab = null;
                string          note   = null;

                int sep = stem.IndexOf(VariantSeparator, System.StringComparison.Ordinal);
                if (sep > 0)
                {
                    if (ImportVariant(palette, stem, sep, path, asset, type, artScale, sb)) count++;
                    continue;
                }

                TileDef def = ResolveDef(stem, palette);

                if (type == PrefabAssetType.Model)
                {
                    string prefabPath = $"{PrefabFolder}/Tile_{def.id}.prefab";
                    prefab = BuildPrefabFromModel(path, prefabPath, artScale, out note);
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

                bool isNew = UpsertEntry(palette, def, prefab);
                count++;
                sb.AppendLine($"  ✓ {stem} → '{def.id}' " +
                              (isNew ? "(yeni giriş)" : "(model tazelendi — palet ayarları korundu)") +
                              (note != null ? $"   [{note}]" : ""));
            }

            if (count == 0 && sb.Length == 0)
                sb.AppendLine("  (Klasörde FBX/prefab karo bulunamadı.)");

            report = sb.ToString();
            return count;
        }

        // "<id>__<ad>.fbx" → <id> karosunun GÖRSEL varyantı (oynanış aynı; HexGridManager hücreye göre seçer).
        private static bool ImportVariant(TilePaletteSO palette, string stem, int sep, string path, GameObject asset,
                                          PrefabAssetType type, float artScale, StringBuilder sb)
        {
            string baseId = CatalogKey(stem.Substring(0, sep));
            string name   = CatalogKey(stem.Substring(sep + VariantSeparator.Length));
            TilePaletteSO.TileEntry entry = palette.tiles.FirstOrDefault(t => t.id == baseId);
            if (entry == null)
            {
                sb.AppendLine($"  ✗ {stem}: ana karo '{baseId}' palette yok (önce {baseId}.fbx taranmalı)");
                return false;
            }

            string     note   = null;
            GameObject prefab = type == PrefabAssetType.Model
                ? BuildPrefabFromModel(path, $"{PrefabFolder}/Tile_{baseId}{VariantSeparator}{name}.prefab", artScale, out note)
                : (type == PrefabAssetType.Regular || type == PrefabAssetType.Variant ? asset : null);
            if (prefab == null)
            {
                sb.AppendLine($"  ✗ {stem}: {note ?? "model/prefab değil"}");
                return false;
            }

            // Mevcut varyantta yalnız prefab tazelenir — ağırlık tasarımcının (Inspector).
            var v = entry.variants.FirstOrDefault(x => x.name == name);
            bool isNew = v == null;
            if (isNew)
            {
                if (entry.variants.Count == 0 && MainWeightDefaults.TryGetValue(baseId, out float mw))
                    entry.mainWeight = mw;
                entry.variants.Add(new TilePaletteSO.VisualVariant
                {
                    name   = name,
                    prefab = prefab,
                    weight = VariantWeightDefaults.TryGetValue($"{baseId}{VariantSeparator}{name}", out float w) ? w : 1f,
                });
            }
            else v.prefab = prefab;

            sb.AppendLine($"  ✓ {stem} → '{baseId}' varyantı '{name}' " +
                          (isNew ? "(yeni)" : "(model tazelendi — ağırlık korundu)") +
                          (note != null ? $"   [{note}]" : ""));
            return true;
        }

        // FBX'i işle: instantiate → footprint'e ölçekle → pivot alt-orta → collider → prefab.
        private static GameObject BuildPrefabFromModel(string fbxPath, string prefabPath, float artScale, out string note)
        {
            note = null;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) { note = "FBX yüklenemedi"; return null; }

            var root = new GameObject("TMP_TileBuild");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = ImportFlip; // karolar FBX'ten ters geliyor → 180° X ile düzelt
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

            // Ölçek: yatay footprint (köşe-köşe) = 1.90 m (Kök Ahdi: 2.00 m). Sadece X/Z — dik süsleme ölçeği bozmaz.
            float target = HexMetrics.OuterRadius * 2f * artScale;
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
        //
        // ÖNCE palette AYNI id'li giriş aranır (TileCatalog id'leri: "yosun_tarlasi", "cam_ormani"…).
        // Eşleşirse o girişin MODELİ değişir → üretilen harita yeni modeli hemen kullanır. Neden:
        // NormalizeKey alt çizgiyi atıyordu; "yosun_tarlasi.fbx" → "yosuntarlasi" adında YENİ bir
        // giriş açılıyor, harita ise eski yer tutucu karoyu göstermeye devam ediyordu (2026-10-10).
        private static TileDef ResolveDef(string stem, TilePaletteSO palette)
        {
            string catalogId = CatalogKey(stem);
            TilePaletteSO.TileEntry existing = palette.tiles.FirstOrDefault(t => t.id == catalogId);
            if (existing != null)
                return new TileDef { id = existing.id, displayName = existing.displayName };

            string key = NormalizeKey(stem);
            if (Overrides.TryGetValue(key, out TileDef d)) return d;
            return new TileDef
            {
                id          = Sanitize(key),
                displayName = stem,
                color       = AutoColor(key),
                walkable    = true,
            };
        }

        /// <summary>Palet girişini ekler/tazeler. Girişi YENİ oluşturduysa true döner.</summary>
        /// <remarks>
        /// Yukarıdaki <see cref="Overrides"/> tablosu ve <see cref="ResolveDef"/> yalnızca
        /// **İLK OLUŞTURMA** varsayılanlarıdır. MEVCUT bir giriş için sadece <c>prefab</c> tazelenir;
        /// ad / renk / <c>isWalkable</c> / yüzey yüksekliği TASARIMCININ malıdır (Tile Painter'dan
        /// ayarlanır) ve taramada korunur.
        ///
        /// TUZAK (2026-08-04'te bulundu): eskiden bu metod her alanı koşulsuz üzerine yazıyordu.
        /// Sonuç: Tile Painter'da bir karoyu "Yürünmez ✗" yapıp sonra "Klasörü Tara"ya basınca ayar
        /// SESSİZCE geri alınıyordu (tablodaki varsayılan su/lav dışında hepsi walkable=true).
        /// Karo hattı "FBX at → Klasörü Tara → boya" olduğu için bu düğmeye sık basılıyor; yürünmezlik
        /// ayarları bu yüzden hiç tutmuyordu. Bu davranışı geri getirme.
        /// </remarks>
        private static bool UpsertEntry(TilePaletteSO palette, TileDef def, GameObject prefab)
        {
            TilePaletteSO.TileEntry entry = palette.tiles.FirstOrDefault(t => t.id == def.id);
            if (entry != null)
            {
                entry.prefab = prefab;   // modeli tazele, tasarımcı ayarlarına DOKUNMA
                return false;
            }

            palette.tiles.Add(new TilePaletteSO.TileEntry
            {
                id                    = def.id,
                displayName           = def.displayName,
                prefab                = prefab,
                editorColor           = def.color,
                isWalkable            = def.walkable,
                surfaceHeightOverride = def.surfaceHeightOverride,
            });
            return true;
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        // Türkçe karakterleri ASCII'ye indirger ki "ağaçkaro1" → "agackaro1" tablo anahtarıyla
        // eşleşsin (yoksa Türkçe adlı karolar bilinmeyen sayılıp genel/bozuk id + yanlış
        // yürünürlük/renk/yükseklik alır). Hem Overrides lookup hem id üretimi için kullanılır.
        private static string NormalizeKey(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
            {
                switch (c)
                {
                    case 'ğ': sb.Append('g'); break;
                    case 'ç': sb.Append('c'); break;
                    case 'ş': sb.Append('s'); break;
                    case 'ı': sb.Append('i'); break;
                    case 'ö': sb.Append('o'); break;
                    case 'ü': sb.Append('u'); break;
                    case 'â': sb.Append('a'); break;
                    case 'î': sb.Append('i'); break;
                    case 'û': sb.Append('u'); break;
                    // _, -, boşluk vb. AT: "agac_karo_1" / "agac karo 1" → "agackaro1" (tabloyla eşleşsin).
                    default:  if (char.IsLetterOrDigit(c)) sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // Katalog biçimi: Türkçe→ASCII, küçük harf, boşluk/tire → '_' ("Yosun Tarlası" → "yosun_tarlasi").
        private static string CatalogKey(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (string part in s.Split(' ', '-', '_'))
            {
                string p = NormalizeKey(part);
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append('_');
                sb.Append(p);
            }
            return sb.ToString();
        }

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
