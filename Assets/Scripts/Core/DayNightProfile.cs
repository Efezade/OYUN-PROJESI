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
                sunColor = new Color(1f, 0.90f, 0.77f),
                sunIntensity = 1.45f, shadowStrength = 0.65f,
                ambientColor = new Color(0.46f, 0.46f, 0.49f),
                skyColor     = new Color(0.45f, 0.55f, 0.66f)
            },
            new SlotAtmosphere {
                label = "Öğle",
                sunEuler = new Vector3(62f, -20f, 0f),
                sunColor = new Color(1f, 0.97f, 0.92f),
                sunIntensity = 1.65f, shadowStrength = 0.72f,
                ambientColor = new Color(0.50f, 0.51f, 0.53f),
                skyColor     = new Color(0.50f, 0.63f, 0.80f)
            },
            new SlotAtmosphere {
                label = "Öğleden Sonra",
                sunEuler = new Vector3(48f, 20f, 0f),
                sunColor = new Color(1f, 0.95f, 0.85f),
                sunIntensity = 1.50f, shadowStrength = 0.68f,
                ambientColor = new Color(0.47f, 0.48f, 0.49f),
                skyColor     = new Color(0.52f, 0.57f, 0.68f)
            },
            new SlotAtmosphere {
                label = "Akşam",
                sunEuler = new Vector3(27f, 58f, 0f),
                sunColor = new Color(1f, 0.79f, 0.58f),
                sunIntensity = 1.22f, shadowStrength = 0.60f,
                ambientColor = new Color(0.42f, 0.40f, 0.42f),
                skyColor     = new Color(0.50f, 0.43f, 0.42f)
            },
            new SlotAtmosphere {
                label = "Gece",
                // Ay: ufkun ÜSTÜNDE tutulur (ışık altta kalırsa her şey düz siyah olur).
                sunEuler = new Vector3(42f, 125f, 0f),
                sunColor = new Color(0.61f, 0.72f, 1f),
                sunIntensity = 0.65f, shadowStrength = 0.38f,
                ambientColor = new Color(0.27f, 0.32f, 0.40f),
                skyColor     = new Color(0.085f, 0.12f, 0.20f)
            },
            new SlotAtmosphere {
                label = "Gece Yarısı",
                sunEuler = new Vector3(52f, 150f, 0f),
                sunColor = new Color(0.56f, 0.68f, 1f),
                sunIntensity = 0.50f, shadowStrength = 0.32f,
                ambientColor = new Color(0.23f, 0.28f, 0.36f),
                skyColor     = new Color(0.065f, 0.09f, 0.16f)
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
