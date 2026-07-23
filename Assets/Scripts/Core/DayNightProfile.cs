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
                sunEuler = new Vector3(22f, -45f, 0f),
                sunColor = new Color(1f, 0.82f, 0.62f),
                sunIntensity = 1.25f, shadowStrength = 0.85f,
                ambientColor = new Color(0.30f, 0.29f, 0.33f),
                skyColor     = new Color(0.45f, 0.47f, 0.58f)
            },
            new SlotAtmosphere {
                label = "Öğle",
                sunEuler = new Vector3(62f, -20f, 0f),
                sunColor = new Color(1f, 0.97f, 0.92f),
                sunIntensity = 1.60f, shadowStrength = 1f,
                ambientColor = new Color(0.40f, 0.41f, 0.44f),
                skyColor     = new Color(0.50f, 0.63f, 0.80f)
            },
            new SlotAtmosphere {
                label = "Öğleden Sonra",
                sunEuler = new Vector3(45f, 25f, 0f),
                sunColor = new Color(1f, 0.93f, 0.79f),
                sunIntensity = 1.45f, shadowStrength = 0.95f,
                ambientColor = new Color(0.36f, 0.35f, 0.36f),
                skyColor     = new Color(0.52f, 0.57f, 0.68f)
            },
            new SlotAtmosphere {
                label = "Akşam",
                sunEuler = new Vector3(12f, 62f, 0f),
                sunColor = new Color(1f, 0.58f, 0.36f),
                sunIntensity = 1.05f, shadowStrength = 0.75f,
                ambientColor = new Color(0.28f, 0.22f, 0.24f),
                skyColor     = new Color(0.40f, 0.24f, 0.24f)
            },
            new SlotAtmosphere {
                label = "Gece",
                // Ay: ufkun ÜSTÜNDE tutulur (ışık altta kalırsa her şey düz siyah olur).
                sunEuler = new Vector3(38f, 140f, 0f),
                sunColor = new Color(0.45f, 0.58f, 0.95f),
                sunIntensity = 0.38f, shadowStrength = 0.5f,
                ambientColor = new Color(0.12f, 0.14f, 0.22f),
                skyColor     = new Color(0.05f, 0.06f, 0.13f)
            },
            new SlotAtmosphere {
                label = "Gece Yarısı",
                sunEuler = new Vector3(58f, 175f, 0f),
                sunColor = new Color(0.38f, 0.50f, 0.90f),
                sunIntensity = 0.24f, shadowStrength = 0.4f,
                ambientColor = new Color(0.08f, 0.09f, 0.16f),
                skyColor     = new Color(0.03f, 0.03f, 0.09f)
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
