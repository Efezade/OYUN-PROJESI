using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ZORUNLU GÖREV ZİNCİRİ barı — ekranın en üstü (kullanıcı isteği 2026-08-28).
    ///
    /// OKUMA BİÇİMİ: her zorunlu görev bir ÇİZGİ. Görev bitince çizgi PARLAR; hepsi parlayınca
    /// boss taşı verilir ve zincir kapanır. Yeni bir görev açılmak üzereyken araya SOLUK bir
    /// hayalet çizgi girer ve kalan AP ile dolar; açılış anında sertçe yerine oturup çakar.
    ///
    /// NEDEN UYARI ŞART: oyunun ana kararı "zinciri şimdi mi kapatayım, yoksa büyüsün mü". Uyarı
    /// olmadan bu karar körlemesine verilirdi — oyuncu son görevini bitirmek üzereyken tepesine
    /// yeni bir görev düşer ve haksızlığa uğramış hissederdi. Geri sayım GÜN değil AP cinsinden:
    /// zaman yalnız oyuncu eylemde bulununca akıyor, harcanan birim de AP.
    ///
    /// TAKVİM SAYACI (Faz 1, 2026-10-03): açık görevlerin sağında, henüz açılmamış her görev için
    /// soluk bir kutu ve içinde açılış günü ("G8") durur — oyuncu zincirin TAMAMINI görür, yalnız
    /// sıradakini değil. Alt yazı gün cinsinden geri sayar ve ekonomi yolu açıksa kaç öz daha
    /// kazanınca sıradaki görevin erken düşeceğini söyler.
    ///
    /// Neden IMGUI: diğer overworld HUD'ları da IMGUI (ChapterRunHUD/ChapterNodeHUD) ve "parlama"
    /// uGUI'de zaten taklit edilmek zorunda (Graphic.color beyazda kırpılır, Overlay canvas bloom
    /// almaz — bkz proje tuzakları). Katmanlı çizim burada daha ucuz ve doğrudan.
    /// </summary>
    public class MandatoryQuestBarHUD : MonoBehaviour
    {
        [SerializeField] private MandatoryQuestDirector _director;
        [SerializeField] private GameStateManager       _state;

        [Header("Ölçüler (1920x1080 sanal ekran)")]
        [SerializeField] private float _segWidth  = 64f;
        [SerializeField] private float _segHeight = 16f;
        [SerializeField] private float _segGap    = 8f;
        [Tooltip("Henüz açılmamış görevlerin takvim kutusu genişliği.")]
        [SerializeField] private float _pipWidth  = 34f;

        [Header("Renk")]
        [SerializeField] private Color _gold = new(1.00f, 0.85f, 0.20f);

        [Header("Çakma süreleri (sn)")]
        [SerializeField] private float _unlockFlash = 1.6f;
        [SerializeField] private float _stoneFlash  = 2.4f;

        private float _unlockFlashUntil;
        private float _stoneFlashUntil;
        private int   _flashTier;

        private void OnEnable()
        {
            if (_director == null) return;
            _director.OnQuestUnlocked += HandleUnlocked;
            _director.OnStoneGranted  += HandleStone;
        }

        private void OnDisable()
        {
            if (_director == null) return;
            _director.OnQuestUnlocked -= HandleUnlocked;
            _director.OnStoneGranted  -= HandleStone;
        }

        private void HandleUnlocked(int tier, TacticalRPG.Grid.HexCoordinate coord)
        {
            _flashTier        = tier;
            _unlockFlashUntil = Time.unscaledTime + _unlockFlash;
        }

        private void HandleStone() => _stoneFlashUntil = Time.unscaledTime + _stoneFlash;

        // ── Çizim ────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // tam-ekran menü açıkken IMGUI çizilmez
            if (_director == null) return;
            if (_state != null && _state.State != GameState.Overworld) return;

            using var _scale = HudScale.Scaled();

            int open  = Mathf.Max(1, _director.OpenCount);
            int done  = _director.DoneCount;
            bool ghost = _director.WarningActive;
            int slots = open + (ghost ? 1 : 0);
            // Takvim kutuları: hayalet çizgi sıradaki açılışı zaten gösteriyorsa ondan SONRAKİLER.
            int pips  = Mathf.Max(0, _director.RemainingUnlocks - (ghost ? 1 : 0));

            float stripW = slots * _segWidth + (slots - 1) * _segGap + pips * (_pipWidth + _segGap);
            float cx     = HudScale.Width * 0.5f;
            float x0     = cx - stripW * 0.5f;
            float y0     = HudLayout.QuestBarY + 4f;

            var strip = new Rect(x0 - 14f, HudLayout.QuestBarY,
                                 stripW + 28f, HudLayout.QuestBarHeight);
            ImguiBlocker.Register(strip);   // bar üstündeki tık haritaya düşmesin

            DrawStoneFlourish(strip);

            for (int i = 0; i < open; i++)
            {
                var r = new Rect(x0 + i * (_segWidth + _segGap), y0, _segWidth, _segHeight);
                if (i < done) DrawDone(r, i + 1);
                else          DrawOpen(r);
            }

            if (ghost)
                DrawGhost(new Rect(x0 + open * (_segWidth + _segGap), y0, _segWidth, _segHeight));

            float px = x0 + slots * (_segWidth + _segGap);
            for (int i = 0; i < pips; i++)
            {
                int day = _director.UpcomingUnlockDay(i + (ghost ? 1 : 0));
                DrawPip(new Rect(px + i * (_pipWidth + _segGap), y0, _pipWidth, _segHeight), day);
            }

            DrawCaption(cx, y0 + _segHeight + 3f, done, open, ghost);
        }

        /// <summary>Bitmiş görev: dışa taşan hâle + dolu altın + sıcak çekirdek.</summary>
        private void DrawDone(Rect r, int tier)
        {
            // Yeni düşen görev değil, BİTEN görev parlar — hâle nefes alır ki bar canlı dursun.
            float breathe = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.2f + tier);
            Glow(r, _gold, breathe);
            Fill(r, _gold);
            Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f),
                 Color.Lerp(_gold, Color.white, 0.55f));
        }

        /// <summary>Açık ama bitmemiş görev: koyu gövde + altın çerçeve.</summary>
        private void DrawOpen(Rect r)
        {
            Fill(r, new Color(0.06f, 0.05f, 0.02f, 0.85f));
            Frame(r, new Color(_gold.r, _gold.g, _gold.b, 0.55f), 2f);
        }

        /// <summary>
        /// Hayalet çizgi: henüz AÇILMAMIŞ görev. Kalan AP azaldıkça SOLDAN SAĞA dolar — geri sayım
        /// hem sayı hem de görsel olarak okunur. Açılış anında bu çizgi solid hâle geçer.
        /// </summary>
        private void DrawGhost(Rect r)
        {
            var cfg = _director.Config;
            float warn = cfg != null ? Mathf.Max(1, cfg.WarningAP) : 24f;
            float k    = Mathf.Clamp01(1f - _director.APUntilNextUnlock / warn);
            float pulse = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);

            Fill(r, new Color(0.06f, 0.05f, 0.02f, 0.55f));
            Fill(new Rect(r.x, r.y, r.width * k, r.height),
                 new Color(_gold.r, _gold.g, _gold.b, 0.30f + 0.30f * k));
            Frame(r, new Color(_gold.r, _gold.g, _gold.b, pulse * (0.35f + 0.5f * k)), 1f);
        }

        /// <summary>Takvim kutusu: henüz AÇILMAMIŞ görev — soluk çerçeve + açılış günü.</summary>
        private void DrawPip(Rect r, int day)
        {
            Fill(r, new Color(0.06f, 0.05f, 0.02f, 0.35f));
            Frame(r, new Color(_gold.r, _gold.g, _gold.b, 0.25f), 1f);
            if (day <= 0) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(_gold.r, _gold.g, _gold.b, 0.6f);
            GUI.Label(r, $"G{day}", style);
        }

        /// <summary>Boss taşı verildiğinde tüm barı saran altın çerçeve (sönerek geçer).</summary>
        private void DrawStoneFlourish(Rect strip)
        {
            float left = _stoneFlashUntil - Time.unscaledTime;
            if (left <= 0f) return;
            float k = Mathf.Clamp01(left / Mathf.Max(0.01f, _stoneFlash));
            Glow(strip, _gold, k * 2f);
            Frame(strip, new Color(_gold.r, _gold.g, _gold.b, k), 2f);
        }

        private void DrawCaption(float cx, float y, int done, int open, bool ghost)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 15,
                fontStyle = FontStyle.Bold
            };

            string msg;
            float unlockLeft = _unlockFlashUntil - Time.unscaledTime;

            if (unlockLeft > 0f)
            {
                // Açılış anı: her şeyin önüne geçen tek satır, yanıp sönerek.
                style.normal.textColor = Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f
                                       ? Color.white : _gold;
                msg = (_director.LastUnlockByEconomy
                       ? $"EKONOMİ BÜYÜDÜ — {_flashTier}. ZORUNLU GÖREV ERKEN DÜŞTÜ"
                       : $"{_flashTier}. ZORUNLU GÖREV DÜŞTÜ")
                    + $" — boss taşı için artık {open} görev gerekiyor";
            }
            else if (_director.ChainClosed)
            {
                style.normal.textColor = _gold;
                msg = "BOSS TAŞI CEBİNDE — dilediğin an bossa gir · zincir kapandı, yeni zorunlu görev gelmeyecek";
            }
            else if (ghost)
            {
                style.normal.textColor = new Color(1f, 0.72f, 0.35f);
                int ap = _director.APUntilNextUnlock;
                msg = $"ZORUNLU GÖREV {done}/{open} · {open + 1}. görev {ap} AP sonra düşüyor"
                    + (_director.UnlockDeferred ? " (savaş bitince)" : "");
            }
            else
            {
                style.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                msg = $"ZORUNLU GÖREV {done}/{open}" + NextUnlockText(open)
                    + (done == open ? "" : " · hepsini bitir → boss taşı");
            }

            GUI.Label(new Rect(cx - 400f, y, 800f, 18f), msg, style);
        }

        /// <summary>" · 3. görev 2 gün sonra (gün 8) · ya da 40 öz daha kazan" — takvim + ekonomi.</summary>
        private string NextUnlockText(int open)
        {
            if (!_director.HasNextUnlock) return "";
            int days = _director.DaysUntilNextUnlock;
            string when = days <= 0 ? "bugün" : $"{days} gün sonra (gün {_director.NextUnlockDay})";
            string text = $" · {open + 1}. görev {when}";
            int essence = _director.EssenceUntilEconomyUnlock;
            if (essence > 0) text += $" · ya da {essence} öz daha kazan";
            return text;
        }

        // ── IMGUI çizim yardımcıları ─────────────────────────────────────────
        // uGUI'de beyazın ötesinde parlatma yok (Graphic.color kırpılır, Overlay canvas bloom almaz);
        // IMGUI'de de aynı sınır geçerli → "parlama" üst üste serilen açık katmanlarla taklit edilir.

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static void Glow(Rect r, Color c, float strength)
        {
            for (int i = 3; i >= 1; i--)
            {
                float grow = i * 3f;
                var g = new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f);
                Fill(g, new Color(c.r, c.g, c.b, 0.10f * strength / i));
            }
        }
    }
}
