using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KISA OLAY BİLDİRİMLERİ (2026-10-10) — bölge kurallarının oyuncuya "ne oldu" dediği satırlar
    /// ("Gözcü Kuzgun seni gördü!", "Alaz seni yanılttı", "Kara Öz: düşmanlar +2 seviye").
    /// Ekranın üst ortasında, bölge giriş yazısının altında en çok <see cref="_maxLines"/> satır;
    /// her biri kendi süresi dolunca söner. Savaş ekranında da görünür (Kara Öz uyarısı orada).
    /// Tıklamayı YUTMAZ (bilgi şeridi).
    /// </summary>
    public class NoticeFeed : MonoBehaviour
    {
        [SerializeField] private float _top      = 196f;
        [SerializeField] private float _width    = 720f;
        [SerializeField] private int   _fontSize = 19;
        [SerializeField] private int   _maxLines = 3;
        [SerializeField] private float _fade     = 0.5f;

        private struct Line { public string text; public Color color; public float start, until; }
        private readonly List<Line> _lines = new();
        private GUIStyle _style;

        /// <summary>Bildirim yayınla. Aynı metin zaten ekrandaysa süresi uzar (yığılmasın).</summary>
        public void Post(string text, Color color, float seconds = 3.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _lines.Count; i++)
                if (_lines[i].text == text)
                {
                    Line l = _lines[i]; l.until = now + seconds; _lines[i] = l;
                    return;
                }
            _lines.Add(new Line { text = text, color = color, start = now, until = now + seconds });
            while (_lines.Count > _maxLines) _lines.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (MenuState.HudsHidden || _lines.Count == 0) return;
            float now = Time.unscaledTime;
            _lines.RemoveAll(l => now > l.until);
            if (_lines.Count == 0) return;

            _style ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = _fontSize, wordWrap = true };

            using var _ = HudScale.Scaled();
            float x = (HudScale.Width - _width) * 0.5f;
            for (int i = 0; i < _lines.Count; i++)
            {
                Line l = _lines[i];
                float a = Mathf.Min(1f, (now - l.start) / _fade, (l.until - now) / _fade);
                var r = new Rect(x, _top + i * 30f, _width, 30f);
                _style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * a);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), l.text, _style);
                _style.normal.textColor = new Color(l.color.r, l.color.g, l.color.b, a);
                GUI.Label(r, l.text, _style);
            }
        }
    }
}
