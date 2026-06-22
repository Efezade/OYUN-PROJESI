using UnityEngine;
using TacticalRPG.Core;

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
            const float w = 300f, h = 176f;
            // Sol-üst (OverworldCombatHUD üst-orta "Geri Don" ile çakışmaz).
            GUILayout.BeginArea(new Rect(12f, 12f, w, h), GUI.skin.box);

            Unit cur = _turnManager.CurrentUnit;
            if (cur != null)
            {
                string who = cur.Team == UnitTeam.Player ? "SENIN TURUN" : "DUSMAN TURU";
                GUILayout.Label($"{who}:  {cur.DisplayName}");
                GUILayout.Label($"HP {cur.CurrentHP}/{cur.MaxHP}    Hiz {cur.Speed}");
                GUILayout.Label($"Hareket {cur.MoveRange}   ·   Saldiri menzili {cur.AttackRange}");

                if (cur.Team == UnitTeam.Player)
                {
                    string m = _turnManager.CurrentHasMoved ? "hareket: bitti" : "hareket: HAZIR";
                    string a = _turnManager.CurrentHasActed ? "saldiri: bitti" : "saldiri: HAZIR";
                    GUILayout.Label($"{m}   |   {a}");

                    GUI.enabled = _turnManager.IsPlayerTurn;
                    if (GUILayout.Button("Turu Bitir", GUILayout.Height(30)))
                        _turnManager.EndPlayerTurn();
                    GUI.enabled = true;
                }
            }
            else GUILayout.Label("Savas hazirlaniyor...");

            if (!string.IsNullOrEmpty(_lastMessage))
                GUILayout.Label($"» {_lastMessage}");

            GUILayout.EndArea();
        }

        private void DrawResultBanner()
        {
            bool won = _turnManager.Result == CombatResult.PlayerWon;
            const float w = 360f, h = 140f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), GUI.skin.box);
            GUILayout.FlexibleSpace();
            GUILayout.Label(won ? "★   Z A F E R   ★" : "Y E N I L G I");
            GUILayout.Label(won ? "Tum dusmanlar yok edildi." : "Tum birimlerin dustu.");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Overworld'e Don", GUILayout.Height(34)))
                _state.ReturnToOverworld();
            GUILayout.EndArea();
        }
    }
}
