using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Yerleştirme fazı IMGUI paneli: parti kartlarını listeler, kart seçtirir, özü gösterir,
    /// "Savaşı Başlat" ile combat'a geçirir. Sadece Deployment state'inde çizer.
    /// Geçici whitebox UI — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class DeploymentHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager  _state;
        [SerializeField] private DeploymentManager _deployment;
        [SerializeField] private PartyManager      _party;
        [SerializeField] private EssenceManager    _essence;

        private void OnGUI()
        {
            if (_state == null || _state.State != GameState.Deployment) return;

            const float w = 300f, h = 380f;
            var rect = new Rect(12f, 80f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("YERLESTIRME — kart sec, mavi hex'e tikla");
            GUILayout.Label($"Oz: {(_essence != null ? _essence.CurrentEssence : 0)}");
            GUILayout.Space(4);

            DrawCardList();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Yerlesen birim: {(_deployment != null ? _deployment.DeployedCount : 0)}");

            GUI.enabled = _deployment != null && _deployment.DeployedCount > 0;
            if (GUILayout.Button("SAVASI BASLAT", GUILayout.Height(34)))
                _state.StartBattle();
            GUI.enabled = true;

            if (GUILayout.Button("Geri Don (Overworld)", GUILayout.Height(26)))
                _state.ReturnToOverworld();

            GUILayout.EndArea();
        }

        private void DrawCardList()
        {
            if (_party == null || _deployment == null) return;

            foreach (var card in _party.Party)
            {
                bool deployed = _deployment.IsCardDeployed(card);
                bool selected = _deployment.SelectedCard == card;
                int  cost     = card.Data.DeployCost;
                bool canAfford = _essence != null && _essence.CanAfford(cost);

                string tag = deployed ? "  [yerlesti]"
                           : selected ? "  ◄ secili"
                           : !canAfford ? "  (oz yok)"
                           : "";
                string label = $"{card.Data.ClassName} Sv{card.Level}  HP{card.MaxHP}  oz{cost}{tag}";

                GUI.enabled = !deployed;
                if (GUILayout.Button(label, GUILayout.Height(28)))
                    _deployment.SelectedCard = selected ? null : card;
                GUI.enabled = true;
            }
        }
    }
}
