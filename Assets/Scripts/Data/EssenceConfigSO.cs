using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Çok-tipli öz sisteminin merkezi ayarı: her türün adı + rengi (UI/node görseli)
    /// ve harita node spawn oranları. Renkler/oranlar koda gömülmez — buradan ayarlanır
    /// (Whiteboxing). Saf veri; runtime'da değiştirilmez.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Essence Config", fileName = "EssenceConfig")]
    public class EssenceConfigSO : ScriptableObject
    {
        [System.Serializable]
        public struct TypeStyle
        {
            public EssenceType type;
            public string      displayName;
            public Color       color;
            [Tooltip("Harita node spawn'ında bu türün görece olasılığı (oran).")]
            [Min(0)] public int spawnWeight;
        }

        [Header("Tür stilleri (ad + renk + spawn ağırlığı)")]
        [SerializeField] private TypeStyle[] _types;

        [Header("Harita node spawn ayarları")]
        [Tooltip("Bir karoda öz bulunma olasılığı (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _tileChance = 0.55f;
        [Tooltip("Özlü bir karoda en az / en çok kaç öz birimi.")]
        [SerializeField, Min(1)] private int _minPerTile = 1;
        [SerializeField, Min(1)] private int _maxPerTile = 4;

        public float TileChance => _tileChance;
        public int   MinPerTile => _minPerTile;
        public int   MaxPerTile => Mathf.Max(_minPerTile, _maxPerTile);

        public string NameOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t && !string.IsNullOrEmpty(s.displayName)) return s.displayName;
            return t.ToString();
        }

        public Color ColorOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t) return s.color;
            return Color.white;
        }

        /// <summary>Spawn ağırlıklarına göre rastgele bir öz türü seçer.</summary>
        public EssenceType RandomWeightedType()
        {
            int total = 0;
            if (_types != null)
                foreach (var s in _types) total += Mathf.Max(0, s.spawnWeight);

            if (total <= 0 || _types == null || _types.Length == 0)
                return (EssenceType)Random.Range(0, 3); // ağırlık yoksa düzgün dağılım

            int roll = Random.Range(0, total);
            foreach (var s in _types)
            {
                roll -= Mathf.Max(0, s.spawnWeight);
                if (roll < 0) return s.type;
            }
            return _types[_types.Length - 1].type;
        }
    }
}
