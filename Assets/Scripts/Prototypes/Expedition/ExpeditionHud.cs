using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionHud : MonoBehaviour
    {
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _game;
        [SerializeField] private GameObject _choices;
        [SerializeField] private GameObject _camp;
        [SerializeField] private GameObject _result;
        [SerializeField] private Text _status;
        [SerializeField] private Text _build;
        [SerializeField] private Text _objective;
        [SerializeField] private Text _ground;
        [SerializeField] private Text _hint;
        [SerializeField] private Text _skills;
        [SerializeField] private Text _choiceTitle;
        [SerializeField] private Text[] _choiceLabels;
        [SerializeField] private Text _resultLabel;
        [SerializeField] private Text _campLabel;
        [SerializeField] private Text _saveLabel;
        [SerializeField] private Button _explore;
        [SerializeField] private Button _newRun;
        [SerializeField] private Button _load;
        [SerializeField] private Button _quit;
        [SerializeField] private Button _resume;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _pause;
        [SerializeField] private Button _pull;
        [SerializeField] private Button _burst;
        [SerializeField] private Button _dash;
        [SerializeField] private Button[] _cards;
        [SerializeField] private Button _continue;
        [SerializeField] private Button _retry;
        [SerializeField] private Button _sameSeed;
        [SerializeField] private Button _anotherSeed;
        private ExpeditionController _controller;
        private ExpeditionConfig _config;
        private readonly StringBuilder _text=new StringBuilder(600);
        private Text _pullLabel,_burstLabel;
        public bool MenuVisible=>_menu.activeSelf;
        public void Bind(ExpeditionController controller,ExpeditionConfig config)
        {
            _controller=controller;_config=config;
            if(_explore!=null)_explore.onClick.AddListener(controller.Explore);
            _pullLabel=_pull.GetComponentInChildren<Text>();_burstLabel=_burst.GetComponentInChildren<Text>();
            _newRun.onClick.AddListener(controller.NewRun);_load.onClick.AddListener(controller.Load);_quit.onClick.AddListener(controller.Quit);
            _resume.onClick.AddListener(controller.Resume);
            _menuButton.onClick.AddListener(controller.Menu);_pause.onClick.AddListener(controller.Pause);
            _pull.onClick.AddListener(controller.AimPull);_burst.onClick.AddListener(controller.Burst);_dash.onClick.AddListener(controller.Dash);
            for(int i=0;i<_cards.Length;i++){int n=i;_cards[i].onClick.AddListener(()=>controller.Choose(n));}
            _continue.onClick.AddListener(controller.Continue);_retry.onClick.AddListener(controller.Retry);
            _sameSeed.onClick.AddListener(controller.SameSeed);_anotherSeed.onClick.AddListener(controller.NewRun);
        }
        public void ShowMenu(){_menu.SetActive(true);_game.SetActive(false);_choices.SetActive(false);_camp.SetActive(false);_result.SetActive(false);Refresh();}
        public void ShowGame(){_menu.SetActive(false);_game.SetActive(true);Refresh();}
        public void Refresh()
        {
            if(_controller==null)return;
            _load.interactable=_controller.CanLoad;
            _resume.interactable=_controller.Simulation!=null&&_controller.Simulation.Phase!=ExpeditionPhase.Ready;
            _saveLabel.text=_controller.CanLoad?"Mabet kaydı bulundu. Son temizlenen alanın ardından devam edebilirsin.":"Henüz mabet kaydı yok. İlk alanı bitirdiğinde otomatik kaydedilir.";
            var s=_controller.Simulation;if(s==null||s.Phase==ExpeditionPhase.Ready||MenuVisible)return;
            if(s.IsJourney){RefreshJourney(s);return;}
            _status.text="KAM  "+s.Kam.Health+" / "+s.Kam.MaxHealth+"     KALKAN "+Mathf.CeilToInt(s.Shield)+"     MANA "+Mathf.FloorToInt(s.Mana)+" / "+_config.Mana+"\nALAN "+(s.Stage+1)+" / 3  ·  "+s.StageName+"     TOHUM "+s.Seed;
            _text.Clear();_text.Append("KOMBİNASYONUN\nAlanlar arasında korunur\n\n");
            for(int i=0;i<7;i++)if(s.Levels[i]>0)_text.Append(ExpeditionPowers.Name(i)).Append("  ").Append(s.Levels[i]).Append("/2\n");
            if(s.Picks==0)_text.Append("İlk güç yaklaşıyor…\n");
            _text.Append("\nGelişim ").Append(s.XP);if(s.NextThreshold>0)_text.Append(" / ").Append(s.NextThreshold);else _text.Append(" · TAMAM");
            _text.Append("\n").Append(s.Chains).Append(" zincir\n\nMuhafız ").Append(s.Units[1].Health).Append(" / ").Append(s.Units[1].MaxHealth).Append("\nOkçu ").Append(s.Units[2].Health).Append(" / ").Append(s.Units[2].MaxHealth);
            _build.text=_text.ToString();
            _objective.text=s.Phase==ExpeditionPhase.Travel?"DOĞUYA İLERLE →\nSonraki mücadele\nGüçler korunuyor":s.ModifierName+"\nDALGA "+s.Wave+" / 3  ·  "+s.LivingEnemies+" düşman\n"+s.Pending.Count+" yaklaşan · "+Mathf.FloorToInt(s.Elapsed)+" sn";
            _ground.text=s.Map.Description(s.Kam.Position);
            _hint.text=s.Paused?"DURAKLATILDI · ESC ile devam et":_controller.Aiming?"ÇEKİM · görünür ve yakın zemine tıkla. Sağ tık / ESC iptal.":s.Hint;
            _skills.text="Q ÇEKİM  "+s.PullCooldown.ToString("0.0")+" sn / 3 mana     E PATLAMA  "+s.BurstCooldown.ToString("0.0")+" sn / 4 mana     SPACE KAÇIŞ  "+s.DashCooldown.ToString("0.0")+" sn";
            bool can=s.Phase==ExpeditionPhase.Fighting&&!s.Paused;
            _pull.interactable=can&&s.PullCooldown<=0&&s.Mana>=3;_burst.interactable=can&&s.BurstCooldown<=0&&s.Mana>=4;_dash.interactable=s.MovingAllowed&&s.DashCooldown<=0;
            _choices.SetActive(s.Phase==ExpeditionPhase.Choosing);
            if(s.Phase==ExpeditionPhase.Choosing)
            {
                _choiceTitle.text="YÖNÜNÜ SEÇ · SAVAŞ DURDU\nBu güç sonraki alanlarda da seninle.";
                for(int i=0;i<3;i++){int p=s.Offers[i];_choiceLabels[i].text=(i+1)+" · "+ExpeditionPowers.Name(p)+"\nSEVİYE "+(s.Levels[p]+1)+" / 2\n\n"+ExpeditionPowers.Description(p);}
            }
            _camp.SetActive(s.Phase==ExpeditionPhase.Camp);
            if(s.Phase==ExpeditionPhase.Camp)_campLabel.text="MABETE ULAŞTIN\n\n"+s.StageName+" temizlendi.\nEkip ve mana yenilendi. "+s.Picks+" güç seçimin korundu.\n\n"+_controller.SaveStatus+"\nDevam et, sonra doğu geçidinden sonraki alana yürü.";
            bool ended=s.Phase==ExpeditionPhase.Won||s.Phase==ExpeditionPhase.Lost;_result.SetActive(ended);_retry.gameObject.SetActive(s.Phase==ExpeditionPhase.Lost);
            if(ended)_resultLabel.text=(s.Phase==ExpeditionPhase.Won?"SEFER TAMAMLANDI":"KAM DÜŞTÜ")+"\n\nTohum "+s.Seed+" · "+Mathf.FloorToInt(s.Elapsed)+" sn · "+s.Chains+" zincir\n"+s.Picks+" güç seçimi · "+s.Deaths+" mabet dönüşü\n\n"+(s.Phase==ExpeditionPhase.Lost?"Mabede dön: bu alanı yeniden dene.\nGüçlerin korunur; aynı düşman tekrar gelişim vermez.":"Aynı tohumu başka seçimlerle veya yeni bir tohumu dene.\nSence hangi arazi ve güç birleşimi işe yaradı?");
        }
    }
}
