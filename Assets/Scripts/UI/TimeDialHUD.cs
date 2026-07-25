using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ZAMAN SAYACI (sol üst) — bir günü 6 dilime bölen dairesel kadran + altında "GÜN N" bandı.
    ///
    /// Her 9 AP harcandığında bir dilim TAK diye ilerler (yumuşak geçiş YOK — anlık/kesikli).
    /// İlk 4 dilim GÜNDÜZ (sıcak sarı), son 2 dilim GECE (koyu mavi) renginde çizilir; merkezdeki
    /// simge gündüz GÜNEŞ, gece AY olur. Geçmiş dilimler soluk, sıradaki dilimler koyu, içinde
    /// bulunulan dilim parlak + dış halkada işaretçi.
    ///
    /// Kadran görüntüsü prosedürel bir Texture2D'dir (sanat asset'i gerekmez) ve yalnız
    /// gün/dilim DEĞİŞTİĞİNDE yeniden üretilir — her karede değil.
    /// Çizim <see cref="HudScale"/> sanal ekranına yapılır → 1080p/4K/geniş ekran fark etmez.
    /// Geçici whitebox IMGUI (projenin diğer HUD'ları gibi) — cila aşamasında uGUI'ye taşınabilir.
    /// </summary>
    public class TimeDialHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager _apManager;
        [Tooltip("Opsiyonel — atanmışsa sayaç yalnız Overworld/görev onayında çizilir (savaşta gizlenir).")]
        [SerializeField] private GameStateManager _stateManager;

        [Header("Yerleşim (1920x1080 sanal ekran)")]
        [SerializeField] private float _dialSize   = 132f;
        [SerializeField] private float _marginLeft = 16f;
        [SerializeField] private float _marginTop  = 12f;
        [Tooltip("Kadranın altındaki GÜN bandının yüksekliği.")]
        [SerializeField] private float _bannerHeight = 26f;

        [Header("Renkler — Gündüz / Gece")]
        [SerializeField] private Color _dayActive   = new(1f,    0.84f, 0.42f, 1f);
        [SerializeField] private Color _dayIdle     = new(0.52f, 0.42f, 0.24f, 1f);
        [SerializeField] private Color _nightActive = new(0.55f, 0.68f, 1f,    1f);
        [SerializeField] private Color _nightIdle   = new(0.20f, 0.26f, 0.44f, 1f);
        [Tooltip("Bu günde ARTIK GEÇMİŞ olan dilimler (harcanmış zaman).")]
        [SerializeField] private Color _spentTint   = new(0.30f, 0.30f, 0.33f, 1f);
        [SerializeField] private Color _frameColor  = new(0.14f, 0.12f, 0.10f, 1f);
        [SerializeField] private Color _inkColor    = new(0.06f, 0.05f, 0.05f, 1f);

        // ── Önbellek ─────────────────────────────────────────────────────────
        // Kadranın görüntüsü YALNIZ dilime bağlı (gün sayısı bandda yazıyla çiziliyor) → her dilim
        // için doku bir kez üretilip saklanır. Böylece ilk günden sonra hiç yeniden üretim olmaz.
        private Texture2D[] _dialBySlot;

        private Texture2D _whiteTex;   // düz dikdörtgen çizimi için
        private GUIStyle  _bannerStyle;

        private void OnDestroy()
        {
            if (_dialBySlot != null)
                foreach (Texture2D t in _dialBySlot)
                    if (t != null) Destroy(t);
            if (_whiteTex != null) Destroy(_whiteTex);
        }

        private void OnGUI()
        {
            if (_apManager == null) return;
            if (_stateManager != null &&
                _stateManager.State != GameState.Overworld &&
                _stateManager.State != GameState.ConfirmMission) return;

            EnsureStyles();

            int day  = _apManager.CurrentDay;
            int slot = _apManager.CurrentSlot;

            Texture2D dialTex = GetDialTexture(slot);
            if (dialTex == null) return;

            using (HudScale.Scaled())
            {
                var dial   = new Rect(_marginLeft, _marginTop, _dialSize, _dialSize);
                var banner = new Rect(_marginLeft + _dialSize * 0.12f, dial.yMax + 2f,
                                      _dialSize * 0.76f, _bannerHeight);

                // Sayaç üstündeki tık haritaya sızmasın (Update, OnGUI'den ÖNCE çalışır).
                ImguiBlocker.Register(dial);
                ImguiBlocker.Register(banner);

                GUI.DrawTexture(dial, dialTex, ScaleMode.StretchToFill, true);
                DrawBanner(banner, day);
            }
        }

        // ── GÜN bandı ────────────────────────────────────────────────────────

        private void DrawBanner(Rect r, int day)
        {
            Color bg = _frameColor;
            bg.a = 0.92f;

            DrawRect(r, bg);
            DrawOutline(r, _inkColor, 2f);

            _bannerStyle.normal.textColor = Color.white;
            GUI.Label(r, $"GÜN {day}", _bannerStyle);
        }

        // ── Kadran dokusunun prosedürel üretimi ──────────────────────────────

        /// <summary>Dilimin kadran dokusu — ilk istendiğinde üretilir, sonra önbellekten döner.</summary>
        private Texture2D GetDialTexture(int slot)
        {
            int slots = Mathf.Max(1, _apManager.SlotsPerDay);
            if (_dialBySlot == null || _dialBySlot.Length != slots)
                _dialBySlot = new Texture2D[slots];

            int i = Mathf.Clamp(slot, 0, slots - 1);
            if (_dialBySlot[i] == null) _dialBySlot[i] = BuildDial(i, slots);
            return _dialBySlot[i];
        }

        private Texture2D BuildDial(int slot, int slots)
        {
            // Doku 4K'da da net kalsın diye sanal boyutun 3 katı çözünürlükte üretilir
            // (4K + UiScale 1.5 → kadran ekranda ~3x piksel kaplar).
            int size = Mathf.Max(64, Mathf.RoundToInt(_dialSize * 3f));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            bool  night  = _apManager.IsNightSlot(slot);
            var   pixels = new Color32[size * size];
            float half   = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 2x2 süper-örnekleme → kenarlar pürüzsüz (IMGUI'de MSAA yok).
                    Color acc = default;
                    for (int s = 0; s < 4; s++)
                    {
                        float ox = (s & 1) == 0 ? 0.25f : 0.75f;
                        float oy = (s & 2) == 0 ? 0.25f : 0.75f;
                        acc += SampleDial((x + ox - half) / half, (y + oy - half) / half,
                                          slot, slots, night);
                    }
                    pixels[y * size + x] = acc * 0.25f;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>Kadranın tek bir noktasının rengi. ux/uy: merkez 0, kenar ±1.</summary>
        private Color SampleDial(float ux, float uy, int slot, int slots, bool night)
        {
            float r = Mathf.Sqrt(ux * ux + uy * uy);
            if (r > 1f) return default;                       // daire dışı = saydam

            const float ringOuter = 0.99f, ringInner = 0.80f, centerR = 0.34f;

            if (r > ringInner)                                // dış çerçeve halkası
            {
                // İçinde bulunulan dilimin hizasına parlak işaretçi.
                float ang  = Angle(ux, uy);
                float step = Mathf.PI * 2f / slots;
                float mid  = (slot + 0.5f) * step;
                float diff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, mid * Mathf.Rad2Deg));
                if (diff < 7f && r > ringInner + 0.04f)
                    return night ? _nightActive : _dayActive;

                return r > ringOuter - 0.02f ? _inkColor : _frameColor;
            }

            if (r > centerR)                                  // dilim halkası
            {
                float ang  = Angle(ux, uy);
                float step = Mathf.PI * 2f / slots;
                int   idx  = Mathf.Clamp((int)(ang / step), 0, slots - 1);

                // Dilim ayırıcı çizgiler.
                float local = ang - idx * step;
                if (local < 0.035f || local > step - 0.035f) return _inkColor;
                if (r > ringInner - 0.03f || r < centerR + 0.025f) return _inkColor;

                bool slotNight = _apManager.IsNightSlot(idx);
                if (idx == slot)     return slotNight ? _nightActive : _dayActive;   // ŞU AN
                if (idx <  slot)     return _spentTint;                              // geçti
                return slotNight ? _nightIdle : _dayIdle;                            // gelecek
            }

            // ── Merkez: gündüz GÜNEŞ, gece AY ──
            float cr = r / centerR;                            // 0..1 merkez disk içinde
            if (cr > 0.94f) return _inkColor;                  // merkez çemberin kenarı

            Color icon = night ? _nightActive : _dayActive;

            if (night)
            {
                // Hilal: disk EKSİ kaydırılmış disk.
                float d1 = r / centerR;
                float dx = (ux - centerR * 0.30f) / centerR;
                float dy = (uy + centerR * 0.10f) / centerR;
                float d2 = Mathf.Sqrt(dx * dx + dy * dy);
                return (d1 < 0.72f && d2 > 0.58f) ? icon : _frameColor;
            }

            // Güneş: iç disk + 8 ışın.
            if (cr < 0.46f) return icon;
            float a2 = Angle(ux, uy);
            return (cr < 0.86f && Mathf.Cos(a2 * 8f) > 0.55f) ? icon : _frameColor;
        }

        /// <summary>Tepeden (12 yönü) saat yönünde 0..2π açı.</summary>
        private static float Angle(float ux, float uy)
        {
            float a = Mathf.Atan2(ux, uy);
            return a < 0f ? a + Mathf.PI * 2f : a;
        }

        // ── IMGUI yardımcıları ───────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            _bannerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 15,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _whiteTex);
            GUI.color = old;
        }

        private void DrawOutline(Rect r, Color c, float w)
        {
            DrawRect(new Rect(r.x,        r.y,        r.width, w),        c);
            DrawRect(new Rect(r.x,        r.yMax - w, r.width, w),        c);
            DrawRect(new Rect(r.x,        r.y,        w,       r.height), c);
            DrawRect(new Rect(r.xMax - w, r.y,        w,       r.height), c);
        }
    }
}
