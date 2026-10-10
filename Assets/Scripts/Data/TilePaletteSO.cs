using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Karo türlerinin tanım listesi.
    /// Her giriş: benzersiz id, görünen ad, 3D prefab, editor rengi, yürünebilirlik.
    /// Inspector'dan düzenle — SceneSetupTool default girişi oluşturur.
    /// </summary>
    [CreateAssetMenu(fileName = "TilePalette", menuName = "TacticalRPG/Tile Palette")]
    public class TilePaletteSO : ScriptableObject
    {
        [System.Serializable]
        public class TileEntry
        {
            public string     id          = "default";
            public string     displayName = "Karo";
            public GameObject prefab;
            public Color      editorColor = Color.gray;
            public bool       isWalkable  = true;

            [Tooltip("Bu karo bir SAVAŞ ALANI mı? İşaretliyse oyuncu yakındayken karoya tıklayınca " +
                     "savaşa girme istemi çıkar (MissionManager: id'ye özel görev ya da varsayılan).")]
            public bool       canEnterCombat = false;

            [Tooltip("Bu karo bir MAĞAZA (store) mı? İşaretliyse oyuncu yakınınca öz harcayıp " +
                     "item/pot alabileceği dükkân açılır (StoreManager + StoreHUD).")]
            public bool       isStore = false;

            // Birimin basacağı yüzey yüksekliği (taban üstü). > 0 ise elle belirler;
            // <= 0 (varsayılan) ise HexGridManager hücre merkezinden ışınla otomatik ölçer.
            public float      surfaceHeightOverride = 0f;

            [Tooltip("Aynı karonun GÖRSEL varyantları (oynanış aynı, yalnız model değişir). Doluysa her " +
                     "hücre ana prefab + varyantlar arasından ağırlığa göre, KOORDİNATA BAĞLI sabit seçer " +
                     "(harita yeniden kurulunca aynı hücre aynı görünür). Kök Ahdi hattı: '<id>__<ad>.fbx'.")]
            public List<VisualVariant> variants = new();

            [Tooltip("Varyant havuzunda ANA prefabın ağırlığı (0 = ana prefab hiç çıkmaz).")]
            [Min(0f)] public float mainWeight = 1f;

            /// <summary>Hücre için görsel seçer; <paramref name="hash"/> = koordinattan kararlı sayı.</summary>
            public GameObject PickPrefab(int hash)
            {
                if (variants == null || variants.Count == 0) return prefab;
                float total = prefab != null ? mainWeight : 0f;
                foreach (var v in variants)
                    if (v.prefab != null) total += v.weight;
                if (total <= 0f) return prefab;

                float t = (hash & 0x7fffffff) / (float)int.MaxValue * total;
                if (prefab != null)
                {
                    if (t < mainWeight) return prefab;
                    t -= mainWeight;
                }
                foreach (var v in variants)
                {
                    if (v.prefab == null) continue;
                    if (t < v.weight) return v.prefab;
                    t -= v.weight;
                }
                return prefab;
            }
        }

        [System.Serializable]
        public class VisualVariant
        {
            public string     name;
            public GameObject prefab;
            [Min(0f)] public float weight = 1f;
        }

        public List<TileEntry> tiles = new();

        public TileEntry GetById(string id)
        {
            foreach (var t in tiles)
                if (t.id == id) return t;
            return null;
        }
    }
}
