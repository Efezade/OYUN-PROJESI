using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Bölüm düğümleri için whitebox IMGUI paneli (TASK-006):
    ///   • Üstte zorunlu görev sayacı (3'ü bitmeden bölüm bitmez).
    ///   • Üzerinde durulan düğümün bilgisi + "Gir" düğmesi.
    ///     → Zindan/encounter'da **ZORLUK GÖRÜNÜR, ÖDÜL GİZLİ** ("riski bil, ödülü bilme").
    ///   • **BOSS her zaman burada** — konumdan bağımsız, haritanın her yerinden girilir.
    ///
    /// Cila aşamasında uGUI parşömen kitine taşınacak (diğer HUD'lar gibi).
    /// </summary>
    public class ChapterNodeHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager   _state;
        [SerializeField] private ChapterNodeManager _nodes;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private ActionPointManager _ap;

        private void OnGUI()
        {
            if (_state == null || _state.State != GameState.Overworld) return;
            if (_nodes == null || _player == null) return;

            using var _scale = HudScale.Scaled();

            const float w = 330f, h = 260f;
            var rect = new Rect(16f, HudScale.Height - h - 16f, w, h);
            ImguiBlocker.Register(rect);   // panel üstündeki tık haritaya düşmesin

            GUILayout.BeginArea(rect, GUI.skin.box);

            _nodes.MandatoryProgress(out int done, out int total);
            GUILayout.Label($"ZORUNLU GÖREVLER: {done}/{total}");
            GUILayout.Label(_nodes.IsMarketOpen() ? "Market: AÇIK (gündüz)" : "Market: KAPALI (gece)");
            GUILayout.Space(6f);

            DrawNodeHere();

            GUILayout.FlexibleSpace();
            DrawBoss();

            GUILayout.EndArea();
        }

        private void DrawNodeHere()
        {
            ChapterNodeManager.MapNode n = _nodes.NodeAt(_player.CurrentCoord);
            if (n == null) { GUILayout.Label("Bu karoda düğüm yok."); return; }

            if (n.Completed) { GUILayout.Label($"{TitleOf(n.Type)} — TAMAMLANDI ({n.Value} öz)"); return; }

            GUILayout.Label($"{TitleOf(n.Type)}");

            string diff = _nodes.DifficultyLabel(n);
            if (!string.IsNullOrEmpty(diff)) GUILayout.Label($"Zorluk: {diff}");     // GÖRÜNÜR
            GUILayout.Label($"Ödül:   {_nodes.RewardLabel(n)}");                     // GİZLİ olabilir
            if (n.APCost > 0) GUILayout.Label($"Maliyet: {n.APCost} AP");

            GUI.enabled = _nodes.CanEnter(n);
            if (GUILayout.Button(EnterLabel(n.Type), GUILayout.Height(28))) _nodes.Enter(n);
            GUI.enabled = true;

            if (n.Type == MapNodeType.Market && !_nodes.IsMarketOpen())
                GUILayout.Label("Gündüz dilimlerinde tekrar gel.");
        }

        private void DrawBoss()
        {
            ChapterNodeManager.MapNode boss = _nodes.Boss;
            if (boss == null) return;

            GUILayout.Label("— ANA BOSS (her yerden) —");
            if (boss.Completed) { GUILayout.Label("Boss yenildi."); return; }

            GUILayout.Label($"Zorluk: {_nodes.DifficultyLabel(boss)} · {boss.APCost} AP");
            GUI.enabled = _nodes.CanEnter(boss);
            if (GUILayout.Button("BOSS'A GİR", GUILayout.Height(30))) _nodes.Enter(boss);
            GUI.enabled = true;
        }

        private static string TitleOf(MapNodeType t) => t switch
        {
            MapNodeType.Mandatory  => "ZORUNLU GÖREV (harita kurtarma)",
            MapNodeType.Zindan     => "ZİNDAN",
            MapNodeType.Encounter  => "KARŞILAŞMA",
            MapNodeType.Market     => "GÜNDÜZ MARKETİ",
            MapNodeType.Watchtower => "GÖZETLEME KULESİ",
            MapNodeType.Boss       => "ANA BOSS",
            _                      => "DÜĞÜM"
        };

        private static string EnterLabel(MapNodeType t) => t switch
        {
            MapNodeType.Watchtower => "KULEYE ÇIK (sisi kalıcı aç)",
            MapNodeType.Market     => "MARKETE GİR",
            _                      => "GİR"
        };
    }
}
