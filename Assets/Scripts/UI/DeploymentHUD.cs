using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Yerleştirme fazı IMGUI paneli. İki bölüm:
    ///   1) ÖZ DEPOSU + "Üret" tarifleri — öz HARCAYARAK birim üretme (artık SADECE burada, overworld'de değil).
    ///   2) Üretilen kartları seç, mavi hex'e (bedava) yerleştir, "Savaşı Başlat".
    /// Sadece Deployment state'inde çizer. Geçici whitebox UI — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class DeploymentHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager  _state;
        [SerializeField] private DeploymentManager _deployment;
        [SerializeField] private PartyManager      _party;

        [Header("Üretim (öz harcayarak)")]
        [SerializeField] private EssenceWallet     _wallet;
        [SerializeField] private EssenceConfigSO   _config;
        [SerializeField] private List<UnitRecipe>  _recipes = new();

        private static readonly EssenceType[] Types =
            { EssenceType.Ates, EssenceType.Su, EssenceType.Toprak };

        private void OnGUI()
        {
            if (_state == null || _state.State != GameState.Deployment) return;

            const float w = 320f, h = 480f;
            var rect = new Rect(12f, 80f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("ÖZ DEPOSU");
            DrawWallet();

            GUILayout.Space(6);
            GUILayout.Label("BİRİM ÜRET (öz tarifi)");
            DrawRecipes();

            GUILayout.Space(8);
            GUILayout.Label("YERLEŞTİR — kart seç, mavi hex'e tıkla (bedava)");
            DrawCommanderLine();
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

        private void DrawWallet()
        {
            if (_wallet == null) { GUILayout.Label("(cüzdan yok)"); return; }

            Color prev = GUI.color;
            foreach (var t in Types)
            {
                if (_config != null) GUI.color = _config.ColorOf(t);
                string name = _config != null ? _config.NameOf(t) : t.ToString();
                GUILayout.Label($"● {name}: {_wallet.Get(t)}");
            }
            GUI.color = prev;
        }

        private void DrawRecipes()
        {
            if (_party == null) return;

            foreach (var r in _recipes)
            {
                if (r == null || r.UnitClass == null) continue;

                bool can = _wallet != null && _wallet.CanAfford(r.Cost);
                GUI.enabled = can;
                if (GUILayout.Button($"Üret: {r.DisplayName}  ({r.CostString(_config)})", GUILayout.Height(26)))
                    _party.TryCreate(r);
                GUI.enabled = true;
            }
        }

        // Komutan (Kam) elle yerleştirilmez — otomatik + ücretsiz iner.
        private void DrawCommanderLine()
        {
            if (_party == null) return;
            foreach (var card in _party.Party)
            {
                if (card == null || !card.IsCommander) continue;
                GUILayout.Label($"Komutan: {card.Data.ClassName} — otomatik iner (ucretsiz)");
            }
        }

        private void DrawCardList()
        {
            if (_party == null || _deployment == null) return;

            foreach (var card in _party.Party)
            {
                if (card.IsCommander) continue; // komutan otomatik iner, listede gösterilmez
                bool deployed = _deployment.IsCardDeployed(card);
                bool selected = _deployment.SelectedCard == card;

                string tag = deployed ? "  [yerlesti]"
                           : selected ? "  ◄ secili"
                           : "";
                string label = $"{card.Data.ClassName} Sv{card.Level}  HP{card.MaxHP}{tag}";

                GUI.enabled = !deployed;
                if (GUILayout.Button(label, GUILayout.Height(28)))
                    _deployment.SelectedCard = selected ? null : card;
                GUI.enabled = true;
            }
        }
    }
}
