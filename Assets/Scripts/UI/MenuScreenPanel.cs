using UnityEngine;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Tam-ekran bir menü panelinin kökü. Hangi <see cref="MenuScreen"/> olduğunu bilir ve
    /// yalnızca kendi görünürlüğünden sorumludur (Single Responsibility). Ekran-özel içerik
    /// mantığı (KİTAP öz deposu, ÇANTA kartları vb.) ileride AYNI GameObject'e eklenecek ayrı
    /// controller'lara bırakılır — bu sınıf onları bilmez.
    ///
    /// Görünürlük: <see cref="_canvasGroup"/> atanmışsa alpha/interactable/blocksRaycasts ile
    /// (yumuşak geçişe hazır); atanmamışsa GameObject aktifliğiyle yönetilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuScreenPanel : MonoBehaviour
    {
        [Tooltip("Bu panelin temsil ettiği menü ekranı.")]
        [SerializeField] private MenuScreen _screen = MenuScreen.None;

        [Tooltip("Opsiyonel — atanırsa görünürlük alpha üzerinden yönetilir (fade'e hazır). " +
                 "Atanmazsa GameObject.SetActive kullanılır.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        public MenuScreen Screen => _screen;

        /// <summary>Paneli görünür yapar.</summary>
        public void Show() => SetVisible(true);

        /// <summary>Paneli gizler.</summary>
        public void Hide() => SetVisible(false);

        public void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                _canvasGroup.alpha          = visible ? 1f : 0f;
                _canvasGroup.interactable   = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
