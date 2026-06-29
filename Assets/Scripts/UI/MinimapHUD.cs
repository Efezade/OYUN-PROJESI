using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// TAB basılı tutunca 3×3 snake minimap (hafif şeffaf). 9 haritayı gösterir; aktif harita
    /// hem rengi hem sayısı parlar. Anlık `WorldGridManager.CurrentMap`'ten okur.
    /// </summary>
    public class MinimapHUD : MonoBehaviour
    {
        [SerializeField] private WorldGridManager _world;
        [SerializeField] private KeyCode _key = KeyCode.Tab;

        // Snake dizilim (9 8 7 / 6 5 4 / 3 2 1)
        private static readonly int[,] Layout = { { 9, 8, 7 }, { 6, 5, 4 }, { 3, 2, 1 } };

        private GUIStyle _num, _title;

        private void OnGUI()
        {
            if (_world == null || !Input.GetKey(_key)) return;

            _num   ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _title ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };

            int current = _world.CurrentMap;

            const float cell = 72f, gap = 8f, pad = 14f;
            float gw = 3f * cell + 2f * gap;
            float gh = 3f * cell + 2f * gap;
            float x0 = (Screen.width  - gw) * 0.5f;
            float y0 = (Screen.height - gh) * 0.5f + 10f;

            // Hafif şeffaf arka plan
            DrawRect(new Rect(x0 - pad, y0 - pad - 26f, gw + 2f * pad, gh + 2f * pad + 26f), new Color(0f, 0f, 0f, 0.40f));

            // Başlık
            _title.normal.textColor = new Color(0.4f, 0.85f, 1f);
            GUI.Label(new Rect(x0 - pad, y0 - pad - 24f, gw + 2f * pad, 22f), $"MİNİHARİTA — Harita {current}", _title);

            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int  map    = Layout[r, c];
                    bool active = map == current;
                    Rect rect   = new Rect(x0 + c * (cell + gap), y0 + r * (cell + gap), cell, cell);

                    // Hücre rengi: aktif = parlak cyan, diğerleri = soluk gri
                    DrawRect(rect, active ? new Color(0.15f, 0.80f, 1f, 0.92f) : new Color(0.28f, 0.28f, 0.32f, 0.55f));

                    // Sayı: aktif = büyük, parlak sarı, kalın; diğerleri = soluk
                    _num.fontSize         = active ? 34 : 20;
                    _num.fontStyle        = active ? FontStyle.Bold : FontStyle.Normal;
                    _num.normal.textColor = active ? Color.yellow : new Color(1f, 1f, 1f, 0.65f);
                    GUI.Label(rect, map.ToString(), _num);
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
