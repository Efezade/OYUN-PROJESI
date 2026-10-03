using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// KİTAP'ın KARAKTERLER sayfası (2026-09-06, Efe'nin isteği): savaşta üretilebilen Kam DIŞI
    /// karakterler, sayfa sayfa çevrilerek. Sol sayfada büst, sağ sayfada öz maliyeti ve statlar.
    ///
    /// NEDEN SAYFA SAYFA: eski hâlde dört sınıf küçük kutulara sıkıştırılmıştı (ikisi "kilitli"
    /// yer tutucuydu) — ne büst sığıyordu ne de maliyet okunuyordu. Kitap zaten bir kitap; bir
    /// karakter bir açılış (spread) olunca hem splash art'a yer kalıyor hem de "hangi birim kaça
    /// mal oluyor" sorusu tek bakışta cevaplanıyor.
    ///
    /// VERİ CANLI: ad/statlar <see cref="CharacterClassData"/>'dan, maliyet <see cref="UnitRecipe"/>'ten
    /// okunur; kese yetmiyorsa maliyet kızarır. Yani buradaki sayı savaş ekranında ödenecek sayının
    /// TA KENDİSİ — ikinci bir liste tutulmuyor.
    ///
    /// Büstler yer tutucudur (<c>InkArtFactory.Bust</c> ile prosedürel çizilir); Efe'nin gerçek
    /// splash art'ları geldiğinde tek iş her girdinin <c>_bust</c> alanını değiştirmek.
    ///
    /// EVRİM + YETENEKLER (2026-10-03, Efe'nin isteği): sağ sayfanın altında sınıfın ÜÇ
    /// yeteneği ve ÜÇ EVRİM SLOTU durur. Slotlar SIRAYLA açılır (I → II → III): açık olan
    /// altın, sıradaki bedelini gösterir ve tıklanınca özle açılır, sonrakiler kilitli bekler.
    /// </summary>
    public class CharacterBookView : MonoBehaviour
    {
        /// <summary>Kitaptaki tek bir karakter sayfası.</summary>
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Savaş ekranındaki üretim tarifi — maliyet BURADAN okunur (tek doğruluk kaynağı).")]
            [SerializeField] private UnitRecipe _recipe;
            [Tooltip("Tarif yoksa (henüz üretilemeyen karakter) sınıf doğrudan verilebilir.")]
            [SerializeField] private CharacterClassData _fallbackClass;
            [Tooltip("Büst görseli — şimdilik prosedürel yer tutucu.")]
            [SerializeField] private Sprite _bust;

            public UnitRecipe Recipe => _recipe;
            public Sprite     Bust   => _bust;

            public CharacterClassData Class
                => _recipe != null && _recipe.UnitClass != null ? _recipe.UnitClass : _fallbackClass;
        }

        /// <summary>Tek bir evrim slotunun parçaları (sahnede kurulur).</summary>
        [System.Serializable]
        public class EvolutionSlot
        {
            [SerializeField] private Button          _button;
            [SerializeField] private Image           _background;
            [SerializeField] private TextMeshProUGUI _title;
            [SerializeField] private TextMeshProUGUI _name;
            [SerializeField] private TextMeshProUGUI _desc;
            [SerializeField] private TextMeshProUGUI _status;

            public Button          Button     => _button;
            public Image           Background => _background;
            public TextMeshProUGUI Title      => _title;
            public TextMeshProUGUI Name       => _name;
            public TextMeshProUGUI Desc       => _desc;
            public TextMeshProUGUI Status     => _status;
        }

        [Header("Bağımlılıklar")]
        [SerializeField] private EssenceWallet   _wallet;
        [SerializeField] private EssenceConfigSO _essenceConfig;
        [Tooltip("Sınıf evrimlerinin ilerlemesi — slotlar buradan okur ve buraya yazar.")]
        [SerializeField] private ClassEvolutionProgress _evolutions;

        [Header("Sayfalar")]
        [SerializeField] private Entry[] _entries;

        [Header("Sol sayfa — büst")]
        [SerializeField] private Image           _bustImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _loreLabel;

        [Header("Sağ sayfa — künye")]
        [SerializeField] private TextMeshProUGUI _costLabel;
        [Tooltip("Stat ADLARI sütunu (sol); değerler ayrı sütunda — orantılı fontta " +
                 "boşlukla hizalama tutmuyor.")]
        [SerializeField] private TextMeshProUGUI _statNameLabel;
        [SerializeField] private TextMeshProUGUI _statsLabel;
        [SerializeField] private TextMeshProUGUI _pageLabel;
        [SerializeField] private Button          _prevButton;
        [SerializeField] private Button          _nextButton;

        [Header("Yetenekler + evrim (2026-10-03)")]
        [SerializeField] private TextMeshProUGUI _abilitiesLabel;
        [SerializeField] private EvolutionSlot[] _evoSlots;
        [SerializeField] private Color _slotOpen      = new(0.94f, 0.80f, 0.42f);
        [SerializeField] private Color _slotAvailable = new(0.96f, 0.92f, 0.80f);
        [SerializeField] private Color _slotLocked    = new(0.78f, 0.74f, 0.66f);

        [Header("Renk")]
        [SerializeField] private Color _ink        = new(0.13f, 0.10f, 0.07f);
        [SerializeField] private Color _inkSoft    = new(0.36f, 0.29f, 0.20f);
        [Tooltip("Kese yetmiyorsa maliyet bu renge döner.")]
        [SerializeField] private Color _tooPricey  = new(0.66f, 0.22f, 0.16f);

        private int _page;

        private void Awake()
        {
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Turn(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Turn(+1));

            if (_evoSlots != null)
                for (int i = 0; i < _evoSlots.Length; i++)
                {
                    int slot = i;                                   // kapanış değişkeni
                    if (_evoSlots[i]?.Button != null) _evoSlots[i].Button.onClick.AddListener(() => Unlock(slot));
                }
        }

        private void OnEnable()
        {
            if (_wallet     != null) _wallet.OnChanged     += Refresh;
            if (_evolutions != null) _evolutions.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_wallet     != null) _wallet.OnChanged     -= Refresh;
            if (_evolutions != null) _evolutions.OnChanged -= Refresh;
        }

        /// <summary>Slota tıklandı: yalnız SIRADAKİ evrim açılabilir (sıra atlanmaz).</summary>
        private void Unlock(int slot)
        {
            if (_evolutions == null || _entries == null || _entries.Length == 0) return;
            CharacterClassData data = _entries[Mathf.Clamp(_page, 0, _entries.Length - 1)]?.Class;
            if (data == null || slot != _evolutions.LevelOf(data)) return;
            _evolutions.TryUnlockNext(data);   // başarısızsa sessiz: sebep slotta yazıyor
        }

        /// <summary>Sayfa çevirir. Uçlarda DÖNMEZ — kitapta ilk sayfadan öncesi yoktur.</summary>
        private void Turn(int delta)
        {
            if (_entries == null || _entries.Length == 0) return;
            _page = Mathf.Clamp(_page + delta, 0, _entries.Length - 1);
            Refresh();
        }

        private void Refresh()
        {
            if (_entries == null || _entries.Length == 0) return;
            _page = Mathf.Clamp(_page, 0, _entries.Length - 1);

            Entry e = _entries[_page];
            CharacterClassData data = e?.Class;

            if (_bustImage != null)
            {
                if (e?.Bust != null) _bustImage.sprite = e.Bust;
                _bustImage.enabled = e?.Bust != null;
                _bustImage.color   = _ink;
            }

            if (_nameLabel != null)
                _nameLabel.text = data != null ? data.ClassName.ToUpperInvariant() : "—";

            if (_loreLabel != null)
                _loreLabel.text = data != null ? data.Lore : "";

            if (_costLabel != null)
            {
                var cost = e?.Recipe != null ? e.Recipe.Cost : null;
                if (cost == null || cost.Count == 0)
                {
                    _costLabel.text  = "ÜRETİM BEDELİ\nyok";
                    _costLabel.color = _inkSoft;
                }
                else
                {
                    _costLabel.text  = "ÜRETİM BEDELİ\n" + e.Recipe.CostString(_essenceConfig);
                    // Kese yetmiyorsa kızarır: "alabilir miyim" sorusu kitapta cevaplansın.
                    bool affordable = _wallet == null || _wallet.CanAfford(cost);
                    _costLabel.color = affordable ? _ink : _tooPricey;
                }
            }

            if (_statNameLabel != null)
                _statNameLabel.text = data == null ? "" :
                    "CAN\nSALDIRI\nSAVUNMA\nHAREKET\nHIZ\nMENZİL" +
                    (data.HasManaSystem ? "\nMANA" : "");

            if (_statsLabel != null)
                _statsLabel.text = data == null ? "" :
                    $"{data.BaseMaxHP}\n{data.BaseAttack}\n{data.BaseDefense}\n" +
                    $"{data.MoveRange}\n{data.Speed}\n{data.AttackRange}" +
                    (data.HasManaSystem ? $"\n{data.MaxMana}" : "");

            RefreshAbilities(data);
            RefreshEvolutions(data);

            if (_pageLabel != null)
                _pageLabel.text = $"{_page + 1} / {_entries.Length}";

            // Uçlarda düğme sönük: kitabın sonu olduğunu tıklamadan görebilmeli.
            if (_prevButton != null) _prevButton.interactable = _page > 0;
            if (_nextButton != null) _nextButton.interactable = _page < _entries.Length - 1;
        }

        /// <summary>Sınıfın üç yeteneği: "• Çift Ok — ... (2 tur)".</summary>
        private void RefreshAbilities(CharacterClassData data)
        {
            if (_abilitiesLabel == null) return;
            if (data == null || data.ClassAbilities.Count == 0) { _abilitiesLabel.text = "YETENEKLER\n—"; return; }

            string s = "YETENEKLER";
            foreach (ClassAbility ab in data.ClassAbilities)
                if (ab != null) s += $"\n• <b>{ab.Name}</b> — {ab.Description} <i>({ab.Cooldown} tur)</i>";
            _abilitiesLabel.text = s;
        }

        /// <summary>Üç evrim slotu: açık · sıradaki (bedel) · kilitli.</summary>
        private void RefreshEvolutions(CharacterClassData data)
        {
            if (_evoSlots == null) return;
            int level = _evolutions != null ? _evolutions.LevelOf(data) : 0;

            for (int i = 0; i < _evoSlots.Length; i++)
            {
                EvolutionSlot slot = _evoSlots[i];
                if (slot == null) continue;
                ClassEvolution evo = data != null && i < data.Evolutions.Count ? data.Evolutions[i] : null;

                bool open      = evo != null && i < level;
                bool next      = evo != null && i == level;
                bool affordable = next && _evolutions != null && _evolutions.CanAffordNext(data);

                if (slot.Title  != null) slot.Title.text = $"EVRİM {Roman(i + 1)}";
                if (slot.Name   != null) slot.Name.text  = evo != null ? evo.Name : "—";
                if (slot.Desc   != null) slot.Desc.text  = evo != null ? evo.Description : "";
                if (slot.Background != null)
                    slot.Background.color = open ? _slotOpen : next ? _slotAvailable : _slotLocked;

                if (slot.Status != null)
                {
                    slot.Status.text  = evo == null ? ""
                                      : open      ? "AÇIK"
                                      : next      ? $"AÇ: {CostText(evo)}"
                                      : $"önce {Roman(i)}";
                    slot.Status.color = next && !affordable ? _tooPricey : _ink;
                }

                if (slot.Button != null) slot.Button.interactable = affordable;
            }
        }

        private static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", _ => n.ToString() };

        private static string CostText(ClassEvolution evo)
        {
            string s = "";
            foreach (var c in evo.Cost)
            {
                if (c.amount <= 0) continue;
                string unit = c.type switch
                {
                    EssenceType.Tas  => "taş",
                    EssenceType.Doga => "doğa",
                    _                => c.type.ToString().ToLowerInvariant()
                };
                s += (s.Length > 0 ? " · " : "") + $"{c.amount} {unit}";
            }
            return s.Length > 0 ? s : "bedelsiz";
        }
    }
}
