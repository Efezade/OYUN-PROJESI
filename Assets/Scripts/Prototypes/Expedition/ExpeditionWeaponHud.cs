using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionHud
    {
        [SerializeField] private Vector2 _weaponCardSize=new Vector2(440,260);
        [SerializeField] private Vector2 _powerCardSize=new Vector2(325,260);
        [SerializeField] private float _weaponCardSpacing=240;
        [SerializeField] private float _powerCardSpacing=345;
        private void RefreshJourney(ExpeditionSimulation s)
        {
            _status.text="KAM  "+s.Kam.Health+" / "+s.Kam.MaxHealth+"    KALKAN "+Mathf.CeilToInt(s.Shield)+"    VURUŞ GÜCÜ "+s.Charge+" / "+s.ChargeRequired+"\nGÖREV "+(s.Stage+1)+" / 3  ·  "+s.WeaponName+"    TOHUM "+s.Seed;
            _text.Clear();_text.Append("YOLCULUĞUN\nGörevler arasında korunur\n\n").Append(s.WeaponName).Append("\n\n");
            for(int i=0;i<9;i++)if(s.HasPower(i))_text.Append("+ ").Append(s.PowerName(i)).Append("\n");
            _text.Append("\nGelişim ").Append(s.XP);if(s.NextThreshold>0)_text.Append(" / ").Append(s.NextThreshold);else _text.Append(" · TAMAM");
            _text.Append("\n").Append(s.Picks).Append(" yeni davranış\n\n").Append(s.Weapon==JourneyWeapon.Crescent?"Yaklaş · ön yayda biç\nÜç isabetle E hazır":"Hizala · del · yer değiştir\nQ: mızrakları geri çağır");_build.text=_text.ToString()+(s.IsMastery?"\n\n"+s.MasterySummary:"");
            _objective.text=s.Phase==ExpeditionPhase.Travel?"DOĞUYA İLERLE →\nSonraki görev\nSilah ve güçler korunuyor":"GÖREV "+(s.Stage+1)+" · "+s.StageName+"\nDALGA "+s.Wave+" / 3\n"+s.LivingEnemies+" düşman · "+s.Pending.Count+" yaklaşan";
            if(s.Phase==ExpeditionPhase.Fighting&&s.IsMastery)_objective.text+=s.Stage==0?"\nSıçrayan: iniş halkasından çık":s.Stage==1?"\nHiddetli: yorulunca saldır":"\nYansıtıcı: asıl düşmanı avla";
            else if(s.Phase==ExpeditionPhase.Fighting)_objective.text+=s.Stage==0?"\nAvcı: atış çizgisinden çık":"\nKuşatmacı: halkayı terk et";
            _ground.text=s.Map.Description(s.Kam.Position);
            if(s.Map.Ground(s.Kam.Position)==GroundKind.Water)_ground.text="SU · düşman nişancısının elektriği +2 hasar verir";
            if(s.Map.Ground(s.Kam.Position)==GroundKind.High)_ground.text="YÜKSELTİ · uzak saldırılar +1,4 menzil kazanır; yakın hilal değişmez";
            _hint.text=s.Paused?"DURAKLATILDI · ESC ile devam et":s.Hint;
            _skills.text="Q "+s.QName+" · "+s.PullCooldown.ToString("0.0")+" sn"+(s.Weapon==JourneyWeapon.Spear?" · "+s.Anchors.Count+" mızrak":"")+"     E "+s.EName+" · "+s.Charge+" / "+s.ChargeRequired+" isabet     SPACE "+s.DashCooldown.ToString("0.0")+" sn";
            _pullLabel.text="Q · "+(s.Weapon==JourneyWeapon.Crescent?"Atıl":"Geri çağır");_burstLabel.text="E · "+(s.CanWeaponE?"HAZIR":s.Charge+" / "+s.ChargeRequired);
            _pull.interactable=s.CanWeaponQ;_burst.interactable=s.CanWeaponE;_dash.interactable=s.MovingAllowed&&s.DashCooldown<=0;
            bool starting=s.Phase==ExpeditionPhase.WeaponChoice;bool choosing=s.Phase==ExpeditionPhase.Choosing;
            _choices.SetActive(starting||choosing);
            if(starting||choosing)
            {
                _choiceTitle.text=starting?"YOLCULUĞUN BAŞLANGICINI SEÇ\nSilahın üç görev boyunca seninle.":"YENİ BİR DAVRANIŞ KAZAN\nBu seçim sonraki görevlerde de korunur.";
                for(int i=0;i<3;i++)
                {
                    int p=s.Offers[i];_cards[i].gameObject.SetActive(p>=0);if(p<0)continue;
                    var rect=(RectTransform)_cards[i].transform;rect.sizeDelta=starting?_weaponCardSize:_powerCardSize;
                    rect.anchoredPosition=new Vector2(starting?(i==0?-_weaponCardSpacing:_weaponCardSpacing):(i-1)*_powerCardSpacing,-30);
                    _choiceLabels[i].rectTransform.sizeDelta=rect.sizeDelta-new Vector2(20,12);_choiceLabels[i].fontSize=17;
                    _choiceLabels[i].text=starting?(i+1)+" · "+(p==0?"HİLAL DARBESİ\nYAKIN OYNA\n\nÖnündeki düşmanları otomatik biç. Q ile atıl. Üç isabetten sonra E ile çevreni yar.":"DELİCİ MIZRAK\nUZAK OYNA\n\nDüşmanları hizala ve otomatik del. Yer değiştirip Q ile mızrakları geri çağır. E ile mesafe aç."):(i+1)+" · "+s.PowerName(p)+"\nYENİ YETENEK / DÖNÜŞÜM\n\n"+s.PowerDescription(p);
                }
            }
            _camp.SetActive(s.Phase==ExpeditionPhase.Camp);
            if(s.Phase==ExpeditionPhase.Camp)_campLabel.text="GÖREV "+(s.Stage+1)+" TAMAMLANDI\n\n"+s.WeaponName+" ve "+s.Picks+" yetenek seçimin korundu.\nCanın yenilendi; gelişim çubuğun devam ediyor.\n\n"+_controller.SaveStatus+"\nENTER, ardından doğu geçidine yürü.";
            if(s.IsMastery&&s.Phase==ExpeditionPhase.Camp)_campLabel.text="GÖREV "+(s.Stage+1)+" TAMAMLANDI\n\nUSTALIK "+(s.Stage+1)+" / 2 KAZANILDI\nSonraki görevde daha güçlü ve hızlı saldıracaksın.\n"+(s.Stage==1?(s.Weapon==JourneyWeapon.Crescent?"ÜÇLÜ YARIK: her üçüncü isabette üç dalga.\nÜçüncü Darbe kartın varsa iki isabette bir.":"MIZRAK YAYLIMI: üç hat, sekiz saplı mızrak.\nÇifte Mızrak kartın varsa dört hat."):"Silahın ve tüm kartların korunur.")+"\n\n"+_controller.SaveStatus+"\nENTER, ardından doğu geçidine yürü.";
            bool ended=s.Phase==ExpeditionPhase.Won||s.Phase==ExpeditionPhase.Lost;_result.SetActive(ended);_retry.gameObject.SetActive(s.Phase==ExpeditionPhase.Lost);
            if(ended)_resultLabel.text=(s.Phase==ExpeditionPhase.Won?"ÜÇ GÖREV TAMAMLANDI":"KAM DÜŞTÜ")+"\n\n"+s.WeaponName+" · "+s.Picks+" yeni davranış\n"+Mathf.FloorToInt(s.Elapsed)+" sn · "+s.Deaths+" dönüş\n\n"+(s.Phase==ExpeditionPhase.Lost?"Mabede dön: aynı görev, aynı silah ve güçler.\nAynı düşmandan tekrar gelişim kazanılmaz.":"Aynı tohumu diğer silahla dene.\nMesafen ve hareket rotan değişti mi?");
        }
    }
}
