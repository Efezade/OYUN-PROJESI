using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Faz A geçici IMGUI akış paneli: görev onayı (Evet/Hayır) ve savaşta "Geri Dön".
    /// State gated olduğu için (sadece ConfirmMission/Combat'ta çizer) tıklama çakışması yok.
    /// Gerçek UI Faz B/C ve cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class OverworldCombatHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager _stateManager;

        private void OnGUI()
        {
            if (_stateManager == null) return;

            switch (_stateManager.State)
            {
                case GameState.ConfirmMission: DrawConfirm(); break;
                case GameState.Combat:         DrawCombat();  break;
            }
        }

        private void DrawConfirm()
        {
            string missionName = _stateManager.PendingMission != null
                ? _stateManager.PendingMission.DisplayName : "Görev";

            const float w = 380f, h = 130f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"'{missionName}' gorevine girmek istiyor musun?");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Evet, Savasa Gir", GUILayout.Height(36))) _stateManager.ConfirmMission();
            if (GUILayout.Button("Hayir",            GUILayout.Height(36))) _stateManager.CancelMission();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCombat()
        {
            string missionName = _stateManager.ActiveMission != null
                ? _stateManager.ActiveMission.DisplayName : "Savas";

            const float w = 320f;
            var rect = new Rect((Screen.width - w) * 0.5f, 12f, w, 74f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"SAVAS — {missionName}");
            if (GUILayout.Button("Geri Don (Overworld)", GUILayout.Height(30)))
                _stateManager.ReturnToOverworld();
            GUILayout.EndArea();
        }
    }
}
