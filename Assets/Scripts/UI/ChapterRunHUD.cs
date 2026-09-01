using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Zaman baskısı geri bildirimi (TASK-007):
    ///   • Çöküş başlamadan önce kaç gün kaldığını ve son günü gösterir (oyuncu körlemesine kalmasın).
    ///   • Gün <see cref="ChapterNodeManager.LateCostActive"/> olunca "zindanlar pahalılaştı" uyarısı.
    ///   • BOSS savaşı bitince (kazanılsa da kaybedilse de) ORTADA "OYUN BİTTİ" ekranı.
    ///     Gerçek akışta zafer sonraki haritaya geçirecek; şimdilik iki sonuç da burada bitiyor
    ///     (kullanıcı kararı 2026-09-01). Bu ekran KAYIP banner'ının ÖNÜNE geçer — boss savaşında
    ///     Kam düşerse ikisi birden çıkmasın, oyun sonu daha güçlü bir olaydır.
    ///   • Bölüm kaybedilince ORTADA banner + "YENİDEN BAŞLA (yeni harita)" düğmesi.
    ///     Banner metni neyin korunduğunu/kaybolduğunu AÇIKÇA yazar — oyuncu haksızlığa uğramış
    ///     hissetmesin (görev metnindeki "sessiz kayıp yok" ilkesinin devamı).
    /// </summary>
    public class ChapterRunHUD : MonoBehaviour
    {
        [SerializeField] private ChapterRunManager   _run;
        [SerializeField] private ChapterNodeManager  _nodes;
        [SerializeField] private ActionPointManager  _ap;
        [SerializeField] private CollapseConfig      _collapseConfig;
        [SerializeField] private GameStateManager    _state;

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_run == null) return;
            using var _scale = HudScale.Scaled();

            // SIRA ÖNEMLİ: oyun sonu, kayıp banner'ından ÖNCE denetlenir.
            if (_nodes != null && _nodes.BossFightResolved) { DrawGameOverBanner(); return; }
            if (_run.ChapterLost) { DrawLostBanner(); return; }
            if (_state != null && _state.State != GameState.Overworld) return;

            DrawPressureStrip();
        }

        /// <summary>Üst orta: gün / son gün / çöküş uyarısı.</summary>
        private void DrawPressureStrip()
        {
            if (_ap == null) return;

            int day     = _ap.CurrentDay;
            int hardCut = _run.HardCutDay;
            int start   = _collapseConfig != null ? _collapseConfig.CollapseStartDay : hardCut;

            string msg;
            if (day < start)      msg = $"Gün {day}/{hardCut} · çöküş {start}. günde başlar";
            else if (day < hardCut) msg = $"Gün {day}/{hardCut} · ÇÖKÜŞ SÜRÜYOR — karolar siliniyor";
            else                  msg = $"Gün {day}/{hardCut} · SON GÜN!";

            if (_nodes != null && _nodes.LateCostActive) msg += " · zindanlar pahalı (×2)";

            const float w = 620f, h = HudLayout.RunBarHeight;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudLayout.RunBarY, w, h);
            ImguiBlocker.Register(rect);

            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            style.normal.textColor = day >= start ? new Color(1f, 0.55f, 0.4f) : Color.white;
            GUI.Box(rect, msg, style);
        }

        /// <summary>BOSS bitti → oyun sonu. Şimdilik tek çıkış "yeniden başla"; gerçek akışta
        /// zafer 2. bölüm haritasına geçirecek.</summary>
        private void DrawGameOverBanner()
        {
            bool won = _run.LastCombatWon;

            const float w = 620f, h = 250f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);

            GUILayout.BeginArea(rect, GUI.skin.box);

            var title = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
            title.normal.textColor = won ? new Color(1f, 0.87f, 0.35f) : new Color(1f, 0.45f, 0.4f);
            GUILayout.Label("OYUN BİTTİ", title, GUILayout.Height(46f));

            var sub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            GUILayout.Label(won ? "Ana boss yenildi — bölüm tamamlandı." : "Ana bossa yenildin.", sub);

            GUILayout.Space(10f);
            GUILayout.Label("Sonraki bölüm haritası henüz bağlı değil: şimdilik zafer de yenilgi de");
            GUILayout.Label("burada bitiyor (geçici). 8 bölümlük akış eklenince zafer 2. haritaya geçirecek.");

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("YENİDEN BAŞLA (farklı harita)", GUILayout.Height(34f)))
                _run.RestartChapter();
            GUILayout.EndArea();
        }

        private void DrawLostBanner()
        {
            const float w = 620f, h = 240f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);

            GUILayout.BeginArea(rect, GUI.skin.box);
            var title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1f, 0.4f, 0.35f);
            GUILayout.Label("BÖLÜM KAYBEDİLDİ", title, GUILayout.Height(40f));

            GUILayout.Label(_run.LossReason);
            GUILayout.Space(8f);
            GUILayout.Label("KORUNDU:  birimlerin ve seviyeleri (kalıcı roster)");
            GUILayout.Label("KAYBOLDU: harcanmamış ham öz (taş + doğa) + keşif ilerlemesi");
            GUILayout.Space(4f);
            GUILayout.Label("Tüm oyun sıfırlanmadı — sadece bu harita baştan başlıyor.");

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("YENİDEN BAŞLA (farklı harita)", GUILayout.Height(34f)))
                _run.RestartChapter();
            GUILayout.EndArea();
        }
    }
}
