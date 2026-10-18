using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.Prototypes.CombatLab
{
    public sealed class CombatLabHud : MonoBehaviour
    {
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _game;
        [SerializeField] private GameObject _choices;
        [SerializeField] private GameObject _result;
        [SerializeField] private Text _title;
        [SerializeField] private Text _status;
        [SerializeField] private Text _roster;
        [SerializeField] private Text _controls;
        [SerializeField] private Text _hint;
        [SerializeField] private Text _comparison;
        [SerializeField] private Text _resultText;
        [SerializeField] private Text _pauseText;
        [SerializeField] private Text _pullLabel;
        [SerializeField] private Text _burstLabel;
        [SerializeField] private Button[] _modeButtons;
        [SerializeField] private Button[] _unitButtons;
        [SerializeField] private Button[] _augmentButtons;
        [SerializeField] private Button[] _ratingButtons;
        [SerializeField] private Button _pull;
        [SerializeField] private Button _burst;
        [SerializeField] private Button _endTurn;
        [SerializeField] private Button _pause;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _otherMode;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _resultOther;
        [SerializeField] private Button _resultRestart;
        private CombatLabController _controller;
        private CombatLabConfig _config;
        public bool MenuVisible => _menu.activeSelf;
        public void Bind(CombatLabController controller, CombatLabConfig config)
        {
            _controller = controller; _config = config;
            for (int i = 0; i < _modeButtons.Length; i++) { int index = i; _modeButtons[i].onClick.AddListener(() => controller.StartMode(index)); }
            for (int i = 0; i < _unitButtons.Length; i++) { int index = i; _unitButtons[i].onClick.AddListener(() => controller.Select(index)); }
            for (int i = 0; i < _augmentButtons.Length; i++) { int index = i; _augmentButtons[i].onClick.AddListener(() => controller.PickAugment(index)); }
            for (int i = 0; i < _ratingButtons.Length; i++) { int index = i + 1; _ratingButtons[i].onClick.AddListener(() => controller.Rate(index)); }
            _pull.onClick.AddListener(controller.Pull); _burst.onClick.AddListener(controller.Burst);
            _endTurn.onClick.AddListener(controller.EndTurn); _pause.onClick.AddListener(controller.Pause);
            _restart.onClick.AddListener(controller.Restart); _otherMode.onClick.AddListener(controller.OtherMode);
            _menuButton.onClick.AddListener(controller.BackToMenu); _resultOther.onClick.AddListener(controller.OtherMode); _resultRestart.onClick.AddListener(controller.Restart);
        }
        public void ShowMenu() { _menu.SetActive(true); _game.SetActive(false); _choices.SetActive(false); _result.SetActive(false); }
        public void ShowGame() { _menu.SetActive(false); _game.SetActive(true); }
        public void Refresh()
        {
            if (_controller == null) return;
            var s = _controller.Simulation;
            _comparison.text = "SON DENEMELER\n\nA / HIZLI TAKTİK\n" + _controller.LastTactical + "\n\nB / GERÇEK ZAMAN\n" + _controller.LastRealtime + "\n\nSüre tek başına kalite ölçüsü değil.\nÖnce A→B, sonra B→A dene.";
            if (s == null || s.Phase == LabPhase.Ready || MenuVisible) return;
            bool realtime = s.Mode == LabMode.Realtime;
            _title.text = realtime ? "B / GERÇEK ZAMAN" : "A / HIZLI TAKTİK";
            _status.text = "DALGA " + s.Wave + "/3   ·   " + (realtime ? "AKTİF SAVAŞ" : "TUR " + s.Round) + "\nMANA " + Mathf.FloorToInt(s.Mana) + "/" + _config.Mana + "   ·   " + s.LivingEnemies + " DÜŞMAN\n" + s.Chains + " ZİNCİR   ·   " + Mathf.RoundToInt(s.Elapsed) + " sn";
            _roster.text = "EKİBİN\n\n";
            for (int i = 0; i < 3; i++)
            {
                var u = s.Units[i];
                _roster.text += (s.SelectedId == i ? "› " : "  ") + u.Name + "  " + u.Health + "/" + u.MaxHealth + "\n";
                if (!realtime) _roster.text += "  " + (u.Alive ? (u.Moved ? "Hareket bitti" : "Hareket hazır") + " · " + (u.Acted ? "Eylem bitti" : "Eylem hazır") : "Düştü") + "\n";
                _unitButtons[i].interactable = !realtime && u.Alive && s.Phase == LabPhase.Player && !s.Paused;
            }
            _controls.text = realtime
                ? "WASD / OKLAR\nKam'ı hareket ettir\n\nSOL TIK\nEkibe düşman hedefi ver\n\nQ · İmlece çek ve işaretle\nE · Kam çevresinde patlat\n\nNormal saldırılar otomatik.\nKırmızı alanlardan çık.\nESC / SPACE · Duraklat"
                : "1 / 2 / 3 · Birlik seç\n\nYEŞİL KAROYA TIKLA\nBir kez hareket et\n\nDÜŞMANA TIKLA\nBir kez saldır\n\nQ · Çekim hedefi seç\nE · Kam çevresinde patlat\n\nSPACE · Düşman turu\nESC · Duraklat / iptal";
            _hint.text = s.Paused ? "DURAKLATILDI — devam etmek için ESC veya Devam düğmesi." : _controller.AimingPull ? "ÇEKİM HEDEFİ: arenada Kam'a yakın bir yere tıkla. Sağ tık / ESC: iptal." : s.Hint;
            _pauseText.text = s.Paused ? "Devam" : "Duraklat";
            _pullLabel.text = "Q  ÇEK + İŞARETLE\n" + _config.PullCost + " mana" + (realtime && s.PullCooldown > 0 ? " · " + s.PullCooldown.ToString("0.0") + " sn" : "");
            _burstLabel.text = "E  PATLAT\n" + _config.BurstCost + " mana" + (realtime && s.BurstCooldown > 0 ? " · " + s.BurstCooldown.ToString("0.0") + " sn" : "");
            bool can = s.Phase == LabPhase.Player && !s.Paused && (realtime || !s.Kam.Acted);
            _pull.interactable = can && s.Mana >= _config.PullCost && (!realtime || s.PullCooldown <= 0);
            _burst.interactable = can && s.Mana >= _config.BurstCost && (!realtime || s.BurstCooldown <= 0);
            _endTurn.gameObject.SetActive(!realtime); _endTurn.interactable = s.Phase == LabPhase.Player && !s.Paused;
            _choices.SetActive(s.Phase == LabPhase.Augment);
            bool done = s.Phase == LabPhase.Won || s.Phase == LabPhase.Lost; _result.SetActive(done);
            if (done) _resultText.text = (s.Phase == LabPhase.Won ? "KAPI AÇILDI" : "KAM DÜŞTÜ") + "\n\n" + s.Kills + " düşman · " + s.Chains + " zincir\n" + Mathf.RoundToInt(s.Elapsed) + " sn · " + s.Commands + " komut\n\nYeniden oynama isteğin?  1: düşük — 5: yüksek\n\nA: " + _controller.LastTactical + "\n\nB: " + _controller.LastRealtime;
        }
    }
}
