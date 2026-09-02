using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÇUKUR İSTEMİ (kullanıcı isteği 2026-09-02, madde 10) — oyuncu çökmüş bir karonun kenarına
    /// gelince açılan yakınlık istemi: RİSKE GİR (bedava, rastgele) ya da ÖZ ÖDE (kesin).
    /// Kazanılan şey karo değil "karo geri getirme hakkı"dır; hak harita ekranında harcanır.
    ///
    /// Neden IMGUI: aynı desendeki <see cref="OverworldCombatHUD"/> ile birebir aynı akış
    /// (yakınlık → istem → tek tık). Cila aşamasında ikisi birlikte uGUI'ye taşınacak.
    /// Yerleşim <see cref="HudLayout.FourthRowY"/> — koordinat UYDURULMADI.
    /// </summary>
    public class TileRecoveryHUD : MonoBehaviour
    {
        [SerializeField] private TileRecoveryManager _recovery;
        [SerializeField] private GameStateManager    _state;
        [Tooltip("Öz bedelini yazıyla göstermek için (isim/renk). Boşsa enum adı yazılır.")]
        [SerializeField] private EssenceConfigSO     _essenceConfig;

        [Tooltip("Sonuç yazısı kaç saniye ekranda kalsın.")]
        [SerializeField, Min(0.5f)] private float _messageSeconds = 3.5f;

        private string _message;
        private float  _messageUntil;

        private void Awake()
        {
            if (_recovery == null) _recovery = FindFirstObjectByType<TileRecoveryManager>();
            if (_state    == null) _state    = FindFirstObjectByType<GameStateManager>();
        }

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;                       // tam ekran menü açık
            if (_recovery == null) return;
            if (_state != null && _state.State != GameState.Overworld) return;

            using (HudScale.Scaled())
            {
                DrawMessage();

                if (!HasTarget(out HexCoordinate target)) return;
                DrawPrompt(target);
            }
        }

        /// <summary>Hedef aramasını ÖNBELLEKLER. OnGUI kare başına birkaç kez çalışır ve arama
        /// bütün çukurları geziyor — her çağrıda yeniden taramak CLAUDE.md §6'nın yasakladığı
        /// türden bir "her karede pahalı iş" olurdu. Çukurlar ancak gün sınırında değişiyor,
        /// oyuncu da adım adım yürüyor: dörtte bir saniyelik tazeleme fazlasıyla yeterli.</summary>
        private bool HasTarget(out HexCoordinate target)
        {
            if (Time.unscaledTime >= _nextTargetScan)
            {
                _nextTargetScan = Time.unscaledTime + 0.25f;
                _hasTarget      = _recovery.TryGetAttemptTarget(out _target);
            }
            target = _target;
            return _hasTarget;
        }

        private float         _nextTargetScan;
        private bool          _hasTarget;
        private HexCoordinate _target;

        private void DrawPrompt(HexCoordinate target)
        {
            const float w = 460f, h = 116f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudLayout.FourthRowY, w, h);
            ImguiBlocker.Register(rect);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"COKMUS KARO YANINDA  {target}   ·   elde hak: {_recovery.Credits}");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("RISKE GIR (bedava)", GUILayout.Height(34)))
            {
                _recovery.AttemptRisk(target, out string msg);
                ShowMessage(msg);
            }

            bool canPay = _recovery.CanAffordPaid();
            GUI.enabled = canPay;
            if (GUILayout.Button($"OZ ODE ({CostText()})", GUILayout.Height(34)))
            {
                _recovery.AttemptPaid(target, out string msg);
                ShowMessage(msg);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.Label(canPay
                ? "Hak kazandiktan sonra HARITA ekranindan istedigin cukura harcanir."
                : "Oz yetmiyor — riskli deneme her zaman bedava.");
            GUILayout.EndArea();
        }

        private string CostText()
        {
            var cost = _recovery.PaidCost;
            if (cost == null || cost.Count == 0) return "bedelsiz";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                string name = _essenceConfig != null ? _essenceConfig.NameOf(cost[i].type)
                                                     : cost[i].type.ToString();
                sb.Append(cost[i].amount).Append(' ').Append(name);
            }
            return sb.ToString();
        }

        private void ShowMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _message      = message;
            _messageUntil = Time.time + _messageSeconds;
            _nextTargetScan = 0f;   // deneme sonrası çukur "denenmiş" oldu → istem hemen kapansın
        }

        private void DrawMessage()
        {
            if (string.IsNullOrEmpty(_message) || Time.time > _messageUntil) return;

            const float w = 460f, h = 30f;
            // İstemin hemen ÜSTÜ: sonuç ile düğmeler aynı yerde okunsun.
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudLayout.FourthRowY - h - 4f, w, h);
            GUI.Label(rect, _message, HudMessageStyle);
        }

        private GUIStyle _msgStyle;
        private GUIStyle HudMessageStyle
        {
            get
            {
                if (_msgStyle == null)
                    _msgStyle = new GUIStyle(GUI.skin.box)
                    { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                return _msgStyle;
            }
        }
    }
}
