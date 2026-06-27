using System.Text;
using UnityEngine;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Oyun içi debug HUD: Gün/Zaman dilimi, AP çubuğu, Öz sayacı, Kam mana, Kıyamet uyarısı.
    /// Tüm referanslar Inspector'dan bağlanır — SceneSetupTool otomatik kurar.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [Tooltip("Durum makinesi — HUD yalnızca Overworld/ConfirmMission'da görünür (savaş/yerleştirme ekranlarında gizlenir).")]
        [SerializeField] private GameStateManager    _state;
        [SerializeField] private ActionPointManager  _apManager;
        [SerializeField] private MapCollapseManager  _collapseManager;
        [SerializeField] private EssenceWallet       _wallet;
        [SerializeField] private KamManaManager      _kamMana;

        [Header("UI Etiketleri")]
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [SerializeField] private TextMeshProUGUI _apLabel;
        [SerializeField] private TextMeshProUGUI _essenceLabel;
        [SerializeField] private TextMeshProUGUI _kamManaLabel;
        [SerializeField] private TextMeshProUGUI _collapseLabel;

        private readonly StringBuilder _sb = new StringBuilder(48);

        // Bu HUD bir overworld panelidir; Canvas'ı kapatarak savaş/yerleştirme
        // ekranlarında gizlenir (CombatHUD/DeploymentHUD ile sol-üstte çakışmasın).
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnAPChanged    += HandleAPChanged;
                _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            }
            if (_collapseManager != null)
                _collapseManager.OnTileCollapsed += HandleTileCollapsed;
            if (_wallet != null)
                _wallet.OnChanged += HandleEssenceChanged;
            if (_kamMana != null)
                _kamMana.OnManaChanged += HandleManaChanged;
            if (_state != null)
                _state.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_apManager != null)
            {
                _apManager.OnAPChanged    -= HandleAPChanged;
                _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
            }
            if (_collapseManager != null)
                _collapseManager.OnTileCollapsed -= HandleTileCollapsed;
            if (_wallet != null)
                _wallet.OnChanged -= HandleEssenceChanged;
            if (_kamMana != null)
                _kamMana.OnManaChanged -= HandleManaChanged;
            if (_state != null)
                _state.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (_collapseLabel != null)
                _collapseLabel.gameObject.SetActive(false);

            if (_essenceLabel != null)
                _essenceLabel.gameObject.SetActive(_wallet != null);
            if (_kamManaLabel != null)
                _kamManaLabel.gameObject.SetActive(_kamMana != null);

            // Başlangıç değerlerini doldur
            if (_apManager != null)
            {
                HandleAPChanged(_apManager.CurrentAP, _apManager.MaxAP);
                HandleTimeAdvanced(_apManager.CurrentDay, _apManager.CurrentSlot,
                                   _apManager.GetCurrentSlotName());
            }
            if (_wallet != null)
                HandleEssenceChanged();
            if (_kamMana != null)
                HandleManaChanged(_kamMana.CurrentMana, _kamMana.MaxMana);

            // Başlangıç görünürlüğü — event kaçırılsa bile state'le senkron
            HandleStateChanged(_state != null ? _state.State : GameState.Overworld);
        }

        // Overworld HUD'ı: savaş/yerleştirme ekranlarında Canvas'ı kapat (UI çakışmasını önler).
        // Canvas kapalıyken bile bileşen event dinlemeye devam eder; dönüşte etiketler güncel olur.
        private void HandleStateChanged(GameState state)
        {
            if (_canvas == null) return;
            _canvas.enabled = state == GameState.Overworld || state == GameState.ConfirmMission;
        }

        // ── Handler'lar ───────────────────────────────────────────────────────

        private void HandleAPChanged(int current, int max)
        {
            if (_apLabel == null) return;
            _sb.Clear();
            _sb.Append("AP  ");
            for (int i = 0; i < max; i++)
                _sb.Append(i < current ? "■" : "□");
            _sb.Append("  ").Append(current).Append('/').Append(max);
            _apLabel.text = _sb.ToString();
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (_timeLabel == null) return;
            _timeLabel.text = $"Gün {day}  ·  {slotName}";
        }

        private void HandleEssenceChanged()
        {
            if (_essenceLabel == null || _wallet == null) return;
            _essenceLabel.text =
                $"Öz  {EssenceType.Ates}:{_wallet.Get(EssenceType.Ates)}  " +
                $"{EssenceType.Su}:{_wallet.Get(EssenceType.Su)}  " +
                $"{EssenceType.Toprak}:{_wallet.Get(EssenceType.Toprak)}";
        }

        private void HandleManaChanged(int current, int max)
        {
            if (_kamManaLabel == null) return;
            _sb.Clear();
            _sb.Append("Mana  ");
            for (int i = 0; i < max; i++)
                _sb.Append(i < current ? "◆" : "◇");
            _sb.Append("  ").Append(current).Append('/').Append(max);
            _kamManaLabel.text = _sb.ToString();
        }

        private void HandleTileCollapsed(int removed, int total)
        {
            if (_collapseLabel == null) return;
            _collapseLabel.gameObject.SetActive(true);
            _collapseLabel.text = $"HARITA ÇÖKÜYOR  [ {total} karo silindi ]";
        }
    }
}
