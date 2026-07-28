using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Overworld IMGUI paneli: ÖZ DEPOSU (3 tipli sayaç) + bulunduğun karodaki özü "Topla (1 AP)"
    /// + roster gösterimi (salt-okunur). Birim ÜRETME burada DEĞİL — savaş öncesi yerleştirme
    /// ekranında (DeploymentHUD) yapılır. Geçici whitebox UI — cila aşamasında uGUI'ye taşınacak.
    /// </summary>
    public class OverworldEssenceHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager  _state;
        [SerializeField] private EssenceWallet      _wallet;
        [Tooltip("ESKİ akış: elle boyanmış öz node'ları (Essence Painter). Bölüm dünyasında kullanılmaz.")]
        [SerializeField] private EssenceNodeManager _nodes;
        [Tooltip("YENİ akış (TASK-005): öz karonun KENDİSİDİR — atanmışsa bu öncelikli kullanılır.")]
        [SerializeField] private ChapterMapGenerator _terrain;
        [SerializeField] private PlayerController    _player;
        [SerializeField] private PartyManager        _party;
        [SerializeField] private EssenceConfigSO     _config;

        [Tooltip("ÖZ DEPOSU'nda gösterilecek öz türleri. Bölüm 1 = Taş + Doğa (GAME_DESIGN §3); " +
                 "eski Ateş/Su/Toprak akışı için o üçü yazılır. Kurulum aracı doldurur.")]
        [SerializeField] private EssenceType[] _shownTypes =
            { EssenceType.Ates, EssenceType.Su, EssenceType.Toprak };

        private EssenceType[] Types => (_shownTypes != null && _shownTypes.Length > 0)
            ? _shownTypes
            : new[] { EssenceType.Ates, EssenceType.Su, EssenceType.Toprak };

        private void OnGUI()
        {
            if (_state == null || _state.State != GameState.Overworld) return;

            // Tam-ekran menü (KİTAP/ÇANTA/HARİTA) açıkken bu paneli ÇİZME — IMGUI, uGUI menüsünün
            // üstüne taşardı; ayrıca KİTAP'ın kendi ÖZ DEPOSU'su zaten var (bkz. MenuState).
            if (MenuState.IsAnyOpen) return;

            // Sanal 1920x1080 ekrana ciz -> her cozunurlukte ayni oran.
            using var _scale = HudScale.Scaled();

            const float w = 300f;
            var rect = new Rect(HudScale.Width - w - 12f, 12f, w, 230f);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("ÖZ DEPOSU");
            DrawWallet();

            GUILayout.Space(4f);
            DrawCollect();

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
            if (_player == null) return;

            HexCoordinate here = _player.CurrentCoord;

            // YENİ akış önce: öz = karonun kendisi (terrain), toplanınca karo tükenir.
            if (_terrain != null)
            {
                if (_terrain.HasEssenceAt(here))
                {
                    GUILayout.Label($"Bu karoda: {_terrain.Describe(here)}");
                    GUI.enabled = _terrain.CanCollect(here);
                    if (GUILayout.Button("Topla (1 AP)", GUILayout.Height(26)))
                        _terrain.CollectAt(here);
                    GUI.enabled = true;
                }
                else GUILayout.Label("Bu karoda öz yok.");
                return;
            }

            if (_nodes == null) return;
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
            GUILayout.Label("(Birim üretme: savaş öncesi yerleştirme ekranında)");
        }
    }
}
