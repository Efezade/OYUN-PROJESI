using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionView
    {
        [SerializeField] private WardenVisualRig _wardenPrefab;
        [SerializeField] private Color _wardenDanger=new Color(1,.3f,.12f);
        [SerializeField] private Color _wardenOpening=new Color(.3f,1,.85f);
        private WardenVisualRig _wardenRig;
        private LineRenderer[] _wardenWarnings;
        private void BindWardenVisual(TacticalRPG.Prototypes.CombatLab.LabUnit u,TacticalRPG.Prototypes.CombatLab.CombatLabActorView actor)
        {
            if(u!=_simulation.Warden||_wardenPrefab==null||_wardenRig!=null)return;
            _wardenRig=Instantiate(_wardenPrefab,actor.transform);actor.ReplaceBody(_wardenRig.transform);
            BindMeteorVisuals();
            if(_wardenWarnings==null){_wardenWarnings=new LineRenderer[8];for(int i=0;i<8;i++)_wardenWarnings[i]=WeaponLine(transform);}
        }
        private void RefreshWardenVisual()
        {
            RefreshMeteorVisuals();
            if(_wardenWarnings==null)return;
            foreach(var line in _wardenWarnings)line.gameObject.SetActive(false);
            var s=_simulation;if(!s.WardenVisible||_wardenRig==null)return;
            var u=s.Warden;_wardenRig.Pose(s.WardenFacing,s.WardenAction,s.WardenStage,u.Windup/_config.WardenWarning,s.WardenAddCountForView>0,s.Elapsed);
            if(s.WardenExposed){var ring=_wardenWarnings[0];ring.gameObject.SetActive(true);TintWeapon(ring,_wardenOpening);Circle(ring,u.Position,1.5f);return;}
            if(!s.WardenBusy||s.WardenAction==WardenMove.Transition)return;
            if(s.WardenAction==WardenMove.Cleave)
            {
                var line=_wardenWarnings[0];line.gameObject.SetActive(true);TintWeapon(line,_wardenDanger);line.positionCount=26;
                line.SetPosition(0,Point(u.Position,.1f));float angle=Mathf.Atan2(s.WardenFacing.y,s.WardenFacing.x);
                for(int i=0;i<24;i++){float a=angle+Mathf.Lerp(-_config.WardenCleaveAngle,_config.WardenCleaveAngle,i/23f)*Mathf.Deg2Rad;line.SetPosition(i+1,Point(u.Position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*_config.WardenCleaveRange,.1f));}line.SetPosition(25,Point(u.Position,.1f));
            }
            else if(s.WardenAction==WardenMove.Rush)
            {var line=_wardenWarnings[1];line.gameObject.SetActive(true);TintWeapon(line,_wardenDanger);line.positionCount=2;line.startWidth=line.endWidth=_config.ChargeRadius*2;line.SetPosition(0,Point(u.Position,.07f));line.SetPosition(1,Point(u.Aim,.07f));}
            else if(s.WardenAction==WardenMove.Ring)
            {var line=_wardenWarnings[2];line.gameObject.SetActive(true);TintWeapon(line,_wardenDanger);line.startWidth=line.endWidth=_weaponStroke;Circle(line,s.WardenOrigin,s.WardenRingRadius);
                if(s.WardenExecuting)for(int i=0;i<2;i++){var edge=_wardenWarnings[6+i];edge.gameObject.SetActive(true);TintWeapon(edge,_wardenDanger);edge.startWidth=edge.endWidth=_weaponStroke*.6f;Circle(edge,s.WardenOrigin,Mathf.Max(.01f,s.WardenRingRadius+(i==0?-1:1)*_config.WardenRingWidth));}}
            else if(s.WardenAction==WardenMove.Slam)
            {for(int i=s.WardenSlamIndex;i<s.WardenSlams.Count;i++){var line=_wardenWarnings[3+i];line.gameObject.SetActive(true);TintWeapon(line,_wardenDanger);Circle(line,s.WardenSlams[i],_config.WardenSlamRadius);}}
        }
    }
    public sealed partial class ExpeditionHud
    {
        [SerializeField] private GameObject _wardenPanel;
        [SerializeField] private Text _wardenLabel;
        [SerializeField] private Image _wardenFill;
        private void RefreshWardenHud(ExpeditionSimulation s)
        {
            if(_wardenPanel==null)return;_wardenPanel.SetActive(s.WardenVisible);if(!s.WardenVisible)return;
            _wardenLabel.text="KÜL YEMİNLİ MUHAFIZ · AŞAMA "+s.WardenStage+" / 3 · "+s.Warden.Health+" / "+s.Warden.MaxHealth+"\n"+s.WardenInstruction;
            _wardenFill.rectTransform.pivot=new Vector2(0,.5f);_wardenFill.rectTransform.anchoredPosition=new Vector2(-_wardenFill.rectTransform.rect.width*.5f,0);
            _wardenFill.fillAmount=(float)s.Warden.Health/s.Warden.MaxHealth;_wardenFill.rectTransform.localScale=new Vector3(_wardenFill.fillAmount,1,1);
        }
    }
}
