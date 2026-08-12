using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// AKSİYON SIRASI BARI (For The King / XCOM tarzı) — ekranın üstünde, savaş sırasında.
    /// Soldan sağa initiative kuyruğunu gösterir: EN SOLDAKİ sırası gelen birimdir (büyük ve
    /// çerçeveli), sağa doğru kimin ne zaman oynayacağı okunur. Ölen birim kuyruktan düşer.
    ///
    /// Her birimin GÖRSELİ: sınıfın <see cref="CharacterClassData.Portrait"/> sprite'ı varsa o
    /// çizilir; yoksa sınıf RENGİ + adın baş harfleri. Yani portre/splash art eklenmeden de her
    /// karakter ayırt edilir — sanat gelince tek yapılacak sınıf asset'ine sprite atamak.
    ///
    /// Geçici whitebox IMGUI (projenin diğer HUD'ları gibi) — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class TurnOrderBarHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private TurnManager      _turnManager;
        [Tooltip("Opsiyonel — atanmışsa bar yalnız Combat state'inde çizilir.")]
        [SerializeField] private GameStateManager _stateManager;

        [Header("Görünüm")]
        [Tooltip("Barda en fazla kaç birim gösterilsin.")]
        [SerializeField, Min(2)] private int   _slotCount   = 8;
        [Tooltip("Sırası gelen (ilk) kartın kenar uzunluğu — diğerleri bundan küçük çizilir.")]
        [SerializeField, Min(32f)] private float _activeSize = 84f;
        [SerializeField, Min(24f)] private float _otherSize  = 58f;
        [SerializeField] private float _spacing   = 8f;
        [SerializeField] private float _topMargin = 8f;
        [SerializeField] private Color _panelColor    = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _activeOutline = new(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _enemyTint     = new(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color _playerTint    = new(0.3f, 0.7f, 1f, 1f);

        private readonly List<Unit> _upcoming = new();
        private GUIStyle _initialStyle, _nameStyle;

        private void OnEnable()
        {
            if (_turnManager != null) _turnManager.OnTurnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (_turnManager != null) _turnManager.OnTurnChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_turnManager != null) _turnManager.FillUpcoming(_upcoming, _slotCount);
        }

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_turnManager == null || !_turnManager.CombatActive) return;
            if (_stateManager != null && _stateManager.State != GameState.Combat) return;

            // Ölüm anında OnTurnChanged yayılmayabilir → listede ölü/yok olmuş birim varsa tazele.
            if (NeedsRefresh()) Refresh();
            if (_upcoming.Count == 0) return;

            EnsureStyles();

            // Sanal 1920x1080 ekrana ciz -> her cozunurlukte ayni oran.
            using var _scale = HudScale.Scaled();

            float width  = _activeSize + (_upcoming.Count - 1) * (_otherSize + _spacing) + _spacing * 3f;
            float height = _activeSize + 26f;
            var panel = new Rect((HudScale.Width - width) * 0.5f, _topMargin, width, height);

            ImguiBlocker.Register(panel);   // bar üstündeki tık hex tıklaması sayılmasın
            DrawRect(panel, _panelColor);

            float x = panel.x + _spacing * 1.5f;
            for (int i = 0; i < _upcoming.Count; i++)
            {
                bool  active = i == 0;
                float size   = active ? _activeSize : _otherSize;
                // Kartlar ALT kenardan hizalanır → sırası gelen büyük kart yukarı taşar (göze çarpar).
                var card = new Rect(x, panel.y + 8f + (_activeSize - size), size, size);

                DrawUnitCard(_upcoming[i], card, active);
                x += size + _spacing;
            }
        }

        private bool NeedsRefresh()
        {
            if (_upcoming.Count == 0) return true;
            foreach (Unit u in _upcoming)
                if (u == null || !u.IsAlive) return true;
            return false;
        }

        // Tek birim kartı: portre (yoksa renk + baş harfler) + takım şeridi + HP çubuğu.
        private void DrawUnitCard(Unit unit, Rect card, bool active)
        {
            if (unit == null) return;

            CharacterClassData data = unit.Card != null ? unit.Card.Data : null;
            Color teamTint = unit.Team == UnitTeam.Player ? _playerTint : _enemyTint;
            Color baseCol  = data != null ? data.UnitColor : teamTint;

            // Zemin: portre varsa onu bas, yoksa sınıf rengi.
            Sprite portrait = data != null ? data.Portrait : null;
            if (portrait != null && portrait.texture != null)
                GUI.DrawTexture(card, portrait.texture, ScaleMode.ScaleAndCrop);
            else
            {
                DrawRect(card, baseCol);
                GUI.Label(card, Initials(unit.DisplayName), _initialStyle);
            }

            // Takım şeridi (üst kenar) — dost/düşman bir bakışta.
            DrawRect(new Rect(card.x, card.y, card.width, 4f), teamTint);

            // Sırası gelen kart: parlak çerçeve.
            if (active) DrawOutline(card, _activeOutline, 3f);

            // HP çubuğu (alt kenar).
            float hp = unit.MaxHP > 0 ? Mathf.Clamp01((float)unit.CurrentHP / unit.MaxHP) : 0f;
            var hpBg = new Rect(card.x, card.yMax - 7f, card.width, 7f);
            DrawRect(hpBg, new Color(0f, 0f, 0f, 0.65f));
            DrawRect(new Rect(hpBg.x, hpBg.y, hpBg.width * hp, hpBg.height),
                     Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.35f, 0.9f, 0.35f), hp));

            // İsim (kartın altında) — yalnız sırası gelende, kalabalık olmasın.
            if (active)
                GUI.Label(new Rect(card.x - 30f, card.yMax + 2f, card.width + 60f, 20f),
                          unit.DisplayName, _nameStyle);
        }

        /// <summary>
        /// Portre yokken kartın üstüne yazılan kısa etiket. Sınıf adından İKİ HARF + varsa örnek
        /// numarası: "Goblin 1"→"Go1", "GoblinSaman 1"→"GS1", "Yamyam 1"→"Ya1", "Kam"→"Ka".
        /// CamelCase sınıf adlarında baş harfler alınır — yoksa "Goblin" ile "GoblinSaman" aynı
        /// kısaltmaya düşer ve barda ayırt edilemezlerdi.
        /// </summary>
        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            string[] parts  = name.Split(' ');
            string   word   = parts[0];
            string   suffix = parts.Length >= 2 ? parts[parts.Length - 1] : string.Empty;

            // CamelCase baş harfleri (GoblinSaman → "GS").
            var caps = new System.Text.StringBuilder();
            foreach (char c in word)
                if (char.IsUpper(c) && caps.Length < 2) caps.Append(c);

            string tag = caps.Length >= 2
                ? caps.ToString()
                : (word.Length >= 2 ? char.ToUpperInvariant(word[0]) + word.Substring(1, 1).ToLowerInvariant()
                                    : word.ToUpperInvariant());
            return tag + suffix;
        }

        private void EnsureStyles()
        {
            _initialStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white }
            };
            _nameStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white }
            };
        }

        private static void DrawRect(Rect r, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawOutline(Rect r, Color col, float t)
        {
            DrawRect(new Rect(r.x, r.y, r.width, t), col);
            DrawRect(new Rect(r.x, r.yMax - t, r.width, t), col);
            DrawRect(new Rect(r.x, r.y, t, r.height), col);
            DrawRect(new Rect(r.xMax - t, r.y, t, r.height), col);
        }
    }
}
