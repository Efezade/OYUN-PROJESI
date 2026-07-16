using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// 9 harita, 3×3 SNAKE dizilim (9 8 7 / 6 5 4 / 3 2 1) — dünyanın ada deposu.
    /// Adalar arası geçiş YALNIZCA portal ile olur (<see cref="TeleportManager"/> → <see cref="TeleportTo"/>);
    /// kenar/sınır karosuna tıklayarak geçiş KALDIRILDI (güvenilmezdi, portal onun yerini aldı).
    /// Sorumluluk: ada yükleme (SwitchToMap) + portal ışınlaması + OnMapChanged yayını.
    /// </summary>
    public class WorldGridManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private PlayerController _player;
        [Tooltip("9 harita (snake): index 0=Harita1 … 8=Harita9.")]
        [SerializeField] private TileMapSO[] _maps = new TileMapSO[9];

        public int  CurrentMap { get; private set; } = 1;
        public bool IsBusy => _teleporting;

        private bool _teleporting;
        /// <summary>Portal ışınlanma efekti sürerken girişi kilitle (MapInputHandler IsBusy'e bakar).</summary>
        public void SetTeleporting(bool b) => _teleporting = b;

        /// <summary>Aktif harita adası değişince tetiklenir. WatchtowerManager dinler →
        /// yeni adanın (kalıcı açık mı?) sis durumunu yeniden uygular.</summary>
        public event System.Action OnMapChanged;

        /// <summary>n. adanın (1-9) TileMap'i (portal eşi taramak için).</summary>
        public TileMapSO GetMap(int n) =>
            (n >= 1 && n <= 9 && _maps != null && _maps.Length >= 9) ? _maps[n - 1] : null;

        /// <summary>Portal ile ışınla: hedef adaya geç (gerekirse) ve oyuncuyu hedef karoya koy.</summary>
        public void TeleportTo(int map, HexCoordinate coord)
        {
            if (_player == null) return;
            if (map < 1 || map > 9) return;
            if (map != CurrentMap) SwitchToMap(map);   // farklı ada → yükle
            _player.Initialize(coord);                 // konum + görüş
        }

        public void SwitchToMap(int n)
        {
            if (n < 1 || n > 9 || _maps == null || _maps.Length < 9) return;
            TileMapSO map = _maps[n - 1];
            if (map == null || _grid == null) return;
            CurrentMap = n;
            _grid.SetTileMap(map);   // grid yeniden üretilir → sis (OnGridRegenerated) tümü Hidden olur
            // Kam'ı ÇAĞIRAN konumlandırır (TeleportTo → Initialize). Burada eski koordinatta
            // Initialize edersek yeni haritada yanlış yerde sis açılırdı.
            OnMapChanged?.Invoke();  // WatchtowerManager → bu ada kalıcı açıksa sisi geri aç
        }
    }
}
