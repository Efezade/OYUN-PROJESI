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

        [Header("Sanal 3x3 Yerleşim (çöküş dalgası geometrisi)")]
        [Tooltip("Adalar sahnede TEK TEK yüklenir (diğerleri hiç görünmez); bu değer yalnız uzak-ada " +
                 "çöküş dalgasının geliş yönü/mesafesi hesabındaki adalar-arası boşluktur (m).")]
        [SerializeField] private float _islandGap = 4f;

        /// <summary>Kaynak adadaki (sourceMap) bir YEREL konumun, AKTİF adanın sahne çerçevesindeki
        /// SANAL konumu. Adalar sahnede aynı origin'e yüklendiği için, 3x3 dizilimdeki gerçek
        /// yön/mesafe bu ofsetle modellenir — uzak adadaki çöküş dalgası tam bu noktadan yayılır
        /// ve halka doğru yönden gelip aktif adayı tarar. Yerleşim = eski kenar-geçiş Neighbor
        /// eşlemesiyle aynı: snake 9 8 7 / 6 5 4 / 3 2 1; +X = grid YUKARI, +Z = grid SOL.</summary>
        public Vector3 VirtualPositionOnCurrentMap(int sourceMap, Vector3 sourceLocalPos)
        {
            if (sourceMap == CurrentMap || sourceMap < 1 || sourceMap > 9) return sourceLocalPos;
            ComputeIslandPitch(out float pitchX, out float pitchZ);
            int rs = 2 - (sourceMap  - 1) / 3, cs = 2 - (sourceMap  - 1) % 3;
            int rc = 2 - (CurrentMap - 1) / 3, cc = 2 - (CurrentMap - 1) % 3;
            // Satır +1 (grid aşağı) = dünya -X; sütun +1 (grid sağ) = dünya -Z.
            return sourceLocalPos + new Vector3((rc - rs) * pitchX, 0f, (cc - cs) * pitchZ);
        }

        // Ada "adımı" = aktif grid'in dünya boyutu + boşluk (9 ada aynı boyutta üretilir).
        private void ComputeIslandPitch(out float pitchX, out float pitchZ)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            if (_grid != null && _grid.Cells != null)
                foreach (var kv in _grid.Cells)
                {
                    Vector3 p = kv.Value.WorldPosition;
                    if (p.x < minX) minX = p.x;  if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;  if (p.z > maxZ) maxZ = p.z;
                }
            bool ok = maxX > minX;   // grid boşsa makul varsayılan
            pitchX = (ok ? maxX - minX : 12f) + _islandGap;
            pitchZ = (ok ? maxZ - minZ : 12f) + _islandGap;
        }

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
