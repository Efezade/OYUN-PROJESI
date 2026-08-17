using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// MİNİHARİTA DOKUSUNU ÜRETİR — haritanın veri modelinden bir <see cref="Texture2D"/> boyar.
    ///
    /// NEDEN KAMERA + RENDER TEXTURE DEĞİL (yöntem kararı 2026-08-17):
    ///   • Bir minimap kamerası 550+ karoyu HER KARE ikinci kez render eder; bu harita yalnız
    ///     menü açıkken görünüyor — bedeli karşılığı yok.
    ///   • Kamera 3B sahneyi küçültür; istenen "daha düşük kalitede, tasarımsal" bir harita.
    ///   • En önemlisi: kamera SİSİ BİLMEZ, yalnız bulut modellerini görür. "Keşfedildi ama şu an
    ///     görüş dışında" ile "hiç görülmedi" ayrımı sis VERİSİNDEDİR, sahnede değil.
    /// Bu yüzden doku doğrudan VERİDEN boyanıyor: karo tipi + sis durumu + düğümler. Aynı yöntem
    /// tile tabanlı oyunlarda standarttır (her piksel/karo grubu bir karoyu temsil eder).
    ///
    /// KARO RENGİ NEREDEN GELİR: paletteki karonun PREFABININ zemin materyalinden
    /// (<c>_BaseColor</c>). Yani karonun modelini/materyalini değiştirince minimap rengi de
    /// kendiliğinden değişir — ayrıca bir "minimap rengi" alanı doldurmak gerekmez
    /// (kullanıcı isteği: "karo ya da modelini değiştirince minimaptaki görünüm de değişecek").
    ///
    /// KABARTMA: Minecraft'ın harita kuralı — her karo KUZEYİNDEKİ komşusuyla karşılaştırılır;
    /// alçaksa koyu, eşitse orta, yüksekse parlak ton. Dağlar/ormanlar böylece düz renk yığını
    /// değil, kabartmalı bir arazi gibi okunur.
    ///
    /// Doku YALNIZ istendiğinde (harita ekranı açılınca / harita üretilince) yeniden boyanır —
    /// her kare çalışan bir iş yok (CLAUDE.md §6).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class MinimapRenderer : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private FogOfWarManager  _fog;
        [SerializeField] private GameStateManager _state;
        [SerializeField] private MinimapStyleSO   _style;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private Texture2D _texture;
        private Color32[] _buffer;
        private int       _w, _h;
        private float     _minX, _minZ, _ppu;

        // Karo id → minimap rengi. Prefabın materyalini okumak ucuz değil; 70+ karo tipi için
        // bir kez çözülüp saklanır. Tam yeniden boyamada temizlenir → model değişirse yenilenir.
        private readonly Dictionary<string, Color> _colorCache = new();

        /// <summary>Boyanmış harita dokusu (henüz boyanmadıysa null).</summary>
        public Texture2D Texture => _texture;

        /// <summary>Doku yeniden ÜRETİLDİ (boyut değişti) — görüntüleyen kendini yeniden bağlar.</summary>
        public event System.Action OnTextureRebuilt;

        public MinimapStyleSO Style => _style;

        private void OnEnable()
        {
            if (_grid != null) _grid.OnGridRegenerated += HandleGridRegenerated;
        }

        private void OnDisable()
        {
            if (_grid != null) _grid.OnGridRegenerated -= HandleGridRegenerated;
        }

        // Grid savaş arenasına da dönüşüyor. Arena haritası minimap'e YAZILMAZ: menü savaştayken
        // açılırsa oyuncu overworld haritasını görmeye devam etsin (son iyi doku korunur).
        private void HandleGridRegenerated()
        {
            if (InOverworld) Rebuild();
        }

        private bool InOverworld => _state == null || _state.State == GameState.Overworld
                                                   || _state.State == GameState.ConfirmMission;

        // ── Boyama ───────────────────────────────────────────────────────────

        /// <summary>Haritayı sıfırdan boyar. Harita ekranı açılınca çağrılır — o an sis, düğümler
        /// ve karo tipleri neyse doku onu gösterir.</summary>
        public void Rebuild()
        {
            // EDİTÖRDE ÇALIŞMAZ: TAM KURULUM'un son adımı haritayı editörde üretiyor ve
            // OnGridRegenerated buraya düşüyor. Orada Texture2D üretmek hem sızdırır hem
            // Destroy(...) edit modda hata verir. Miniharita bir OYUN görseli — Play'de kurulur.
            if (!Application.isPlaying) return;
            if (_grid == null || !_grid.HasCells || !InOverworld) return;

            _colorCache.Clear();          // karo modeli/materyali değişmiş olabilir
            if (!EnsureTexture()) return;

            Color32 empty = _style != null ? (Color32)_style.VoidColor : new Color32(0, 0, 0, 0);
            for (int i = 0; i < _buffer.Length; i++) _buffer[i] = empty;

            foreach (var kv in _grid.Cells) PaintCell(kv.Key);

            _texture.SetPixels32(_buffer);
            _texture.Apply(false);
        }

        /// <summary>Dokuyu (gerekiyorsa) yeniden yaratır ve dünya→piksel dönüşümünü kurar.
        /// Boyut haritanın DÜNYA SINIRLARINDAN türer → 36×34 kıta da 10×8 arena da doğru oturur.</summary>
        private bool EnsureTexture()
        {
            _ppu = _style != null ? _style.PixelsPerUnit : 8f;
            float hexSize = _grid.HexSize;
            float outer   = HexMetrics.OuterRadius * hexSize;
            float inner   = HexMetrics.InnerRadius * hexSize;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var kv in _grid.Cells)
            {
                Vector3 p = kv.Value.WorldPosition;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }
            if (minX > maxX) return false;

            _minX = minX - inner;
            _minZ = minZ - outer;

            int w = Mathf.Max(8, Mathf.CeilToInt((maxX + inner - _minX) * _ppu));
            int h = Mathf.Max(8, Mathf.CeilToInt((maxZ + outer - _minZ) * _ppu));

            if (_texture == null || _w != w || _h != h)
            {
                _w = w; _h = h;
                _buffer = new Color32[w * h];

                if (_texture != null) Destroy(_texture);
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name       = "MinimapTexture",
                    // İRİ PİKSEL kasıtlı: doku ekranda büyütülerek gösterilir, bilinear
                    // filtreleme onu bulanık bir lekeye çevirirdi. Point = keskin harita hissi.
                    filterMode = FilterMode.Point,
                    wrapMode   = TextureWrapMode.Clamp
                };
                OnTextureRebuilt?.Invoke();
            }
            return true;
        }

        private void PaintCell(HexCoordinate coord)
        {
            if (!_grid.TryGetCell(coord, out HexCell cell)) return;

            FogState fog = _fog != null ? _fog.GetFogState(coord) : FogState.Visible;

            Color color;
            if (fog == FogState.Hidden)
            {
                // Keşfedilmemiş: karo tipi SIZDIRILMAZ — düz "bilinmiyor" rengi.
                color = _style != null ? _style.UnexploredColor : new Color(0.2f, 0.18f, 0.14f, 0.5f);
            }
            else
            {
                string id = TileIdAt(coord);
                color = ColorOf(id) * ShadeOf(coord, id) * DitherOf(coord);
                if (fog != FogState.Visible)
                    color *= _style != null ? _style.ExploredDim : 0.62f;
                color.a = 1f;
            }

            Stamp(cell.WorldPosition, color);
        }

        /// <summary>Bir altıgeni dokuya basar. Piksel→dünya dönüşümüyle nokta-altıgen içinde mi
        /// testi yapılır: komşu altıgenler arada boşluk kalmadan birleşir.</summary>
        private void Stamp(Vector3 center, Color color)
        {
            float hexSize = _grid.HexSize;
            float R = HexMetrics.OuterRadius * hexSize;
            float I = HexMetrics.InnerRadius * hexSize;
            float slope = 0.5f * R / I;                 // eğik kenarların eğimi

            float edgeDarken = _style != null ? _style.EdgeDarken : 0.16f;
            float shrink     = 1f - Mathf.Clamp01(1.2f / (_ppu * R));   // kenar bandı ≈ 1 piksel

            int px0 = Mathf.Max(0,      Mathf.FloorToInt((center.x - I - _minX) * _ppu));
            int px1 = Mathf.Min(_w - 1, Mathf.CeilToInt ((center.x + I - _minX) * _ppu));
            int py0 = Mathf.Max(0,      Mathf.FloorToInt((center.z - R - _minZ) * _ppu));
            int py1 = Mathf.Min(_h - 1, Mathf.CeilToInt ((center.z + R - _minZ) * _ppu));

            var body = (Color32)color;
            var edge = (Color32)(new Color(color.r * (1f - edgeDarken),
                                           color.g * (1f - edgeDarken),
                                           color.b * (1f - edgeDarken), color.a));

            for (int py = py0; py <= py1; py++)
            {
                float wz = _minZ + (py + 0.5f) / _ppu;
                float dz = Mathf.Abs(wz - center.z);
                int   row = py * _w;

                for (int px = px0; px <= px1; px++)
                {
                    float wx = _minX + (px + 0.5f) / _ppu;
                    float dx = Mathf.Abs(wx - center.x);

                    if (dx > I || dz > R - slope * dx) continue;          // altıgenin dışı

                    bool inner = edgeDarken <= 0.001f
                              || (dx <= I * shrink && dz <= (R - slope * dx) * shrink);
                    _buffer[row + px] = inner ? body : edge;
                }
            }
        }

        // ── Renk / ton ───────────────────────────────────────────────────────

        private string TileIdAt(HexCoordinate coord)
            => _grid.TileMap != null ? _grid.TileMap.GetTileId(coord) : null;

        /// <summary>Karonun minimap rengi. ÖNCE paletteki prefabın zemin materyali sorulur —
        /// böylece karonun modelini değiştirmek minimap'i de değiştirir. Prefab yoksa palet
        /// editör rengi, o da yoksa katalog rengi.</summary>
        private Color ColorOf(string tileId)
        {
            if (string.IsNullOrEmpty(tileId)) return Color.gray;
            if (_colorCache.TryGetValue(tileId, out Color cached)) return cached;

            Color c = ResolveColor(tileId);
            _colorCache[tileId] = c;
            return c;
        }

        private Color ResolveColor(string tileId)
        {
            TilePaletteSO.TileEntry entry = _grid.TilePalette != null
                ? _grid.TilePalette.GetById(tileId) : null;

            if (entry != null && entry.prefab != null)
            {
                Renderer r = entry.prefab.GetComponent<Renderer>()
                          ?? entry.prefab.GetComponentInChildren<Renderer>(true);
                Material m = r != null ? r.sharedMaterial : null;
                if (m != null)
                {
                    if (m.HasProperty(BaseColorId)) return Opaque(m.GetColor(BaseColorId));
                    if (m.HasProperty(ColorId))     return Opaque(m.GetColor(ColorId));
                }
            }

            if (entry != null) return Opaque(entry.editorColor);

            TileCatalog.Entry cat = TileCatalog.Get(tileId);
            return cat != null ? new Color(cat.R, cat.G, cat.B, 1f) : Color.gray;
        }

        private static Color Opaque(Color c) { c.a = 1f; return c; }

        /// <summary>MINECRAFT KURALI: karo, KUZEY-BATIDAKİ komşusundan alçaksa koyu, eşitse orta,
        /// yüksekse parlak tona boyanır. Işık hep aynı yönden gelir → arazi kabartmalı okunur.</summary>
        private float ShadeOf(HexCoordinate coord, string tileId)
        {
            if (_style == null) return 1f;

            // Kuzey-batı komşusu: axial (-1, +1) → dokuda yukarı-sol. Işığın geldiği yön budur.
            var north = new HexCoordinate(coord.Q - 1, coord.R + 1);
            if (!_grid.Cells.ContainsKey(north)) return _style.ShadeEqual;

            int here  = ElevationOf(tileId);
            int there = ElevationOf(TileIdAt(north));

            if (here < there) return _style.ShadeLower;
            if (here > there) return _style.ShadeHigher;
            return _style.ShadeEqual;
        }

        /// <summary>Karonun "yükseklik" sınıfı — gerçek yükseklik verisi yok, karo AİLESİNDEN
        /// türetilir. Kabartma için üç kademe yeter: su &lt; düzlük/taşlık &lt; orman &lt; dağ.</summary>
        private static int ElevationOf(string tileId)
        {
            TileCatalog.Entry e = TileCatalog.Get(tileId);
            if (e == null) return 1;

            return e.Family switch
            {
                TileFamily.Fringe   => 0,
                TileFamily.Void     => 0,
                TileFamily.Nature   => 2,
                TileFamily.Mountain => 3,
                _                   => 1
            };
        }

        /// <summary>Karo başına küçük, DETERMİNİSTİK parlaklık oynaması — geniş düz renk alanları
        /// (ova denizi) cansız durmasın. Koordinattan türer: aynı harita her açılışta aynı görünür.</summary>
        private float DitherOf(HexCoordinate coord)
        {
            float amount = _style != null ? _style.Dither : 0f;
            if (amount <= 0.0001f) return 1f;

            int hash = (coord.Q * 73856093) ^ (coord.R * 19349663);
            float t = ((hash >> 3) & 0xFF) / 255f;          // 0..1
            return 1f + (t - 0.5f) * 2f * amount;
        }

        // ── Konum dönüşümü (ikon yerleşimi için) ─────────────────────────────

        /// <summary>Karonun doku içindeki normalize konumu (0..1). İkon katmanı bunu kullanır.</summary>
        public bool TryGetUV(HexCoordinate coord, out Vector2 uv)
        {
            uv = default;
            if (_texture == null || _grid == null) return false;
            if (!_grid.TryGetCell(coord, out HexCell cell)) return false;

            uv = new Vector2(((cell.WorldPosition.x - _minX) * _ppu) / _w,
                             ((cell.WorldPosition.z - _minZ) * _ppu) / _h);
            return true;
        }

        /// <summary>Dünya konumunun doku içindeki normalize karşılığı (oyuncu imleci için —
        /// karolar arasında yürürken de akıcı).</summary>
        public bool TryGetUV(Vector3 world, out Vector2 uv)
        {
            uv = default;
            if (_texture == null) return false;
            uv = new Vector2(((world.x - _minX) * _ppu) / _w,
                             ((world.z - _minZ) * _ppu) / _h);
            return true;
        }

        /// <summary>TERS DÖNÜŞÜM: dokudaki normalize noktanın hangi karoya düştüğü. Haritaya
        /// tıklayıp karo seçmek bunu kullanır. Dönüşüm 3B haritadakiyle AYNI fonksiyondan geçer
        /// (<see cref="HexGridManager.WorldToHex"/>) → minimap ile sahne asla ayrışmaz.</summary>
        public bool TryGetCoordAt(Vector2 uv, out HexCoordinate coord)
        {
            coord = default;
            if (_texture == null || _grid == null) return false;

            float wx = _minX + uv.x * _w / _ppu;
            float wz = _minZ + uv.y * _h / _ppu;
            coord = _grid.WorldToHex(new Vector3(wx, 0f, wz));
            return true;
        }

        /// <summary>Bir karonun DOKUDAKİ piksel ölçüsü (genişlik, yükseklik). Seçim halkası ve
        /// rota noktaları karo boyutunda çizilsin diye gerekir; ekran boyutu buna dokunun ekrandaki
        /// ölçek çarpanı uygulanarak bulunur.</summary>
        public Vector2 HexPixelSize
        {
            get
            {
                float hexSize = _grid != null ? _grid.HexSize : 1f;
                return new Vector2(2f * HexMetrics.InnerRadius * hexSize * _ppu,
                                   2f * HexMetrics.OuterRadius * hexSize * _ppu);
            }
        }
    }
}
