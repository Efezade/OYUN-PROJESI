using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Çok-tipli öz sisteminin görsel ayarı: her türün adı + rengi + (opsiyonel) prefab'ı.
    /// Renkler/prefab'lar koda gömülmez — buradan ayarlanır (Whiteboxing). Öz YERLEŞİMİ rastgele
    /// değildir; el yapımı EssenceMapSO + EssencePainterWindow ile belirlenir. Saf veri.
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
            [Tooltip("Bu türün harita görseli. BOŞSA renkli placeholder küre üretilir — " +
                     "buraya kendi (animasyonlu) öz prefab'ını atayınca otomatik o kullanılır.")]
            public GameObject  prefab;
        }

        [Header("Tür stilleri (ad + renk + prefab)")]
        [SerializeField] private TypeStyle[] _types;

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

        /// <summary>Bu türün görsel prefab'ı (atanmamışsa null → placeholder küre).</summary>
        public GameObject PrefabOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t) return s.prefab;
            return null;
        }
    }
}
