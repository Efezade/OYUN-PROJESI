using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bölüm 1 prosedürel harita üretiminin ayarları (TASK-005 · GAME_DESIGN.md §3).
    /// Sayılar koda gömülmez — hepsi buradan gelir (CLAUDE.md §3 Whiteboxing).
    ///
    /// **UYARI:** Bu değerler denge tarafında Python ile kalibre edildi ve 10 seed ELLE doğrulandı.
    /// Boyutu / engel oranını / ağırlıkları değiştirirsen 10-seed havuzunun doğrulaması GEÇERSİZ olur
    /// (haritalar bambaşka çıkar). Değiştirmeden önce `Docs/Balance/tools/harita_seed_secimi.py`
    /// yeniden koşturulmalı.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainConfig", menuName = "TacticalRPG/Config/TerrainConfig")]
    public class TerrainConfigSO : ScriptableObject
    {
        [Header("Harita boyutu (hex)")]
        [SerializeField, Min(1)] private int _width  = 22;
        [SerializeField, Min(1)] private int _height = 25;

        [Header("Engel üretimi")]
        // DİKKAT: double — float DEĞİL. 0.20f'in double karşılığı 0.20000000298… olur ve Python
        // referansıyla birebir eşleşme (aynı seed → aynı harita) bozulabilir.
        [Tooltip("Hedef engel oranı (sık orman + dağ + göl + nehir). GAME_DESIGN §3: ~%20.")]
        [SerializeField] private double _obstaclePct = 0.20;
        [SerializeField, Min(0)] private int _riverCount      = 1;
        [Tooltip("Nehir başına köprü geçidi sayısı — nehir yürünemez, köprü yürünür.")]
        [SerializeField, Min(0)] private int _bridgesPerRiver = 2;
        [SerializeField, Min(1)] private int _blobSizeMin     = 8;
        [SerializeField, Min(1)] private int _blobSizeMax     = 30;

        [Header("Yürünür alt-tip ağırlıkları")]
        // DİKKAT: double — float DEĞİL (yukarıdaki gerekçe).
        [Tooltip("SIRA ÖNEMLİ — TerrainGenerator.SubtypeIds ile aynı sırada olmalı: " +
                 "ova · taşlık ova · bol taşlık ova · az ağaçlı ova · orman · nadir yüksek orman.")]
        [SerializeField] private List<double> _subtypeWeights = new() { 0.55, 0.12, 0.05, 0.15, 0.10, 0.03 };

        [Header("Sabit seed havuzu")]
        [Tooltip("GAME_DESIGN.md §3'teki 10 seed (gap12'ye göre yüksekten düşüğe). SONSUZ/TAM RASTGELE " +
                 "ÜRETME YOK — bu havuzun dışına çıkılmaz, çünkü bunlar adalet/oynanabilirlik için " +
                 "elle doğrulandı (harita parçalanmamış, 70-öz hedefi ulaşılabilir, görevler erişilebilir).")]
        [SerializeField] private List<int> _seedPool = new() { 89, 7, 20, 108, 219, 64, 173, 283, 141, 286 };

        [Header("Oyuncu başlangıcı")]
        [Tooltip("Başlangıç aranacak referans karo (varsayılan harita ortası). Yürünemezse en yakın " +
                 "yürünür karoya kaydırılır.")]
        [SerializeField] private Vector2Int _startHint = new(11, 12);

        public int    Width          => _width;
        public int    Height         => _height;
        public double ObstaclePct    => _obstaclePct;
        public int   RiverCount      => _riverCount;
        public int   BridgesPerRiver => _bridgesPerRiver;
        public int   BlobSizeMin     => _blobSizeMin;
        public int   BlobSizeMax     => _blobSizeMax;
        public Vector2Int StartHint  => _startHint;

        public IReadOnlyList<int> SeedPool => _seedPool;

        /// <summary>Ağırlıkları üreticinin beklediği double dizisine çevirir; eksik/boşsa null döner
        /// (üretici o zaman kendi varsayılanını kullanır — Python referansıyla aynı).</summary>
        public double[] SubtypeWeights()
        {
            if (_subtypeWeights == null || _subtypeWeights.Count == 0) return null;
            return _subtypeWeights.ToArray();
        }
    }
}
