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
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_stateManager == null) return;

            // Sanal 1920x1080 ekrana çiz → panel her çözünürlükte aynı oranda görünür.
            using (HudScale.Scaled())
            {
                switch (_stateManager.State)
                {
                    case GameState.Overworld:      DrawNearbyMissionPrompt(); break;
                    case GameState.ConfirmMission:  DrawConfirm();             break;
                    case GameState.Combat:          DrawCombat();              break;
                }
            }
        }

        // Oyuncu bir görev karosunun _enterRange içindeyse "Savaşa Gir" istemi göster.
        private void DrawNearbyMissionPrompt()
        {
            if (_missionManager == null || _player == null) return;

            MissionData mission = _missionManager.GetEnterableMission(_player.CurrentCoord);
            if (mission == null) return;

            // Gün barının ALTINA — ikisi de üst-ortadaydı ve birebir çakışıyordu (bkz HudLayout).
            const float w = 360f, h = 76f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudLayout.SecondRowY, w, h);
            ImguiBlocker.Register(rect);
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
            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);
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

            // Üst-ORTA sıra barına (TurnOrderBarHUD) bırakıldı → bu panel sağ üstte.
            const float w = 320f;
            var rect = new Rect(HudScale.Width - w - HudLayout.RightMargin, HudLayout.RightSecondY, w, 74f);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"SAVAS — {missionName}");
            if (GUILayout.Button("Geri Don (Overworld)", GUILayout.Height(30)))
                _stateManager.ReturnToOverworld();
            GUILayout.EndArea();
        }
    }
}
