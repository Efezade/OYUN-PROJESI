using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Gün/gece atmosferi — her ZAMAN DİLİMİ için ışık/ortam/gökyüzü ayarları.
    /// Eşleşme dilim SIRASINA göre yapılır: giriş 0 = TimeSlotConfig'in 0. dilimi ("Sabah").
    /// TimeSlotConfig kaç dilim tanımlıyorsa burada o kadar giriş olmalı (varsayılan 6);
    /// eksikse indeks başa sarar, fazlası yok sayılır.
    /// Tüm değerler Inspector'dan tweaklenir — DayNightCycle bunları okur.
    /// </summary>
    [CreateAssetMenu(fileName = "DayNightProfile", menuName = "TacticalRPG/Config/DayNightProfile")]
    public class DayNightProfile : ScriptableObject
    {
        [System.Serializable]
        public class SlotAtmosphere
        {
            [Tooltip("Yalnızca Inspector okunabilirliği için — eşleşme SIRAYA göredir, isme göre DEĞİL.")]
            public string label = "Dilim";

            [Header("Güneş / Ay (Directional Light)")]
            [Tooltip("Işığın dünya rotasyonu. X = yükseklik (küçük = ufka yakın), Y = pusula yönü.")]
            public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
            [ColorUsage(false)] public Color sunColor = new Color(1f, 0.96f, 0.9f);
            [Min(0f)] public float sunIntensity = 1.6f;
            [Range(0f, 1f)] public float shadowStrength = 1f;

            [Header("Ortam")]
            [Tooltip("Düz (Flat) ambient renk — gölgede kalan yüzeylerin taban aydınlatması.")]
            [ColorUsage(false)] public Color ambientColor = new Color(0.35f, 0.35f, 0.38f);
            [Tooltip("Kamera arkaplanı (gökyüzü). Kamera SolidColor ile temizlendiği için bu renk gökyüzüdür.")]
            [ColorUsage(false)] public Color skyColor = new Color(0.04f, 0.03f, 0.07f);
        }

        [Tooltip("Sıra = zaman dilimi sırası. Varsayılan: Sabah, Öğle, Öğleden Sonra, Akşam, Gece, Gece Yarısı.")]
        [SerializeField] private SlotAtmosphere[] _slots =
        {
            new SlotAtmosphere {
                label = "Sabah",
                sunEuler = new Vector3(32f, -45f, 0f),
                sunColor = new Color(1f, 0.95f, 0.86f),
                sunIntensity = 1.9f, shadowStrength = 0.52f,
                ambientColor = new Color(0.72f, 0.74f, 0.77f),
                skyColor = new Color(0.52f, 0.63f, 0.74f)
            },
            new SlotAtmosphere {
                label = "Öğle",
                sunEuler = new Vector3(62f, -20f, 0f),
                sunColor = new Color(1f, 0.98f, 0.95f),
                sunIntensity = 2.1f, shadowStrength = 0.58f,
                ambientColor = new Color(0.76f, 0.78f, 0.8f),
                skyColor = new Color(0.57f, 0.69f, 0.83f)
            },
            new SlotAtmosphere {
                label = "Öğleden Sonra",
                sunEuler = new Vector3(48f, 20f, 0f),
                sunColor = new Color(1f, 0.97f, 0.89f),
                sunIntensity = 1.95f, shadowStrength = 0.54f,
                ambientColor = new Color(0.73f, 0.76f, 0.78f),
                skyColor = new Color(0.57f, 0.65f, 0.77f)
            },
            new SlotAtmosphere {
                label = "Akşam",
                sunEuler = new Vector3(27f, 58f, 0f),
                sunColor = new Color(1f, 0.86f, 0.69f),
                sunIntensity = 1.7f, shadowStrength = 0.46f,
                ambientColor = new Color(0.69f, 0.69f, 0.73f),
                skyColor = new Color(0.6f, 0.52f, 0.49f)
            },
            new SlotAtmosphere {
                label = "Gece",
                // Ay: ufkun ÜSTÜNDE tutulur (ışık altta kalırsa her şey düz siyah olur).
                sunEuler = new Vector3(42f, 125f, 0f),
                sunColor = new Color(0.79f, 0.88f, 1f),
                sunIntensity = 1.3f, shadowStrength = 0.28f,
                ambientColor = new Color(0.57f, 0.66f, 0.78f),
                skyColor = new Color(0.2f, 0.28f, 0.41f)
            },
            new SlotAtmosphere {
                label = "Gece Yarısı",
                sunEuler = new Vector3(52f, 150f, 0f),
                sunColor = new Color(0.75f, 0.85f, 1f),
                sunIntensity = 1.15f, shadowStrength = 0.24f,
                ambientColor = new Color(0.53f, 0.62f, 0.75f),
                skyColor = new Color(0.17f, 0.24f, 0.37f)
            }
        };

        public int Count => _slots != null ? _slots.Length : 0;

        /// <summary>Dilim indeksine karşılık gelen atmosfer; indeks başa sarar. Profil boşsa null.</summary>
        public SlotAtmosphere GetSlot(int slotIndex)
        {
            if (_slots == null || _slots.Length == 0) return null;
            int i = slotIndex % _slots.Length;
            if (i < 0) i += _slots.Length;
            return _slots[i];
        }
    }
}
