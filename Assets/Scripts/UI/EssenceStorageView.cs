using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Data;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÖZ DEPOSU görünümü (KİTAP üstü): her öz türü için CANLI sayaç. <see cref="EssenceWallet.OnChanged"/>'e
    /// abone olup yenilenir; isim ve renk <see cref="EssenceConfigSO"/>'dan gelir (koda gömülü değil —
    /// Whiteboxing). Salt-okur: harcama/kazanım başka sistemlerde, burası yalnız gösterir.
    ///
    /// Panel gizliyken (GameObject inactive) OnEnable tetiklenmez; KİTAP açılınca OnEnable → anında
    /// güncel değerleri çeker, o yüzden başlangıç event'ini kaçırmak sorun değil.
    /// </summary>
    public class EssenceStorageView : MonoBehaviour
    {
        [System.Serializable]
        public struct Counter
        {
            public EssenceType     type;
            public TextMeshProUGUI amountLabel; // "15"
            public TextMeshProUGUI nameLabel;   // "Ateş" (opsiyonel)
            public Image           swatch;      // renk göstergesi (opsiyonel)
        }

        [SerializeField] private EssenceWallet   _wallet;
        [SerializeField] private EssenceConfigSO _config;
        [SerializeField] private Counter[]       _counters;

        private void OnEnable()
        {
            if (_wallet != null) _wallet.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_wallet != null) _wallet.OnChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_counters == null) return;

            foreach (var c in _counters)
            {
                int amt = _wallet != null ? _wallet.Get(c.type) : 0;
                if (c.amountLabel != null) c.amountLabel.text = amt.ToString();

                if (_config != null)
                {
                    Color col = _config.ColorOf(c.type);
                    if (c.nameLabel   != null) c.nameLabel.text = _config.NameOf(c.type);
                    if (c.swatch      != null) c.swatch.color   = col;
                    if (c.amountLabel != null) c.amountLabel.color = col;
                }
            }
        }
    }
}
