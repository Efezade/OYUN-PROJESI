using System.Text;
using UnityEngine;
using TMPro;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Oyun içi debug HUD: Gün/Zaman dilimi, AP çubuğu, Kıyamet uyarısı.
    /// Tüm referanslar Inspector'dan bağlanır — SceneSetupTool otomatik kurar.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager _apManager;
        [SerializeField] private MapCollapseManager _collapseManager;

        [Header("UI Etiketleri")]
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [SerializeField] private TextMeshProUGUI _apLabel;
        [SerializeField] private TextMeshProUGUI _collapseLabel;

        private readonly StringBuilder _sb = new StringBuilder(32);

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnAPChanged    += HandleAPChanged;
                _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            }
            if (_collapseManager != null)
                _collapseManager.OnTileCollapsed += HandleTileCollapsed;
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
        }

        private void Start()
        {
            if (_collapseLabel != null)
                _collapseLabel.gameObject.SetActive(false);

            if (_apManager == null) return;
            HandleAPChanged(_apManager.CurrentAP, _apManager.MaxAP);
            HandleTimeAdvanced(_apManager.CurrentDay, _apManager.CurrentSlot,
                               _apManager.GetCurrentSlotName());
        }

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

        private void HandleTileCollapsed(int removed, int total)
        {
            if (_collapseLabel == null) return;
            _collapseLabel.gameObject.SetActive(true);
            _collapseLabel.text = $"HARITA ÇÖKÜYOR  [ {total} karo silindi ]";
        }
    }
}
