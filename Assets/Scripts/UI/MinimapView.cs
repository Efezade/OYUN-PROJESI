using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HARİTA EKRANINDAKİ MİNİHARİTA — <see cref="MinimapRenderer"/>'ın boyadığı dokuyu gösterir
    /// ve üstüne ÖNEMLİ KAROLARIN işaretlerini yerleştirir (market, savaş alanı, zindan, zorunlu
    /// görev, kule, öz yatakları, oyuncunun konumu).
    ///
    /// İŞARETLER DOKUYA GÖMÜLMEZ, ayrı UI nesneleridir. Neden: doku bilerek iri pikselli
    /// (arazi öyle görünsün diye) ama işaretlerin OKUNAKLI olması gerek. Ayrı katman ayrıca
    /// işareti tıklanabilir/ipucu gösterir yapmayı ileride mümkün kılar.
    ///
    /// SİS KURALI: yalnız KEŞFEDİLMİŞ karoların işareti çizilir (kullanıcı isteği: "sisli yerler
    /// gözükmesin"). Tek istisna zorunlu görevler — onlar tasarım gereği sisten bağımsız görünür
    /// (GAME_DESIGN §3, <see cref="ChapterNodeManager.IsMarkerVisible"/> ile aynı kural).
    ///
    /// Panel her açıldığında yeniden kurulur: harita ekranı kapalıyken hiçbir şey hesaplanmaz.
    /// </summary>
    public class MinimapView : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private MinimapRenderer     _renderer;
        [SerializeField] private HexGridManager      _grid;
        [SerializeField] private FogOfWarManager     _fog;
        [SerializeField] private ChapterNodeManager  _nodes;
        [SerializeField] private EssenceFieldManager _field;
        [SerializeField] private PlayerController    _player;
        [SerializeField] private MinimapStyleSO      _style;

        [Header("Görsel")]
        [Tooltip("Harita dokusunun çizildiği alan. İkonlar bunun ÇOCUĞU olan katmana konur.")]
        [SerializeField] private RawImage      _image;
        [Tooltip("İşaretlerin konduğu katman — _image ile aynı dikdörtgeni kaplamalı.")]
        [SerializeField] private RectTransform _iconLayer;
        [Tooltip("Yakınlaştırma/kaydırma. Atanmışsa harita boyutunu O yönetir.")]
        [SerializeField] private MinimapPanZoom _panZoom;
        [Tooltip("Haritanın sığacağı en büyük kutu (piksel). Doku oranı korunarak buna oturtulur.")]
        [SerializeField] private Vector2 _maxSize = new(940f, 560f);
        [Tooltip("Harita henüz üretilmediyse gösterilecek yazı.")]
        [SerializeField] private TextMeshProUGUI _emptyLabel;
        [Tooltip("Öz işaretleri düğüm işaretlerinden daha küçük çizilir — önem sırası okunsun.")]
        [SerializeField, Range(0.3f, 1f)] private float _essenceIconScale = 0.62f;

        [Header("Açıklama şeridi (legend)")]
        [SerializeField] private LegendRow[] _legend;

        [System.Serializable]
        public struct LegendRow
        {
            public Image           icon;
            public MinimapIconKind kind;
        }

        private readonly List<GameObject> _icons = new();

        private void OnEnable()
        {
            if (_renderer != null) _renderer.OnTextureRebuilt += BindTexture;
            PaintLegend();
            Refresh();
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.OnTextureRebuilt -= BindTexture;
            ClearIcons();
        }

        /// <summary>Haritayı yeniden boyar ve işaretleri yeniden kurar. Panel her açıldığında
        /// çağrılır — kapalıyken hiçbir şey hesaplanmaz.</summary>
        public void Refresh()
        {
            if (_renderer != null) _renderer.Rebuild();
            BindTexture();
            RebuildIcons();
        }

        // ── Doku ─────────────────────────────────────────────────────────────

        private void BindTexture()
        {
            Texture2D tex = _renderer != null ? _renderer.Texture : null;
            bool ready = tex != null;

            if (_image != null)
            {
                _image.texture = tex;
                _image.enabled = ready;
                if (ready) FitToBox(tex);
            }
            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(!ready);
        }

        /// <summary>Dokuyu ORANI BOZMADAN kutuya oturtur — harita 36×34 de olsa 10×8 de olsa
        /// kareler kare kalır. Bu, yakınlaştırmanın 1× hâlidir; ötesini
        /// <see cref="MinimapPanZoom"/> yönetir.</summary>
        private void FitToBox(Texture2D tex)
        {
            float aspect = tex.height > 0 ? tex.width / (float)tex.height : 1f;
            float w = _maxSize.x, h = w / aspect;
            if (h > _maxSize.y) { h = _maxSize.y; w = h * aspect; }

            if (_panZoom != null)
            {
                _panZoom.SetBaseSize(new Vector2(w, h));
                _panZoom.ResetView();   // harita her açılışta tam görünür, ortalanmış
            }
            else
            {
                _image.rectTransform.sizeDelta = new Vector2(w, h);
            }
        }

        // ── İşaretler ────────────────────────────────────────────────────────

        private void ClearIcons()
        {
            foreach (GameObject go in _icons) if (go != null) Destroy(go);
            _icons.Clear();
        }

        private void RebuildIcons()
        {
            ClearIcons();
            if (_iconLayer == null || _renderer == null || _renderer.Texture == null) return;

            AddNodeIcons();
            AddPaintedTileIcons();
            AddEssenceIcons();
            AddPlayerIcon();
        }

        // Harita düğümleri: zorunlu görev · zindan · encounter · market · kule.
        // Boss KONUMSUZ (HUD düğmesi) → haritada işareti yok.
        private void AddNodeIcons()
        {
            if (_nodes == null) return;

            foreach (ChapterNodeManager.MapNode n in _nodes.Nodes)
            {
                if (!TryKindOf(n.Type, out MinimapIconKind kind)) continue;
                if (!_nodes.IsMarkerVisible(n)) continue;   // sis kuralı (zorunlu görev muaf)

                float alpha = n.Completed
                    ? (_style != null ? _style.CompletedIconAlpha : 0.38f)
                    : 1f;

                AddIcon(n.Coord, kind, ColorOf(kind), alpha, 1f);
            }
        }

        // Elle BOYANMIŞ mağaza / savaş karoları — düğüm sisteminden gelmezler ama oyuncu için
        // aynı derecede önemlidir (palette isStore / canEnterCombat bayrakları).
        private void AddPaintedTileIcons()
        {
            if (_grid == null || !_grid.HasCells) return;

            foreach (var kv in _grid.Cells)
            {
                HexCell cell = kv.Value;
                if (!cell.IsStore && !cell.CanEnterCombat) continue;
                if (!IsKnown(kv.Key)) continue;
                if (_nodes != null && _nodes.NodeAt(kv.Key) != null) continue;   // düğüm zaten çizdi

                MinimapIconKind kind = cell.IsStore ? MinimapIconKind.Market : MinimapIconKind.Encounter;
                AddIcon(kv.Key, kind, ColorOf(kind), 1f, 1f);
            }
        }

        // Öz yatakları — türünün renginde küçük bir taş.
        private void AddEssenceIcons()
        {
            if (_field == null) return;
            EssenceConfigSO cfg = _field.Config;

            foreach (EssenceFieldManager.Deposit d in _field.Deposits)
            {
                if (!IsKnown(d.Coord)) continue;
                Color c = cfg != null ? cfg.ColorOf(d.Type) : Color.white;
                AddIcon(d.Coord, MinimapIconKind.Essence, c, 1f, _essenceIconScale);
            }
        }

        // Oyuncu EN SON eklenir → her şeyin üstünde kalır. Konumu karo değil DÜNYA konumundan
        // alınır: karolar arası yürürken de doğru yerde durur.
        private void AddPlayerIcon()
        {
            if (_player == null || _renderer == null) return;
            if (!_renderer.TryGetUV(_player.transform.position, out Vector2 uv)) return;

            SpawnIcon(uv, MinimapIconKind.Player, ColorOf(MinimapIconKind.Player), 1f, 1.15f);
        }

        private void AddIcon(HexCoordinate coord, MinimapIconKind kind, Color color,
                             float alpha, float scale)
        {
            if (!_renderer.TryGetUV(coord, out Vector2 uv)) return;
            color.a = alpha;
            SpawnIcon(uv, kind, color, alpha, scale);
        }

        private void SpawnIcon(Vector2 uv, MinimapIconKind kind, Color color, float alpha, float scale)
        {
            var go = new GameObject($"Icon_{kind}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_iconLayer, false);

            // Anchor'ı UV'ye oturtmak, katman yeniden boyutlanınca işaretin de doğru yerde
            // kalmasını sağlar (elle piksel hesabı gerekmez). Kırpma: oyuncu haritanın dış
            // kenarındayken UV bir tık taşabilir, işaret panelin dışına kaçmasın.
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            rt.anchorMin = rt.anchorMax = uv;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            float size = (_style != null ? _style.IconSize : 26f) * scale;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.sprite = MinimapIcons.Get(kind);
            color.a = alpha;
            img.color = color;
            img.raycastTarget = false;

            _icons.Add(go);
        }

        private bool IsKnown(HexCoordinate coord) => _fog == null || _fog.IsKnown(coord);

        private static bool TryKindOf(MapNodeType type, out MinimapIconKind kind)
        {
            switch (type)
            {
                case MapNodeType.Market:     kind = MinimapIconKind.Market;     return true;
                case MapNodeType.Zindan:     kind = MinimapIconKind.Dungeon;    return true;
                case MapNodeType.Encounter:  kind = MinimapIconKind.Encounter;  return true;
                case MapNodeType.Mandatory:  kind = MinimapIconKind.Mandatory;  return true;
                case MapNodeType.Watchtower: kind = MinimapIconKind.Watchtower; return true;
                default:                     kind = MinimapIconKind.Market;     return false; // Boss konumsuz
            }
        }

        private Color ColorOf(MinimapIconKind kind)
        {
            if (_style == null) return Color.white;
            return kind switch
            {
                MinimapIconKind.Market     => _style.MarketColor,
                MinimapIconKind.Encounter  => _style.EncounterColor,
                MinimapIconKind.Dungeon    => _style.DungeonColor,
                MinimapIconKind.Mandatory  => _style.MandatoryColor,
                MinimapIconKind.Watchtower => _style.WatchtowerColor,
                MinimapIconKind.Player     => _style.PlayerColor,
                _                          => Color.white
            };
        }

        /// <summary>Açıklama şeridindeki örnek ikonları doldurur. Sprite'lar ÇALIŞMA ZAMANINDA
        /// üretildiği için editörde atanamaz — bağ burada kurulur.</summary>
        private void PaintLegend()
        {
            if (_legend == null) return;
            foreach (LegendRow row in _legend)
            {
                if (row.icon == null) continue;
                row.icon.sprite = MinimapIcons.Get(row.kind);
                row.icon.color  = row.kind == MinimapIconKind.Essence
                    ? new Color(0.55f, 0.80f, 0.45f)      // öz: türe göre değişir, örnekte doğa rengi
                    : ColorOf(row.kind);
                row.icon.raycastTarget = false;
            }
        }
    }
}
