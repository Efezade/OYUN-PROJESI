using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Savaş IMGUI paneli: sıradaki birim, aktif birim bilgisi, "Turu Bitir" butonu ve
    /// zafer/yenilgi banner'ı + "Overworld'e Dön". Sadece Combat state'inde çizer.
    /// Geçici whitebox UI — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager _state;
        [SerializeField] private TurnManager      _turnManager;
        [Tooltip("Kam'ın büyü kasteri — Komutan turunda yetenek paneli için.")]
        [SerializeField] private AbilityCaster    _caster;
        [Tooltip("SINIF YETENEKLERİ (2026-10-03) — sırası gelen birimin 3 yeteneği burada düğme olur.")]
        [SerializeField] private UnitAbilityCaster _unitAbilities;
        [Tooltip("Kam'ın aktif mekaniği (bugün mana) — büyü panelinde gösterilir.")]
        [UnityEngine.Serialization.FormerlySerializedAs("_kamMana")]
        [SerializeField] private KamMechanicHost  _kam;

        private string _lastMessage = "";

        private void OnEnable()
        {
            if (_turnManager != null) _turnManager.OnMessage += HandleMessage;
        }

        private void OnDisable()
        {
            if (_turnManager != null) _turnManager.OnMessage -= HandleMessage;
        }

        private void HandleMessage(string msg) => _lastMessage = msg;

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_state == null || _turnManager == null) return;
            if (_state.State != GameState.Combat) return;

            // Sanal 1920x1080 ekrana çiz → savaş panelleri her çözünürlükte aynı oranda.
            using (HudScale.Scaled())
            {
                DrawTurnPanel();

                if (_turnManager.Result != CombatResult.Ongoing)
                    DrawResultBanner();
            }
        }

        private void DrawTurnPanel()
        {
            Unit cur = _turnManager.CurrentUnit;

            // Davul karosu satırı yalnız bir etki VARKEN çizilir → panel boş yere büyümez,
            // ama etki varken oyuncu "neden bu tur daha sert vuruyorum"u panelde görür.
            string tileLine = cur != null ? TileEffectLine(cur) : null;

            const float w = 320f;
            // Eski 1/2/3 büyü düğmeleri kaldırıldı (2026-10-03): büyüler artık YALNIZ davuldan
            // gelir ve manası oradan düşer. Panel her turda Kam'ın manasını gösterir.
            float h = 202f + (string.IsNullOrEmpty(tileLine) ? 0f : 26f);

            // Sınıf yetenekleri + evrim satırı panelin boyunu büyütür (yalnız varken).
            int abilityCount = _unitAbilities != null && cur != null && cur.Team == UnitTeam.Player && !cur.IsCommander
                             ? _unitAbilities.AbilitiesOf(cur).Count : 0;
            string evoLine = cur != null ? cur.Evolution.Describe() : "";
            h += abilityCount > 0 ? abilityCount * 32f + 26f : 0f;
            h += string.IsNullOrEmpty(evoLine) ? 0f : 26f;
            // Sol-üst (OverworldCombatHUD üst-orta "Geri Don" ile çakışmaz).
            var rect = new Rect(12f, 12f, w, h);
            ImguiBlocker.Register(rect);   // panel üstündeki tık hareket/saldırı sayılmasın
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (cur != null)
            {
                string who = cur.Team == UnitTeam.Player ? "SENIN TURUN" : "DUSMAN TURU";
                GUILayout.Label($"{who}:  {cur.DisplayName}{(cur.IsCommander ? "  ★ Komutan" : "")}");
                GUILayout.Label($"HP {cur.CurrentHP}/{cur.MaxHP}    Hiz {cur.Speed}");
                GUILayout.Label($"Hareket {cur.MoveRange}   ·   Saldiri menzili {cur.AttackRange}");

                if (!string.IsNullOrEmpty(tileLine)) GUILayout.Label(tileLine);
                if (!string.IsNullOrEmpty(evoLine))  GUILayout.Label($"EVRİM: {evoLine}");

                // KAM'IN MANASI — her turda görünür: davul çaldığında hangi kartı alabileceğini
                // oyuncu önceden hesaplayabilsin.
                if (_kam != null)
                    GUILayout.Label($"KAM {_kam.ResourceName.ToUpperInvariant()}: {_kam.Current}/{_kam.Max}");

                if (cur.Team == UnitTeam.Player)
                {
                    string m = _turnManager.CurrentHasMoved ? "hareket: bitti" : "hareket: HAZIR";
                    string a = _turnManager.CurrentHasActed ? "eylem: bitti"   : "eylem: HAZIR";
                    GUILayout.Label($"{m}   |   {a}");


                    if (abilityCount > 0) DrawClassAbilities(cur);

                    GUI.enabled = _turnManager.IsPlayerTurn && (_unitAbilities == null || !_unitAbilities.Busy);
                    if (GUILayout.Button("Turu Bitir", GUILayout.Height(28)))
                        _turnManager.EndPlayerTurn();
                    GUI.enabled = true;
                }
            }
            else GUILayout.Label("Savas hazirlaniyor...");

            if (!string.IsNullOrEmpty(_lastMessage))
                GUILayout.Label($"» {_lastMessage}");

            GUILayout.EndArea();
        }

        /// <summary>Sırası gelen birimin üç sınıf yeteneği: [1] ad (bekleme). Seçili olan ► ile
        /// işaretlenir; kullanılamayan sönük durur ve sebebi düğmede yazar.</summary>
        private void DrawClassAbilities(Unit cur)
        {
            GUILayout.Label("YETENEKLER — 1/2/3 seç, hedefe tıkla:");
            var list = _unitAbilities.AbilitiesOf(cur);
            for (int i = 0; i < list.Count; i++)
            {
                var ab = list[i];
                if (ab == null) continue;
                bool can   = _unitAbilities.CanUse(cur, i, out string why);
                bool armed = _unitAbilities.ArmedIndex == i;
                int  cd    = _unitAbilities.CooldownLeft(cur, i);

                string state = cd > 0 ? $"  ({cd} tur)" : "";
                GUI.enabled = can && !_unitAbilities.Busy;
                if (GUILayout.Button($"{(armed ? "► " : "")}[{i + 1}] {ab.Name}{state}", GUILayout.Height(28)))
                    _unitAbilities.Arm(i);
                GUI.enabled = true;
            }
        }

        /// <summary>Birimin ÜSTÜNDEKİ davul karosundan gelen fark — "karo çalışıyor mu" sorusunun
        /// sayısal cevabı. Halka/yazı geçicidir; bu satır tur boyunca durur.</summary>
        private string TileEffectLine(Unit u)
        {
            var b = u.Bonus;
            string s = "";
            if (b.Attack     != 0) s += $"{Signed(b.Attack)} hasar  ";
            if (b.Defense    != 0) s += $"{Signed(b.Defense)} sav  ";
            if (b.Move       != 0) s += $"{Signed(b.Move)} hareket  ";
            if (b.Initiative != 0) s += $"{Signed(b.Initiative)} hiz  ";
            if (b.Range      != 0) s += $"{Signed(b.Range)} menzil  ";

            if (u.IsStunned)                s += "SERSEM  ";
            if (_turnManager.ExtraActions > 0 && u == _turnManager.CurrentUnit)
                s += $"+{_turnManager.ExtraActions} aksiyon";

            return s.Length == 0 ? null : "KARO: " + s.Trim();
        }

        private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();

        private void DrawResultBanner()
        {
            bool won = _turnManager.Result == CombatResult.PlayerWon;
            const float w = 360f, h = 140f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.FlexibleSpace();
            GUILayout.Label(won ? "★   Z A F E R   ★" : "Y E N I L G I");
            GUILayout.Label(won ? "Tum dusmanlar yok edildi." : "Komutan (Kam) dustu — sefer sona erdi.");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Overworld'e Don", GUILayout.Height(34)))
                _state.ReturnToOverworld();
            GUILayout.EndArea();
        }
    }
}
