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
