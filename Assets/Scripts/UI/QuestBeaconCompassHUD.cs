using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ZORUNLU GÖREV PUSULASI (B1, 2026-09-03): ekranın DIŞINDA kalan görev fenerlerini ekran
    /// kenarında bir okla gösterir — okun yönü fenerin yönü, yanındaki sayı karo cinsinden mesafe.
    ///
    /// NEDEN VAR: <see cref="MandatoryQuestBeacon"/> sütunu ancak kameranın gördüğü yere düşerse
    /// işe yarar. Görev haritanın öbür ucuna düşerse dünyada hiçbir iz görünmez ve geri bildirim
    /// yine yalnız minimap ikonuna + üst bara kalırdı (kullanıcı raporu 2026-09-02: "görüş dışında
    /// düşerse hiç bilgi yok"). Pusula, sütunun ekran dışındaki uzantısıdır: ikisi birlikte
    /// oyuncunun UI'a BAKMADAN da haberdar olmasını sağlar.
    ///
    /// İKİ KADEME (fener ile aynı): YENİ görev = parlak ok + "ZORUNLU GÖREV" yazısı + nabız;
    /// eskiyen görev = soluk ok + yalnız mesafe. Beş görev birden açıkken kenar oklarla dolmasın.
    ///
    /// Neden IMGUI: diğer overworld HUD'ları da IMGUI (ChapterRunHUD / MandatoryQuestBarHUD) ve
    /// dönmüş bir ok uGUI'de ayrıca sprite + RectTransform işi olurdu. Ok, döndürülmüş GUI
    /// matrisinde satır satır daralan dikdörtgenlerle çizilir (IMGUI üçgen çizemez).
    ///
    /// VERİYİ ÜRETMEZ: fener listesini <see cref="MandatoryQuestBeacon"/>'dan okur.
    /// </summary>
    public class QuestBeaconCompassHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private MandatoryQuestBeacon _beacons;
        [SerializeField] private PlayerController     _player;
        [SerializeField] private GameStateManager     _state;
        [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
        [SerializeField] private Camera _camera;

        [Header("Ölçüler (1920x1080 sanal ekran)")]
        [Tooltip("Okun ekran kenarından içeri payı — köşelerdeki diğer HUD'ların altında kalmasın.")]
        [SerializeField] private float _margin      = 56f;
        [SerializeField] private float _arrowSize   = 26f;
        [Tooltip("Fener ekranda görünürken de ok çizilsin mi? Varsayılan HAYIR: sütun zaten orada.")]
        [SerializeField] private bool  _showOnScreen = false;

        [Header("Renk")]
        [SerializeField] private Color _gold      = new(1.00f, 0.85f, 0.20f);
        [Tooltip("Eskiyen görevin oku bu kadar soluk çizilir (0-1).")]
        [SerializeField] private float _calmAlpha = 0.45f;
        [SerializeField] private float _pulseSpeed = 4f;

        private GUIStyle _labelStyle;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;                       // tam-ekran menü açıkken çizme
            if (_beacons == null || _camera == null) return;
            if (_state != null && _state.State != GameState.Overworld) return;

            IReadOnlyList<MandatoryQuestBeacon.Beacon> list = _beacons.Beacons;
            if (list.Count == 0) return;

            using var _scale = HudScale.Scaled();

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };

            float w = HudScale.Width, h = HudScale.Height;
            var center = new Vector2(w * 0.5f, h * 0.5f);

            for (int i = 0; i < list.Count; i++)
            {
                MandatoryQuestBeacon.Beacon b = list[i];

                Vector3 sp = _camera.WorldToScreenPoint(b.Ground + Vector3.up * 1.5f);
                bool behind = sp.z <= 0f;
                if (behind)
                {
                    // Kameranın ARKASINDAKİ nokta ekrana ters düşer (projeksiyon işareti döner) →
                    // merkeze göre aynala, yoksa ok tam ters yönü gösterir.
                    sp.x = Screen.width  - sp.x;
                    sp.y = Screen.height - sp.y;
                }

                Vector2 g = HudScale.ToGui(sp);
                bool onScreen = !behind
                             && g.x > _margin && g.x < w - _margin
                             && g.y > _margin && g.y < h - _margin;
                if (onScreen && !_showOnScreen) continue;

                Vector2 dir = g - center;
                if (dir.sqrMagnitude < 0.01f) continue;

                // Merkezden çıkan ışının, kenardan _margin içeri çekilmiş dikdörtgeni kestiği nokta.
                float mx = (w * 0.5f - _margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
                float my = (h * 0.5f - _margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
                Vector2 edge = onScreen ? g : center + dir * Mathf.Min(mx, my);

                float alpha = b.Fresh
                            ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * _pulseSpeed)
                            : _calmAlpha;
                var color = new Color(_gold.r, _gold.g, _gold.b, alpha);

                // Ok yukarı bakacak şekilde çizilir; açı ekran uzayında (y AŞAĞI) hesaplanır.
                float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
                DrawArrow(edge, angle, _arrowSize * (b.Fresh ? 1f : 0.75f), color);

                DrawLabel(b, edge, dir, color);
            }
        }

        private void DrawLabel(MandatoryQuestBeacon.Beacon b, Vector2 edge, Vector2 dir, Color color)
        {
            string text = _player != null
                        ? $"{_player.CurrentCoord.DistanceTo(b.Coord)} karo"
                        : string.Empty;
            if (b.Fresh) text = string.IsNullOrEmpty(text) ? "ZORUNLU GÖREV" : $"ZORUNLU GÖREV · {text}";
            if (string.IsNullOrEmpty(text)) return;

            // Yazı okun EKRANIN İÇİNE bakan yanına konur, yoksa kenarın dışına taşardı.
            Vector2 inward = -dir.normalized * (_arrowSize + 14f);
            var rect = new Rect(edge.x + inward.x - 90f, edge.y + inward.y - 10f, 180f, 20f);

            _labelStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _labelStyle);
            _labelStyle.normal.textColor = color;
            GUI.Label(rect, text, _labelStyle);
        }

        /// <summary>
        /// Yukarı bakan üçgen ok. IMGUI üçgen çizemediği için satır satır daralan dikdörtgenlerle
        /// yaklaşılır; döndürme GUI matrisinden yapılır (HudScale ölçeğinin ÜSTÜNE biner, çizim
        /// sonunda matris geri konur).
        /// </summary>
        private static void DrawArrow(Vector2 center, float angleDeg, float size, Color color)
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angleDeg, center);

            const int rows = 9;
            float rowH = size / rows;
            for (int i = 0; i < rows; i++)
            {
                float t = i / (rows - 1f);                       // 0 = tepe, 1 = taban
                float halfW = size * 0.5f * t;
                float y = center.y - size * 0.5f + i * rowH;
                Fill(new Rect(center.x - halfW, y, halfW * 2f, rowH + 0.5f), color);
            }

            // Sap: okun geldiği yönü belli etsin (üçgen tek başına yön okunurluğu zayıf).
            Fill(new Rect(center.x - size * 0.14f, center.y + size * 0.5f, size * 0.28f, size * 0.35f),
                 new Color(color.r, color.g, color.b, color.a * 0.7f));

            GUI.matrix = saved;
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
