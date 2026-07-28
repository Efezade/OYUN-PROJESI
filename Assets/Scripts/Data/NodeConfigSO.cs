using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>Bölüm haritasına yerleşen düğüm (node) türleri — TASK-006 / GAME_DESIGN.md §3.</summary>
    public enum MapNodeType
    {
        /// <summary>Zorunlu harita-kurtarma görevi. Sis'ten BAĞIMSIZ hep görünür; üçü de
        /// tamamlanmadan bölüm bitmez.</summary>
        Mandatory  = 0,
        /// <summary>Yan görev. Zorluk GİRMEDEN ÖNCE görünür, ödül GİZLİ ve yüksek varyanslı.</summary>
        Zindan     = 1,
        /// <summary>Hafif, tekrar edilebilir savaş. Düşük maliyet.</summary>
        Encounter  = 2,
        /// <summary>Gündüz marketi — SADECE gündüz dilimlerinde açık.</summary>
        Market     = 3,
        /// <summary>Gözetleme kulesi — çevresindeki alanın sisini KALICI açar.</summary>
        Watchtower = 4,
        /// <summary>Ana boss — KONUMDAN BAĞIMSIZ, haritanın her yerinden girilir.</summary>
        Boss       = 5
    }

    /// <summary>
    /// Bölüm 1 düğüm yerleşiminin sayıları ve değer/maliyet aralıkları
    /// (TASK-006 · referans: `Docs/Balance/tools/harita_map1_sim.py` → `build_nodes`).
    ///
    /// **Sayılar TASLAK** — INBOX'ta açıkça öyle deniyor; playtest'le ayarlanacak. Hepsi buradan
    /// gelir, koda gömülü değildir (CLAUDE.md §3 Whiteboxing).
    /// </summary>
    [CreateAssetMenu(fileName = "NodeConfig", menuName = "TacticalRPG/Config/NodeConfig")]
    public class NodeConfigSO : ScriptableObject
    {
        [Header("Zorunlu görevler (bölüm bunlarsız bitmez)")]
        [SerializeField, Min(0)] private int _mandatoryCount = 3;
        [SerializeField, Min(0)] private int _mandatoryValue = 20;
        [SerializeField, Min(0)] private int _mandatoryAP    = 5;

        [Header("Zindan — riski bil, ödülü bilme")]
        [SerializeField, Min(0)] private int _zindanCount    = 6;
        [Tooltip("Ödül aralığı (GİZLİ — girmeden görünmez).")]
        [SerializeField] private Vector2Int _zindanValue = new(8, 15);
        [Tooltip("AP maliyeti aralığı. Zorluk göstergesi bu maliyetten türetilir (GÖRÜNÜR).")]
        [SerializeField] private Vector2Int _zindanAP    = new(3, 6);

        [Header("Encounter — hafif, tekrar edilebilir savaş")]
        [SerializeField, Min(0)] private int _encounterCount = 8;
        [SerializeField] private Vector2Int _encounterValue = new(3, 6);
        [SerializeField] private Vector2Int _encounterAP    = new(1, 2);

        [Header("Gündüz marketi")]
        [SerializeField, Min(0)] private int _marketCount = 2;

        [Header("Gözetleme kulesi")]
        [Tooltip("Haritaya RASTGELE yerleşen kule sayısı (kullanıcı kararı 2026-07-28: 3).")]
        [SerializeField, Min(0)] private int _watchtowerCount = 3;
        [Tooltip("Kaç karo YARIÇAPINDAKİ alan KALICI açılır. ÇAP = 2×yarıçap+1 → yarıçap 4 = " +
                 "9 karo çapında alan (61 karo). Kullanıcı kararı 2026-07-28.")]
        [SerializeField, Min(1)] private int _watchtowerRadius = 4;
        [SerializeField, Min(0)] private int _watchtowerAP     = 1;

        [Header("Ana boss (konumdan bağımsız)")]
        [SerializeField, Min(0)] private int _bossAP = 5;

        [Header("Zaman baskısı (TASK-007)")]
        [Tooltip("Bu GÜNDEN İTİBAREN zindan/encounter AP maliyeti çarpanla artar (GAME_DESIGN §3: gün 10).")]
        [SerializeField, Min(1)] private int _lateCostFromDay = 10;
        [Tooltip("Geç oyunda zindan/encounter maliyet çarpanı (TASLAK: ×2).")]
        [SerializeField, Min(1)] private int _lateCostMultiplier = 2;

        public int LateCostFromDay    => _lateCostFromDay;
        public int LateCostMultiplier => _lateCostMultiplier;

        public int MandatoryCount => _mandatoryCount;
        public int MandatoryValue => _mandatoryValue;
        public int MandatoryAP    => _mandatoryAP;

        public int        ZindanCount => _zindanCount;
        public Vector2Int ZindanValue => _zindanValue;
        public Vector2Int ZindanAP    => _zindanAP;

        public int        EncounterCount => _encounterCount;
        public Vector2Int EncounterValue => _encounterValue;
        public Vector2Int EncounterAP    => _encounterAP;

        public int MarketCount      => _marketCount;
        public int WatchtowerCount  => _watchtowerCount;
        public int WatchtowerRadius => _watchtowerRadius;
        public int WatchtowerAP     => _watchtowerAP;
        public int BossAP           => _bossAP;
    }
}
