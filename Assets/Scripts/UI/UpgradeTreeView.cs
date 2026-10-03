using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// KİTAP'TAKİ BİR GELİŞTİRME AĞACI sayfası: gövdeden dallanan düğümler + altta künye kartı
    /// (`Gerekli belgeler/game UI.pdf` s.6 dili). Eski adı <c>KamSkillTreeView</c> — 2026-10-03'te
    /// genelleşti (dosya GUID'i korundu): aynı sınıf YETENEK (büyü) ve KAROLAR (değiştirilebilir
    /// karo) sekmelerini çizer; hangisi olduğunu bağlı <see cref="UpgradeTreeProgress"/> belirler.
    ///
    /// SORUMLULUK: yalnız GÖSTERİR ve tıklamayı iletir. Neyin açılabildiği ve neye kaç öz gittiği
    /// ilerleme bileşeninin, ağacın şekli <see cref="UpgradeTreeSO"/>'nun işi. Düğüm nesneleri
    /// sahnede kurulur (SceneSetupTool) — bu sınıf onları YARATMAZ, whiteboxing gereği hepsi
    /// Inspector'dan bağlanır.
    ///
    /// DÖRT DURUM TEK BAKIŞTA (yoksa ağaç "tıkla da gör" bulmacasına döner):
    ///   • KİLİTLİ (ön koşul kapalı) — soluk kâğıt, ASMA KİLİT ikonu.
    ///   • AÇILABİLİR — kâğıt canlı, asma kilit durur ama halka koyulaşır; öz yetiyorsa düğme aktif.
    ///   • AÇIK — düğümün kendi ikonu + altın disk + seviye rozeti.
    ///   • TAVAN — dolu altın, düğme "EN ÜST SEVİYE" der.
    /// Seçili düğümün HALKASI vurgulanır (mockup'ta seçim rengi yok; okunurluk için eklendi).
    /// </summary>
    public class UpgradeTreeView : MonoBehaviour
    {
        /// <summary>Sahnedeki tek bir düğüm rozetinin parçaları.</summary>
        [System.Serializable]
        public class NodeView
        {
            [SerializeField] private string _skillId;   // düğüm id'si (ad tarihsel)
            [SerializeField] private Button _button;
            [Tooltip("Durum rengini taşıyan dolu daire (madalyon zemini).")]
            [SerializeField] private Image  _disc;
            [Tooltip("Mürekkep halka — seçili düğümde vurgulanır.")]
            [SerializeField] private Image  _ring;
            [Tooltip("Ortadaki glif: kilitliyken asma kilit, açıkken düğümün ikonu.")]
            [SerializeField] private Image  _icon;
            [SerializeField] private Sprite _lockedIcon;
            [SerializeField] private Sprite _openIcon;
            [SerializeField] private TextMeshProUGUI _nameLabel;
            [Tooltip("Seviye rozeti diski — yalnız AÇIK düğümlerde görünür.")]
            [SerializeField] private Image  _levelBadge;
            [SerializeField] private TextMeshProUGUI _levelLabel;

            public string Id       => _skillId;
            public Button Button   => _button;
            public Image  Disc     => _disc;
            public Image  Ring     => _ring;
            public Image  Icon     => _icon;
            public Sprite LockedIcon => _lockedIcon;
            public Sprite OpenIcon   => _openIcon;
            public Image  LevelBadge => _levelBadge;
            public TextMeshProUGUI NameLabel  => _nameLabel;
            public TextMeshProUGUI LevelLabel => _levelLabel;
        }

        [Header("Bağımlılıklar")]
        [Tooltip("Hangi ağaç: KamSkillProgress (büyüler) ya da AugmentTreeProgress (karolar).")]
        [SerializeField] private UpgradeTreeProgress _progress;
        [SerializeField] private EssenceWallet       _wallet;

        [Header("Düğümler")]
        [SerializeField] private NodeView[] _nodes;

        [Header("Künye kartı")]
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailBody;
        [SerializeField] private TextMeshProUGUI _detailCost;
        [SerializeField] private TextMeshProUGUI _walletLabel;
        [SerializeField] private Button          _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        [Header("Durum renkleri")]
        [Tooltip("Ön koşulu kapalı düğüm — soluk kâğıt.")]
        [SerializeField] private Color _lockedColor    = new(0.78f, 0.74f, 0.66f);
        [Tooltip("Açılabilir düğüm — temiz kâğıt.")]
        [SerializeField] private Color _availableColor = new(0.96f, 0.92f, 0.80f);
        [Tooltip("Açık düğüm — altın.")]
        [SerializeField] private Color _unlockedColor  = new(0.94f, 0.80f, 0.42f);
        [Tooltip("Tavana gelmiş düğüm — dolu altın.")]
        [SerializeField] private Color _maxedColor     = new(1.00f, 0.85f, 0.20f);
        [SerializeField] private Color _inkColor       = new(0.13f, 0.10f, 0.07f);
        [Tooltip("Seçili düğümün halkası bu renge boyanır.")]
        [SerializeField] private Color _selectedColor  = new(0.55f, 0.25f, 0.10f);

        private string _selectedId;

        private void Awake()
        {
            if (_nodes != null)
                foreach (NodeView nv in _nodes)
                {
                    if (nv?.Button == null) continue;
                    string id = nv.Id;                          // kapanış değişkeni: döngü değişkeni DEĞİL
                    nv.Button.onClick.AddListener(() => Select(id));
                }

            if (_actionButton != null) _actionButton.onClick.AddListener(Advance);
        }

        private void OnEnable()
        {
            if (_progress != null) _progress.OnChanged += Refresh;
            if (_wallet   != null) _wallet.OnChanged   += Refresh;

            // Sayfa açılırken bir şey seçili olsun: boş künye "bozuk mu?" hissi verir.
            if (string.IsNullOrEmpty(_selectedId) && _nodes != null && _nodes.Length > 0)
                _selectedId = _nodes[0].Id;
            Refresh();
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.OnChanged -= Refresh;
            if (_wallet   != null) _wallet.OnChanged   -= Refresh;
        }

        // ── Etkileşim ────────────────────────────────────────────────────────

        private void Select(string id)
        {
            _selectedId = id;
            Refresh();
        }

        private void Advance()
        {
            if (_progress == null || string.IsNullOrEmpty(_selectedId)) return;
            _progress.TryAdvance(_selectedId);   // başarısızsa sessiz: sebebi künyede zaten yazıyor
        }

        // ── Çizim ────────────────────────────────────────────────────────────

        private void Refresh()
        {
            RefreshNodes();
            RefreshDetail();
            RefreshWallet();
        }

        private UpgradeTreeSO.Node NodeOf(string id)
            => _progress != null && _progress.Tree != null ? _progress.Tree.Find(id) : null;

        private void RefreshNodes()
        {
            if (_nodes == null || _progress == null) return;

            foreach (NodeView nv in _nodes)
            {
                if (nv == null) continue;
                UpgradeTreeSO.Node node = NodeOf(nv.Id);

                int  level     = _progress.LevelOf(nv.Id);
                bool unlocked  = level > 0;
                bool maxed     = node != null && level >= node.MaxLevel;
                bool reachable = node != null && _progress.PrerequisiteMet(node);
                bool selected  = nv.Id == _selectedId;

                if (nv.Disc != null)
                    nv.Disc.color = maxed     ? _maxedColor
                                  : unlocked  ? _unlockedColor
                                  : reachable ? _availableColor
                                  : _lockedColor;

                if (nv.Ring != null)
                    nv.Ring.color = selected ? _selectedColor
                                  : reachable || unlocked ? _inkColor
                                  : new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.45f);

                // Kilitliyken asma kilit, açıkken düğümün kendi glifi (mockup'taki okuma biçimi).
                if (nv.Icon != null)
                {
                    Sprite s = unlocked ? nv.OpenIcon : nv.LockedIcon;
                    if (s != null) nv.Icon.sprite = s;
                    nv.Icon.color = unlocked || reachable
                                  ? _inkColor
                                  : new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.5f);
                }

                if (nv.NameLabel != null)
                {
                    nv.NameLabel.text  = _progress.NameOf(nv.Id);
                    nv.NameLabel.color = unlocked || reachable
                                       ? _inkColor
                                       : new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.55f);
                }

                if (nv.LevelBadge != null && nv.LevelBadge.gameObject.activeSelf != unlocked)
                    nv.LevelBadge.gameObject.SetActive(unlocked);

                if (nv.LevelLabel != null && unlocked && node != null)
                    nv.LevelLabel.text = $"{level}/{node.MaxLevel}";
            }
        }

        private void RefreshDetail()
        {
            if (_progress == null) return;

            UpgradeTreeSO.Node node = NodeOf(_selectedId);

            if (_detailName != null)
                _detailName.text = string.IsNullOrEmpty(_selectedId) ? "—" : _progress.NameOf(_selectedId);

            // Seçili düğümün ŞU ANKİ hâli (seviyeli kopya): oyuncu yükseltmenin ne getirdiğini
            // kartın kendi dilinde okur. Mana bedeli de burada yazar.
            if (_detailBody != null)
                _detailBody.text = string.IsNullOrEmpty(_selectedId) ? "" : _progress.DescribeCurrent(_selectedId);

            bool canAct = node != null && _progress.CanAffordNext(node);
            int  level  = _progress.LevelOf(_selectedId);

            if (_detailCost != null)
                _detailCost.text = CostLine(node, level);

            if (_actionLabel != null)
                _actionLabel.text = node == null                     ? "—"
                                  : level >= node.MaxLevel           ? "EN ÜST SEVİYE"
                                  : !_progress.PrerequisiteMet(node) ? "KİLİTLİ"
                                  : level == 0                       ? "AÇ"
                                  : "YÜKSELT";

            if (_actionButton != null) _actionButton.interactable = canAct;
        }

        /// <summary>Bedel satırı — kilidin sebebi de burada yazar (düğme sessizce sönük kalmasın).</summary>
        private string CostLine(UpgradeTreeSO.Node node, int level)
        {
            if (node == null) return "";
            if (level >= node.MaxLevel) return "En üst seviyede.";

            if (!_progress.PrerequisiteMet(node))
                return $"Kilitli — önce {_progress.NameOf(node.Requires)} açılmalı.";

            var cost = _progress.NextCost(node);
            if (cost == null || cost.Count == 0) return "Bedelsiz.";

            string line = level == 0 ? "Açma bedeli: " : $"{level} → {level + 1} bedeli: ";
            for (int i = 0; i < cost.Count; i++)
                line += (i > 0 ? " · " : "") + $"{cost[i].amount} {Label(cost[i].type)}";

            if (_wallet != null && !_wallet.CanAfford(cost)) line += "\n(öz yetmiyor)";
            return line;
        }

        private void RefreshWallet()
        {
            if (_walletLabel == null || _wallet == null) return;
            _walletLabel.text = $"KESEDE  {_wallet.Get(EssenceType.Tas)} taş · " +
                                $"{_wallet.Get(EssenceType.Doga)} doğa";
        }

        private static string Label(EssenceType t) => t switch
        {
            EssenceType.Tas    => "taş",
            EssenceType.Doga   => "doğa",
            EssenceType.Ates   => "ateş",
            EssenceType.Su     => "su",
            EssenceType.Toprak => "toprak",
            _                  => t.ToString().ToLowerInvariant()
        };
    }
}
