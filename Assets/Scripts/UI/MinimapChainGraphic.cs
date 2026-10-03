using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Minimap üstündeki HİKAYE ZİNCİRİ çizgilerinin mesh'i: her parça kesik kesik "halkalar"
    /// olarak çizilir (zincir hissi, düz çizgiden daha hafif).
    ///
    /// Parçalar dokunun UV uzayında tutulur, mesh her kurulumda katmanın O ANKİ boyutuna
    /// çevrilir. Yakınlaştırma katmanın <c>sizeDelta</c>'sını büyüttüğü için Unity mesh'i
    /// kendiliğinden yeniden kurdurur → çizgi doğru yerde kalır ve kalınlığı ekran pikselinde
    /// sabit kalır (ikonların devleşmemesiyle aynı ilke).
    ///
    /// CANVASRENDERER ŞART (2026-10-03 hatası): bu bileşen CanvasRenderer'sız bir nesneye
    /// eklenmişti; RectMask2D kırpması her karede MissingComponentException attı ve TÜM
    /// arayüzün güncellemesi yarıda kaldı (HARİTA açılmadı, UI yanıp söndü). Awake eksik
    /// bileşeni kendisi ekler — eski sahneler de kendiliğinden onarılır.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MinimapChainGraphic : MaskableGraphic
    {
        public struct Segment
        {
            public Vector2 A, B;   // UV (0-1)
            public Color   Color;
            public int     Chain;  // vurgulama için zincir kimliği
        }

        private readonly List<Segment> _segments = new();
        private float _thickness = 2.5f;
        private float _link      = 8f;
        private float _gap       = 5f;

        // VURGU (fare zincirin ya da görevinin üstünde): kalınlaşır + nabız gibi parlar.
        private int   _highlight = -1;
        private float _hoverThicknessMul = 2.2f;
        private float _glowWidthMul      = 5f;
        private float _pulseSpeed        = 4f;

        /// <summary>Vurgulanan zincir (-1 = yok).</summary>
        public int Highlight => _highlight;

        /// <summary>Zinciri vurgular / vurguyu kaldırır (-1). Nabız animasyonu için çağıran
        /// vurgu sürdükçe her kare <see cref="Graphic.SetVerticesDirty"/> çağırır.</summary>
        public void SetHighlight(int chain, float thicknessMul, float glowWidthMul, float pulseSpeed)
        {
            _hoverThicknessMul = Mathf.Max(1f, thicknessMul);
            _glowWidthMul      = Mathf.Max(1f, glowWidthMul);
            _pulseSpeed        = Mathf.Max(0f, pulseSpeed);
            if (chain == _highlight) return;
            _highlight = chain;
            SetVerticesDirty();
        }

        /// <summary>UV → bu grafiğin yerel piksel uzayı (fare isabeti için).</summary>
        public Vector2 UvToLocal(Vector2 uv)
        {
            Rect r = rectTransform.rect;
            return r.min + Vector2.Scale(uv, r.size);
        }

        protected override void Awake()
        {
            if (!TryGetComponent(out CanvasRenderer _)) gameObject.AddComponent<CanvasRenderer>();
            base.Awake();
        }

        public void SetSegments(List<Segment> segments, float thickness, float link, float gap)
        {
            _segments.Clear();
            if (segments != null) _segments.AddRange(segments);
            _thickness = Mathf.Max(0.5f, thickness);
            _link      = Mathf.Max(1f, link);
            _gap       = Mathf.Max(0f, gap);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            // İki geçiş: önce sıradan zincirler, sonra VURGULANAN — vurgu her şeyin üstünde kalsın.
            foreach (Segment s in _segments)
                if (s.Chain != _highlight) AddDashed(vh, s, _thickness, s.Color);

            if (_highlight < 0) return;

            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * _pulseSpeed);
            foreach (Segment s in _segments)
            {
                if (s.Chain != _highlight) continue;

                // PARILTI: kesiksiz, geniş ve soluk iki katman — ışık gibi okunur.
                Color glow = s.Color; glow.a = 0.16f * pulse;
                AddSolid(vh, s, _thickness * _glowWidthMul, glow);
                glow.a = 0.30f * pulse;
                AddSolid(vh, s, _thickness * _glowWidthMul * 0.5f, glow);

                // ÇEKİRDEK: kalın, beyaza doğru açılmış, tam opak halkalar.
                Color core = Color.Lerp(s.Color, Color.white, 0.35f * pulse); core.a = 1f;
                AddDashed(vh, s, _thickness * _hoverThicknessMul, core);
            }
        }

        private void AddDashed(VertexHelper vh, Segment s, float thickness, Color c)
        {
            if (!Endpoints(s, thickness, out Vector2 a, out Vector2 dir, out Vector2 normal, out float len)) return;
            float period = _link + _gap;
            for (float t = 0f; t < len; t += period)
                AddQuad(vh, a + dir * t, a + dir * Mathf.Min(t + _link, len), normal, c);
        }

        private void AddSolid(VertexHelper vh, Segment s, float thickness, Color c)
        {
            if (!Endpoints(s, thickness, out Vector2 a, out Vector2 dir, out Vector2 normal, out float len)) return;
            AddQuad(vh, a, a + dir * len, normal, c);
        }

        private bool Endpoints(Segment s, float thickness, out Vector2 a, out Vector2 dir,
                               out Vector2 normal, out float len)
        {
            a = UvToLocal(s.A);
            Vector2 d = UvToLocal(s.B) - a;
            len = d.magnitude;
            dir = normal = Vector2.zero;
            if (len < 0.5f) return false;
            dir    = d / len;
            normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            return true;
        }

        private static void AddQuad(VertexHelper vh, Vector2 from, Vector2 to, Vector2 normal, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(from - normal, c, Vector2.zero);
            vh.AddVert(from + normal, c, Vector2.zero);
            vh.AddVert(to   + normal, c, Vector2.zero);
            vh.AddVert(to   - normal, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
