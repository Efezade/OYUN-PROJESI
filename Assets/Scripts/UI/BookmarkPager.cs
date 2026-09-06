using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.UI
{
    /// <summary>
    /// KİTAP'ın YER İMLERİ (2026-09-04): aynı kitap gövdesi içinde birden çok SAYFA TAKIMI
    /// arasında geçiş (SINIFLAR ↔ KAM'IN YETENEKLERİ). Kenardaki yer imine basılınca o sayfa
    /// takımı görünür, diğerleri gizlenir.
    ///
    /// Neden ayrı ekran değil: yetenek ağacı KİTAP'ın içinde duracak (Efe'nin kararı 2026-09-04).
    /// Yeni bir <see cref="MenuScreen"/> açmak sekme çubuğunu kalabalıklaştırırdı; kitabın kendi
    /// yer imleri hem mockup'taki (game UI.pdf) dile uyuyor hem de ileride "evrim", "günce" gibi
    /// sayfalar aynı desenle ekleniyor.
    ///
    /// Bu sınıf İÇERİĞİ bilmez — yalnız görünürlük çevirir (Single Responsibility).
    /// </summary>
    public class BookmarkPager : MonoBehaviour
    {
        [System.Serializable]
        public class Page
        {
            [Tooltip("Yer imine basılınca görünecek sayfa kökü.")]
            [SerializeField] private GameObject _root;
            [Tooltip("Kitabın kenarındaki yer imi düğmesi.")]
            [SerializeField] private Button _bookmark;
            [Tooltip("Seçili yer iminin arka planı bu renge boyanır (atanmazsa renk değişmez).")]
            [SerializeField] private Image _bookmarkBackground;

            public GameObject Root       => _root;
            public Button     Bookmark   => _bookmark;
            public Image      Background => _bookmarkBackground;
        }

        [SerializeField] private Page[] _pages;

        [Tooltip("Açılışta hangi sayfa görünsün (dizideki sıra).")]
        [SerializeField, Min(0)] private int _defaultPage;

        [Header("Yer imi renkleri")]
        [SerializeField] private Color _activeColor   = new(0.96f, 0.92f, 0.80f);
        [SerializeField] private Color _inactiveColor = new(0.83f, 0.75f, 0.58f);

        private int _current = -1;

        private void Awake()
        {
            if (_pages == null) return;
            for (int i = 0; i < _pages.Length; i++)
            {
                int index = i;                                  // kapanış değişkeni
                if (_pages[i]?.Bookmark != null)
                    _pages[i].Bookmark.onClick.AddListener(() => Show(index));
            }
        }

        private void OnEnable()
        {
            // Kitap her açıldığında varsayılan sayfaya döner: oyuncu kitabı kapatıp açtığında
            // nerede kaldığını hatırlamak zorunda kalmasın.
            Show(_defaultPage);
        }

        /// <summary>Verilen sayfayı gösterir, diğerlerini gizler.</summary>
        public void Show(int index)
        {
            if (_pages == null || _pages.Length == 0) return;
            index = Mathf.Clamp(index, 0, _pages.Length - 1);
            _current = index;

            for (int i = 0; i < _pages.Length; i++)
            {
                Page p = _pages[i];
                if (p == null) continue;
                bool on = i == index;
                if (p.Root != null && p.Root.activeSelf != on) p.Root.SetActive(on);
                if (p.Background != null) p.Background.color = on ? _activeColor : _inactiveColor;
            }
        }

        /// <summary>Şu an açık sayfanın sırası (hiçbiri kurulmadıysa -1).</summary>
        public int Current => _current;
    }
}
