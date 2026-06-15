using System.Text;
using UnityEngine;
using TMPro;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Oyun içi debug HUD: Gün/Zaman dilimi, AP çubuğu, Öz sayacı, Kam mana, Kıyamet uyarısı.
    /// Tüm referanslar Inspector'dan bağlanır — SceneSetupTool otomatik kurar.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager  _apManager;
        [SerializeField] private MapCollapseManager  _collapseManager;
        [SerializeField] private EssenceManager      _essenceManager;
        [SerializeField] private KamManaManager      _kamMana;

        [Header("UI Etiketleri")]
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [SerializeField] private TextMeshProUGUI _apLabel;
        [SerializeField] private TextMeshProUGUI _essenceLabel;
        [SerializeField] private TextMeshProUGUI _kamManaLabel;
        [SerializeField] private TextMeshProUGUI _collapseLabel;

        private readonly StringBuilder _sb = new StringBuilder(48);

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnAPChanged    += HandleAPChanged;
                _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            }
            if (_collapseManager != null)
                _collapseManager.OnTileCollapsed += HandleTileCollapsed;
            if (_essenceManager != null)
                _essenceManager.OnEssenceChanged += HandleEssenceChanged;
            if (_kamMana != null)
                _kamMana.OnManaChanged += HandleManaChanged;
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
            if (_essenceManager != null)
                _essenceManager.OnEssenceChanged -= HandleEssenceChanged;
            if (_kamMana != null)
                _kamMana.OnManaChanged -= HandleManaChanged;
        }

        private void Start()
        {
            if (_collapseLabel != null)
                _collapseLabel.gameObject.SetActive(false);

            if (_essenceLabel != null)
                _essenceLabel.gameObject.SetActive(_essenceManager != null);
            if (_kamManaLabel != null)
                _kamManaLabel.gameObject.SetActive(_kamMana != null);

            // Başlangıç değerlerini doldur
            if (_apManager != null)
            {
                HandleAPChanged(_apManager.CurrentAP, _apManager.MaxAP);
                HandleTimeAdvanced(_apManager.CurrentDay, _apManager.CurrentSlot,
                                   _apManager.GetCurrentSlotName());
            }
            if (_essenceManager != null)
                HandleEssenceChanged(_essenceManager.CurrentEssence, 0);
            if (_kamMana != null)
                HandleManaChanged(_kamMana.CurrentMana, _kamMana.MaxMana);
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

        private void HandleEssenceChanged(int current, int delta)
        {
            if (_essenceLabel == null) return;
            _essenceLabel.text = $"Öz  {current}";
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
