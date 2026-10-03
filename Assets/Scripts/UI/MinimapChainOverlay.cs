using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HİKAYE ZİNCİRLERİNİ MİNİMAPTE ÇİZER (Efe'nin isteği 2026-10-03). 3B haritada zincir YOK.
    ///
    /// SİS KURALI: çizgi yalnız KEŞFEDİLMİŞ karoların üstünde görünür. İki alan arasındaki yol
    /// sisin içine giriyorsa çizgi SİSİN KENARINA KADAR ilerler ve orada kesilir — oyuncu zincirin
    /// nereye uzandığını sezer ama ucunda ne olduğunu keşfetmeden göremez. Zorunlu görevlerin
    /// İKONU sisten bağımsız görünse de aralarındaki altın zincir aynı kurala uyar.
    ///
    /// Nasıl kırpılır: her parça karo başına <see cref="_samplesPerHex"/> örneğe bölünür; örneğin
    /// düştüğü karo keşfedilmişse o dilim görünür. Hücresi olmayan noktalar (kıyı girintisi,
    /// harita boşluğu) bir önceki dilimin görünürlüğünü devralır — çizgi bir koyun üstünden
    /// geçerken anlamsızca kopmasın.
    ///
    /// Maliyet: yalnız panel AÇIKKEN, sis yeni karo açtığında (sınırlı sıklıkta) ya da zincirler
    /// değişince yeniden hesaplanır — <see cref="MinimapView"/>'ın arazi tazelemesiyle aynı kural.
    ///
    /// RENK + VURGU (Efe, 2026-10-03): her savaş zinciri stil paletinden KENDİ rengini alır,
    /// zorunlu zincir altın kalır. Fare bir zincir çizgisinin ya da o zincirdeki bir görev
    /// ikonunun üstüne gelince o zincir kalınlaşır ve nabız gibi parlar. İsabet yalnız
    /// GÖRÜNEN şeylere bakar: sisin içindeki ikon ya da kırpılmış çizgi parçası seçilmez.
    /// </summary>
    public class MinimapChainOverlay : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private MinimapRenderer   _renderer;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private FogOfWarManager   _fog;
        [SerializeField] private StoryChainManager _chains;
        [SerializeField] private MinimapStyleSO    _style;

        [Header("Katman")]
        [Tooltip("İkon katmanı — zincir onun EN ALT çocuğu olur ki ikonlar üstünde kalsın.")]
        [SerializeField] private RectTransform       _iconLayer;
        [Tooltip("Boşsa çalışma zamanında ikon katmanının altına kurulur (ikonlar da öyle kuruluyor).")]
        [SerializeField] private MinimapChainGraphic _graphic;

        [Header("Hesap")]
        [Tooltip("Sis kırpmasının inceliği: bir karo boyunca kaç örnek alınır.")]
        [SerializeField, Range(2, 12)] private int _samplesPerHex = 4;
        [Tooltip("Sis değişince en sık bu aralıkla yeniden çizilir (sn).")]
        [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

        private readonly List<MinimapChainGraphic.Segment> _segments = new();

        // Fare isabeti için görünen görev ikonları (UV + zincir kimliği).
        private readonly List<NodeHit> _nodeHits = new();
        private struct NodeHit { public Vector2 Uv; public int Chain; }

        private RectMask2D _viewport;      // harita penceresi: dışındaki fare sayılmaz
        private int   _fogVersion = -1;
        private float _nextRefresh;
        private bool  _dirty;

        private void OnEnable()
        {
            if (_chains   != null) _chains.OnChainsChanged     += MarkDirty;
            if (_renderer != null) _renderer.OnTextureRebuilt  += MarkDirty;
            EnsureGraphic();
            Redraw();
        }

        private void OnDisable()
        {
            if (_chains   != null) _chains.OnChainsChanged     -= MarkDirty;
            if (_renderer != null) _renderer.OnTextureRebuilt  -= MarkDirty;
            if (_graphic  != null) _graphic.SetHighlight(-1, 1f, 1f, 0f);   // panel kapanırken vurgu kalmasın
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            bool fogChanged = _fog != null && _fog.ExplorationVersion != _fogVersion;
            if (fogChanged && Time.unscaledTime >= _nextRefresh) _dirty = true;
            if (_dirty) Redraw();
            UpdateHover();
        }

        // ── Çizim ────────────────────────────────────────────────────────────

        private void Redraw()
        {
            _dirty       = false;
            _fogVersion  = _fog != null ? _fog.ExplorationVersion : 0;
            _nextRefresh = Time.unscaledTime + _refreshInterval;
            if (_graphic == null) return;

            _segments.Clear();
            _nodeHits.Clear();
            if (_chains != null && _renderer != null && _renderer.Texture != null && _grid != null)
            {
                Color gold = _style != null ? _style.MandatoryChainColor : new Color(1f, 0.84f, 0.30f, 0.75f);

                // Kimlik: savaş zincirleri 0..N-1, zorunlu zincir N.
                for (int i = 0; i < _chains.Chains.Count; i++)
                {
                    Color c = _style != null ? _style.ChainColorAt(i) : new Color(0.96f, 0.90f, 0.78f, 0.55f);
                    AddPath(_chains.Chains[i].Steps, c, i, mandatory: false);
                }
                AddPath(_chains.MandatoryChain, gold, _chains.Chains.Count, mandatory: true);
            }

            _graphic.SetSegments(_segments,
                                 _style != null ? _style.ChainThickness : 2.5f,
                                 _style != null ? _style.ChainLink      : 8f,
                                 _style != null ? _style.ChainGap       : 5f);
        }

        private void AddPath(IReadOnlyList<ChapterNodeManager.MapNode> steps, Color color, int chain, bool mandatory)
        {
            for (int i = 1; i < steps.Count; i++)
                AddClipped(steps[i - 1].Coord, steps[i].Coord, color, chain);

            // Tek adımlı zorunlu zincirin çizgisi yok — ikonu da vurgu tetiklemesin.
            if (steps.Count < 2) return;
            foreach (var n in steps)
            {
                // Zorunlu görev ikonu sisten bağımsız görünür; savaş alanınınki yalnız keşfedilince.
                bool iconVisible = mandatory || _fog == null || _fog.IsKnown(n.Coord);
                if (iconVisible && _renderer.TryGetUV(n.Coord, out Vector2 uv))
                    _nodeHits.Add(new NodeHit { Uv = uv, Chain = chain });
            }
        }

        /// <summary>a→b parçasını yalnız keşfedilmiş karoların üstünde kalan dilimleriyle ekler.</summary>
        private void AddClipped(HexCoordinate a, HexCoordinate b, Color color, int chain)
        {
            if (!_grid.TryGetCell(a, out HexCell ca) || !_grid.TryGetCell(b, out HexCell cb)) return;
            Vector3 pa = ca.WorldPosition, pb = cb.WorldPosition;
            if (!_renderer.TryGetUV(pa, out Vector2 ua) || !_renderer.TryGetUV(pb, out Vector2 ub)) return;

            int   steps    = Mathf.Max(2, a.DistanceTo(b) * _samplesPerHex);
            bool  inRun    = false, prevVisible = false;
            float runStart = 0f;

            for (int k = 0; k < steps; k++)
            {
                float t0 = k / (float)steps, t1 = (k + 1) / (float)steps;
                HexCoordinate hex = _grid.WorldToHex(Vector3.Lerp(pa, pb, (t0 + t1) * 0.5f));
                bool visible = _grid.TryGetCell(hex, out _) ? (_fog == null || _fog.IsKnown(hex)) : prevVisible;
                prevVisible = visible;

                if (visible && !inRun) { inRun = true; runStart = t0; }
                else if (!visible && inRun) { inRun = false; Add(ua, ub, runStart, t0, color, chain); }
            }
            if (inRun) Add(ua, ub, runStart, 1f, color, chain);
        }

        private void Add(Vector2 ua, Vector2 ub, float t0, float t1, Color color, int chain)
            => _segments.Add(new MinimapChainGraphic.Segment
               {
                   A = Vector2.Lerp(ua, ub, t0),
                   B = Vector2.Lerp(ua, ub, t1),
                   Color = color,
                   Chain = chain
               });

        // ── Vurgu ────────────────────────────────────────────────────────────

        /// <summary>
        /// Farenin altındaki zinciri bulur: önce GÖREV İKONU (ikon yarıçapı içinde), yoksa ÇİZGİ
        /// (yakınlık yarıçapı içinde). Vurgu sürdükçe mesh her kare yeniden kurulur (nabız) —
        /// birkaç düzine dörtgen, ucuz; vurgu yokken hiçbir şey kurulmaz.
        /// </summary>
        private void UpdateHover()
        {
            if (_graphic == null) return;

            int hover = -1;
            if (TryMouseLocal(out Vector2 p))
            {
                float best  = float.MaxValue;
                float iconR = (_style != null ? _style.IconSize : 26f) * 0.5f + 2f;
                float lineR = _style != null ? _style.ChainHoverRadius : 8f;

                foreach (NodeHit h in _nodeHits)
                {
                    float d = (_graphic.UvToLocal(h.Uv) - p).magnitude;
                    if (d <= iconR && d < best) { best = d; hover = h.Chain; }
                }

                if (hover < 0)
                    foreach (MinimapChainGraphic.Segment s in _segments)
                    {
                        float d = DistanceToSegment(p, _graphic.UvToLocal(s.A), _graphic.UvToLocal(s.B));
                        if (d <= lineR && d < best) { best = d; hover = s.Chain; }
                    }
            }

            _graphic.SetHighlight(hover,
                                  _style != null ? _style.ChainHoverThickness : 2.2f,
                                  _style != null ? _style.ChainGlowWidth      : 5f,
                                  _style != null ? _style.ChainPulseSpeed     : 4f);
            if (hover >= 0) _graphic.SetVerticesDirty();   // nabız animasyonu
        }

        /// <summary>Fare, harita penceresinin İÇİNDEYSE grafiğin yerel koordinatı.</summary>
        private bool TryMouseLocal(out Vector2 local)
        {
            local = default;
            Vector2 mouse = Input.mousePosition;
            Canvas canvas = _graphic.canvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                       ? canvas.worldCamera : null;

            if (_viewport != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(_viewport.rectTransform, mouse, cam))
                return false;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       _graphic.rectTransform, mouse, cam, out local);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
            return (a + ab * t - p).magnitude;
        }

        /// <summary>Çizgi katmanı yoksa ikon katmanının EN ALTINA kurar (ikonlar üstte kalsın).</summary>
        private void EnsureGraphic()
        {
            if (_graphic != null)
            {
                if (_viewport == null) _viewport = _graphic.GetComponentInParent<RectMask2D>();
                return;
            }
            if (_iconLayer == null) return;

            var go = new GameObject("ZincirKatmani", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_iconLayer, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();

            _graphic = go.AddComponent<MinimapChainGraphic>();
            _graphic.raycastTarget = false;
            _viewport = _graphic.GetComponentInParent<RectMask2D>();
        }
    }
}
