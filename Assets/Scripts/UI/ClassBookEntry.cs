using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// KİTAP'ta bir sınıf bölümü: portre + evrim maliyet satırı (Sv2/Sv3 öz maliyeti
    /// <see cref="CharacterClassData.GetEssenceCost"/>'tan). <see cref="_data"/> null ise "KİLİTLİ"
    /// (henüz gelmemiş sınıf — Mage/Healer) durumu gösterilir.
    ///
    /// ŞİMDİLİK SALT GÖRÜNÜM: evrim/yükseltme etkileşimi (öz harcayıp seviye atlama) sınıf başına
    /// GÜNCEL SEVİYE state'i (roster) netleşince eklenecek — o yüzden burada tık/aksiyon yok.
    /// </summary>
    public class ClassBookEntry : MonoBehaviour
    {
        [Tooltip("Bu bölümün sınıf verisi. BOŞSA kilitli/gelecek sınıf olarak gösterilir.")]
        [SerializeField] private CharacterClassData _data;

        [SerializeField] private Image           _portrait;
        [SerializeField] private TextMeshProUGUI _costLabel;
        [Tooltip("_data null iken görünür olacak 'kilitli' kaplaması (opsiyonel).")]
        [SerializeField] private GameObject      _lockedOverlay;

        public bool IsLocked => _data == null;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            bool locked = _data == null;

            if (_lockedOverlay != null) _lockedOverlay.SetActive(locked);

            if (_portrait != null)
            {
                if (!locked && _data.Portrait != null)
                {
                    _portrait.sprite = _data.Portrait;
                    _portrait.color  = Color.white;
                }
                else
                {
                    // Portre yoksa sınıf rengi (kilitliyse koyu gri) — böyle de ayırt edilir.
                    _portrait.sprite = null;
                    _portrait.color  = locked ? new Color(0.18f, 0.16f, 0.14f, 1f) : _data.UnitColor;
                }
            }

            if (_costLabel != null)
                _costLabel.text = locked
                    ? "Kilitli"
                    : $"Sv2: {_data.GetEssenceCost(2)} öz    Sv3: {_data.GetEssenceCost(3)} öz";
        }
    }
}
