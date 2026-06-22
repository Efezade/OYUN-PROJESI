using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Overworld IMGUI paneli: ÖZ DEPOSU (3 tipli sayaç), bulunduğun karodaki özü "Topla (1 AP)",
    /// ve öz tarifiyle birim üretme (Savaşçı/Ranger) + roster gösterimi.
    /// Sadece Overworld state'inde çizer. Geçici whitebox UI — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class OverworldEssenceHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager  _state;
        [SerializeField] private EssenceWallet      _wallet;
        [SerializeField] private EssenceNodeManager _nodes;
        [SerializeField] private PlayerController    _player;
        [SerializeField] private PartyManager        _party;
        [SerializeField] private EssenceConfigSO     _config;

        [Header("Üretim Tarifleri")]
        [SerializeField] private List<UnitRecipe> _recipes = new();

        private static readonly EssenceType[] Types =
            { EssenceType.Ates, EssenceType.Su, EssenceType.Toprak };

        private void OnGUI()
        {
            if (_state == null || _state.State != GameState.Overworld) return;

            const float w = 300f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 12f, 12f, w, 380f), GUI.skin.box);

            GUILayout.Label("ÖZ DEPOSU");
            DrawWallet();

            GUILayout.Space(4f);
            DrawCollect();

            GUILayout.Space(8f);
            GUILayout.Label("BİRİM ÜRET (öz tarifi)");
            DrawRecipes();

            GUILayout.Space(8f);
            DrawRoster();

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

        private void DrawCollect()
        {
            if (_nodes == null || _player == null) return;

            HexCoordinate here = _player.CurrentCoord;
            if (_nodes.HasEssenceAt(here))
            {
                GUILayout.Label($"Bu karoda: {_nodes.Describe(here)}");
                GUI.enabled = _nodes.CanCollect(here);
                if (GUILayout.Button("Topla (1 AP)", GUILayout.Height(26)))
                    _nodes.CollectAt(here);
                GUI.enabled = true;
            }
            else GUILayout.Label("Bu karoda öz yok.");
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

        private void DrawRoster()
        {
            if (_party == null) return;

            var sb = new System.Text.StringBuilder("Roster: ");
            bool first = true;
            foreach (var c in _party.Party)
            {
                if (c == null) continue;
                if (!first) sb.Append(", ");
                sb.Append(c.Data.ClassName);
                if (c.IsCommander) sb.Append("(K)");
                first = false;
            }
            GUILayout.Label(sb.ToString());
        }
    }
}
