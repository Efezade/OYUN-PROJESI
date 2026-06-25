using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Geçici IMGUI akış paneli:
    ///   • Overworld: görev karosuna yeterince yaklaşınca "Savaşa Gir" istemi (proximity).
    ///   • ConfirmMission: Evet/Hayır onayı.
    ///   • Combat: "Geri Dön".
    /// State gated olduğu için tıklama çakışması yok. Cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class OverworldCombatHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager _stateManager;
        [Tooltip("Yakınlık istemi için — atanmazsa istem çizilmez (geri uyumlu).")]
        [SerializeField] private MissionManager   _missionManager;
        [SerializeField] private PlayerController _player;

        private void OnGUI()
        {
            if (_stateManager == null) return;

            switch (_stateManager.State)
            {
                case GameState.Overworld:      DrawNearbyMissionPrompt(); break;
                case GameState.ConfirmMission:  DrawConfirm();             break;
                case GameState.Combat:          DrawCombat();              break;
            }
        }

        // Oyuncu bir görev karosunun _enterRange içindeyse "Savaşa Gir" istemi göster.
        private void DrawNearbyMissionPrompt()
        {
            if (_missionManager == null || _player == null) return;

            MissionData mission = _missionManager.GetEnterableMission(_player.CurrentCoord);
            if (mission == null) return;

            const float w = 360f, h = 76f;
            var rect = new Rect((Screen.width - w) * 0.5f, 12f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"Gorev yakinda: '{mission.DisplayName}'");
            if (GUILayout.Button("Savasa Gir", GUILayout.Height(34)))
                _stateManager.RequestMission(mission);
            GUILayout.EndArea();
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
