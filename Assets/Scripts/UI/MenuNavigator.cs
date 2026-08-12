using System;
using UnityEngine;
using UnityEngine.UI;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Ana menü gezinme kabuğu (her ekranda kalıcı): sağ-alt KİTAP/ÇANTA/HARİTA sekmeleri +
    /// sağ-üst ⚙ ayar düğmesi. Tam-ekran panelleri (<see cref="MenuScreenPanel"/>) açar/kapatır;
    /// aynı anda YALNIZ biri açık olur.
    ///
    /// Event-driven (CLAUDE.md): panelleri doğrudan bilmeyen sistemler <see cref="OnScreenChanged"/>
    /// / <see cref="OnMenuOpenChanged"/> dinler (örn. overworld IMGUI HUD'ları menü açıkken kendini
    /// gizleyebilir). Girdi bloğu MapInputHandler tarafında EventSystem üzerinden hâlleniyor —
    /// bu sınıf harita girişini doğrudan bilmez (tek yönlü bağımlılık).
    ///
    /// Durum entegrasyonu: <see cref="_stateManager"/> atanmışsa kabuk yalnız Overworld'de görünür;
    /// savaşa/deployment'a geçince kabuk gizlenir ve açık menü kapanır.
    /// </summary>
    public class MenuNavigator : MonoBehaviour
    {
        [Header("Paneller")]
        [Tooltip("Yönetilen tam-ekran menü panelleri (KİTAP/ÇANTA/HARİTA/AYARLAR). Sıra önemsiz.")]
        [SerializeField] private MenuScreenPanel[] _panels;

        [Tooltip("Yalnız OVERWORLD'de görünecek sekmeler (KİTAP/ÇANTA/HARİTA). Ayar dişlisi bu " +
                 "listede OLMAMALI — o her durumda erişilebilir kalır.")]
        [SerializeField] private GameObject[] _overworldOnlyTabs;

        [Header("Kalıcı Kabuk")]
        [Tooltip("Sekmeleri + ayar düğmesini barındıran kök. Overworld dışında gizlenir.")]
        [SerializeField] private GameObject _persistentBar;
        [SerializeField] private Button _bookTab;
        [SerializeField] private Button _bagTab;
        [SerializeField] private Button _mapTab;
        [SerializeField] private Button _settingsButton;

        [Header("Bağımlılıklar (opsiyonel)")]
        [Tooltip("Atanırsa kabuk yalnız Overworld'de görünür; savaşa girince menü otomatik kapanır.")]
        [SerializeField] private GameStateManager _stateManager;

        /// <summary>Şu an açık olan ekran; hiçbiri açık değilse <see cref="MenuScreen.None"/>.</summary>
        public MenuScreen Current { get; private set; } = MenuScreen.None;

        /// <summary>Herhangi bir tam-ekran menü açık mı?</summary>
        public bool IsMenuOpen => Current != MenuScreen.None;

        /// <summary>Aktif ekran değiştiğinde (kapanış dahil, None ile) tetiklenir.</summary>
        public event Action<MenuScreen> OnScreenChanged;

        /// <summary>Menü açıldı/kapandı SINIRI geçildiğinde tetiklenir (ekran değiştirmede değil).</summary>
        public event Action<bool> OnMenuOpenChanged;

        private void Awake()
        {
            WireTab(_bookTab,        MenuScreen.Book);
            WireTab(_bagTab,         MenuScreen.Bag);
            WireTab(_mapTab,         MenuScreen.Map);
            WireTab(_settingsButton, MenuScreen.Settings);
            HideAllPanels();
            MenuState.IsAnyOpen = false; // domain reload'da kalmış olabilecek stale değeri sıfırla
        }

        private void OnEnable()
        {
            if (_stateManager != null)
                _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null)
                _stateManager.OnStateChanged -= HandleStateChanged;
            MenuState.IsAnyOpen = false; // kabuk kapanırsa overworld HUD'ları geri gelsin
        }

        private void Update()
        {
            // Esc → açıksa kapat, kapalıysa AYARLAR'ı aç.
            // "Aç" kısmı 2026-08-12'de eklendi: üst sekme barı overworld dışında gizlendiği için
            // savaşta/yerleştirmede ⚙ düğmesine ulaşılamıyor, ayarlar (ses/parlaklık/kamera zoom)
            // erişilemez oluyordu. Esc her durumda ayarları açar.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (IsMenuOpen) CloseScreen();
            else            OpenScreen(MenuScreen.Settings);
        }

        /// <summary>Sekme davranışı: aynı ekran zaten açıksa kapatır, değilse o ekrana geçer.</summary>
        public void ToggleScreen(MenuScreen screen)
        {
            if (Current == screen) CloseScreen();
            else                   OpenScreen(screen);
        }

        public void OpenScreen(MenuScreen screen)
        {
            if (screen == MenuScreen.None) { CloseScreen(); return; }
            if (Current == screen) return;

            bool wasOpen = IsMenuOpen;
            SetPanelVisible(Current, false);   // öncekini gizle (varsa)
            Current = screen;
            SetPanelVisible(Current, true);
            MenuState.IsAnyOpen = true;        // overworld IMGUI HUD'ları (öz deposu) gizlensin

            OnScreenChanged?.Invoke(Current);
            if (!wasOpen) OnMenuOpenChanged?.Invoke(true);
        }

        public void CloseScreen()
        {
            if (!IsMenuOpen) return;
            SetPanelVisible(Current, false);
            Current = MenuScreen.None;
            MenuState.IsAnyOpen = false;        // overworld IMGUI HUD'ları geri gelsin
            OnScreenChanged?.Invoke(MenuScreen.None);
            OnMenuOpenChanged?.Invoke(false);
        }

        private void HandleStateChanged(GameState state)
        {
            bool overworld = state == GameState.Overworld;

            // ÜST BAR AÇIK KALIR, yalnız overworld'e özel SEKMELER gizlenir.
            // Eskiden barın tamamı kapatılıyordu; ⚙ ayar düğmesi de barın çocuğu olduğu için
            // savaşta/yerleştirmede ayarlara HİÇ ulaşılamıyordu (2026-08-12 hata raporu).
            // KİTAP/ÇANTA/HARİTA savaşta anlamsız → onlar gizli; dişli her durumda görünür.
            if (_overworldOnlyTabs != null)
                foreach (var tab in _overworldOnlyTabs)
                    if (tab != null) tab.SetActive(overworld);

            if (_persistentBar != null) _persistentBar.SetActive(true);
            if (!overworld) CloseScreen();
        }

        private void WireTab(Button button, MenuScreen screen)
        {
            if (button == null) return;
            button.onClick.AddListener(() => ToggleScreen(screen));
        }

        private void HideAllPanels()
        {
            if (_panels != null)
                foreach (var p in _panels)
                    if (p != null) p.Hide();
            Current = MenuScreen.None;
        }

        private void SetPanelVisible(MenuScreen screen, bool visible)
        {
            if (screen == MenuScreen.None || _panels == null) return;
            foreach (var p in _panels)
                if (p != null && p.Screen == screen) { p.SetVisible(visible); return; }
        }
    }
}
