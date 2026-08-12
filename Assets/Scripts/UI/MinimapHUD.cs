using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// TAB basılı tutunca **8 bölümlük ilerleme şeridi** (hafif şeffaf): tamamlananlar yeşil,
    /// içinde bulunulan parlak, kilitliler sönük. Anlık <see cref="ChapterProgress"/>'ten okur.
    /// **1 bölüm = 1 harita** (Docs/GAME_DESIGN.md §0).
    ///
    /// (2026-07-28 / TASK-004: burada eskiden 3×3 snake ada minimap'i vardı — 9 adalı dünya
    /// alternatife düştü, bkz <c>Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/</c>. Eski hâli:
    /// <c>git show 3dafb5f:Assets/Scripts/UI/MinimapHUD.cs</c>.)
    /// </summary>
    public class MinimapHUD : MonoBehaviour
    {
        [SerializeField] private ChapterProgress _progress;
        [SerializeField] private KeyCode _key = KeyCode.Tab;

        private GUIStyle _num, _title;

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_progress == null || !Input.GetKey(_key)) return;

            _num   ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _title ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };

            // Sanal 1920x1080 ekrana ciz -> her cozunurlukte ayni oran.
            using var _scale = HudScale.Scaled();

            int current = _progress.CurrentChapter;
            int count   = Mathf.Max(1, _progress.ChapterCount);

            const float cell = 72f, gap = 8f, pad = 14f;
            float gw = count * cell + (count - 1) * gap;
            float x0 = (HudScale.Width  - gw) * 0.5f;
            float y0 = (HudScale.Height - cell) * 0.5f + 10f;

            // Hafif şeffaf arka plan
            var panel = new Rect(x0 - pad, y0 - pad - 26f, gw + 2f * pad, cell + 2f * pad + 26f);
            ImguiBlocker.Register(panel);   // şerit açıkken üstüne tıklamak karakteri yürütmesin
            DrawRect(panel, new Color(0f, 0f, 0f, 0.40f));

            // Başlık
            _title.normal.textColor = new Color(0.4f, 0.85f, 1f);
            string chapterName = _progress.Config != null ? _progress.Config.NameOf(current) : $"Bölüm {current}";
            GUI.Label(new Rect(x0 - pad, y0 - pad - 24f, gw + 2f * pad, 22f), $"BÖLÜMLER — {chapterName}", _title);

            for (int i = 0; i < count; i++)
            {
                int  chapter = i + 1;
                bool active  = chapter == current;
                bool done    = _progress.IsCompleted(chapter);
                Rect rect    = new Rect(x0 + i * (cell + gap), y0, cell, cell);

                // Hücre rengi: şu an = parlak cyan · tamamlandı = yeşil · kilitli = soluk gri
                DrawRect(rect, active ? new Color(0.15f, 0.80f, 1f, 0.92f)
                                      : done ? new Color(0.32f, 0.58f, 0.30f, 0.80f)
                                             : new Color(0.28f, 0.28f, 0.32f, 0.55f));

                // Sayı: aktif = büyük, parlak sarı, kalın; diğerleri = soluk
                _num.fontSize         = active ? 34 : 20;
                _num.fontStyle        = active ? FontStyle.Bold : FontStyle.Normal;
                _num.normal.textColor = active ? Color.yellow : new Color(1f, 1f, 1f, 0.65f);
                GUI.Label(rect, chapter.ToString(), _num);
            }
        }

        private static void DrawRect(Rect r, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
