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

            // Birimin basacağı yüzey yüksekliği (taban üstü). > 0 ise elle belirler;
            // <= 0 (varsayılan) ise HexGridManager hücre merkezinden ışınla otomatik ölçer.
            public float      surfaceHeightOverride = 0f;
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
