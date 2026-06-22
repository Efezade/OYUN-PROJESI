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
        [Tooltip("Kam manası — büyü panelinde gösterilir.")]
        [SerializeField] private KamManaManager   _kamMana;

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
            if (_state == null || _turnManager == null) return;
            if (_state.State != GameState.Combat) return;

            DrawTurnPanel();

            if (_turnManager.Result != CombatResult.Ongoing)
                DrawResultBanner();
        }

        private void DrawTurnPanel()
        {
            Unit cur = _turnManager.CurrentUnit;
            bool commanderTurn = cur != null && cur.Team == UnitTeam.Player && cur.IsCommander;

            const float w = 320f;
            float h = commanderTurn ? 330f : 176f;
            // Sol-üst (OverworldCombatHUD üst-orta "Geri Don" ile çakışmaz).
            GUILayout.BeginArea(new Rect(12f, 12f, w, h), GUI.skin.box);

            if (cur != null)
            {
                string who = cur.Team == UnitTeam.Player ? "SENIN TURUN" : "DUSMAN TURU";
                GUILayout.Label($"{who}:  {cur.DisplayName}{(cur.IsCommander ? "  ★ Komutan" : "")}");
                GUILayout.Label($"HP {cur.CurrentHP}/{cur.MaxHP}    Hiz {cur.Speed}");
                GUILayout.Label($"Hareket {cur.MoveRange}   ·   Saldiri menzili {cur.AttackRange}");

                if (cur.Team == UnitTeam.Player)
                {
                    string m = _turnManager.CurrentHasMoved ? "hareket: bitti" : "hareket: HAZIR";
                    string a = _turnManager.CurrentHasActed ? "eylem: bitti"   : "eylem: HAZIR";
                    GUILayout.Label($"{m}   |   {a}");

                    if (commanderTurn) DrawCommanderAbilities();

                    GUI.enabled = _turnManager.IsPlayerTurn;
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

        // Kam'ın büyüleri: mana + 1/2/3 arm butonları (hedefe sol tıkla = uygula).
        private void DrawCommanderAbilities()
        {
            if (_kamMana != null)
                GUILayout.Label($"Mana {_kamMana.CurrentMana}/{_kamMana.MaxMana}");

            var abilities = _caster != null ? _caster.Abilities : null;
            if (abilities == null || abilities.Count == 0)
            {
                GUILayout.Label("(Yetenek yok — Faz C4 kurulumu?)");
                return;
            }

            bool acted = _turnManager.CurrentHasActed;
            for (int i = 0; i < abilities.Count; i++)
            {
                KamAbilityData ab    = abilities[i];
                bool           armed = _caster.ArmedAbility == ab;
                bool           canMana = _kamMana == null || _kamMana.CanCast(ab.ManaCost);

                GUI.enabled = !acted && canMana;
                string mark  = armed ? "► " : "";
                if (GUILayout.Button($"{mark}[{i + 1}] {ab.DisplayName}  ({ab.Effect} {ab.Power}, m{ab.ManaCost}, menzil {ab.Range})"))
                    _caster.ArmAbility(i);
                GUI.enabled = true;
            }

            if (_caster.HasArmedAbility)
                GUILayout.Label($"HAZIR: {_caster.ArmedAbility.DisplayName} → hedefe tikla (Esc iptal)");
        }

        private void DrawResultBanner()
        {
            bool won = _turnManager.Result == CombatResult.PlayerWon;
            const float w = 360f, h = 140f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), GUI.skin.box);
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
