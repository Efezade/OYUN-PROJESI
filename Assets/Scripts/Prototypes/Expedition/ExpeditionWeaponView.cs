using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionView
    {
        [SerializeField] private Color _crescentColor=new Color(1,.72f,.18f);
        [SerializeField] private Color _spearColor=new Color(.25f,.8f,1);
        [SerializeField] private Color _discColor=new Color(.9f,.45f,1);
        [SerializeField] private float _weaponStroke=.12f;
        private LineRenderer[] _anchorViews,_bladeViews;
        private LineRenderer _heldWeapon;
        private MaterialPropertyBlock _weaponTint;
        private void TintWeapon(LineRenderer line,Color color)
        {if(_weaponTint==null)_weaponTint=new MaterialPropertyBlock();_weaponTint.Clear();_weaponTint.SetColor("_BaseColor",color);line.SetPropertyBlock(_weaponTint);}
        private LineRenderer WeaponLine(Transform parent)
        {var line=Instantiate(_linePrefab,parent);line.positionCount=2;line.startWidth=line.endWidth=_weaponStroke;line.gameObject.SetActive(false);return line;}
        private void PrepareWeaponVisuals()
        {
            if(!_simulation.IsJourney||_heldWeapon!=null)return;
            _heldWeapon=WeaponLine(transform);_anchorViews=new LineRenderer[4];_bladeViews=new LineRenderer[2];
            for(int i=0;i<4;i++)_anchorViews[i]=WeaponLine(transform);
            for(int i=0;i<2;i++)_bladeViews[i]=WeaponLine(transform);
        }
        private void RefreshWeaponVisuals()
        {
            if(_heldWeapon==null)return;
            var s=_simulation;bool active=s.IsJourney&&s.Phase!=ExpeditionPhase.WeaponChoice;
            _heldWeapon.gameObject.SetActive(active&&s.Kam.Alive);
            var color=s.Weapon==JourneyWeapon.Crescent?_crescentColor:_spearColor;
            _heldWeapon.startColor=_heldWeapon.endColor=color;
            TintWeapon(_heldWeapon,color);
            _heldWeapon.SetPosition(0,Point(s.Kam.Position,.65f));_heldWeapon.SetPosition(1,Point(s.Kam.Position+s.Facing*(s.Weapon==JourneyWeapon.Crescent?1.2f:1.9f),.85f));
            for(int i=0;i<4;i++)
            {
                bool show=active&&i<s.Anchors.Count;var line=_anchorViews[i];line.gameObject.SetActive(show);if(!show)continue;
                var p=s.Anchors[i].Position;line.startColor=line.endColor=_spearColor;TintWeapon(line,_spearColor);line.SetPosition(0,Point(p,.06f));line.SetPosition(1,Point(p+Vector2.right*.3f,1.2f));
            }
            for(int i=0;i<2;i++)
            {
                var line=_bladeViews[i];line.gameObject.SetActive(active&&s.Kam.Alive&&s.HasPower(1));
                var axis=new Vector2(Mathf.Cos(s.BladeAngle+i*Mathf.PI),Mathf.Sin(s.BladeAngle+i*Mathf.PI));
                var p=s.Kam.Position+axis*2;var tangent=new Vector2(-axis.y,axis.x)*.5f;
                line.startColor=line.endColor=_crescentColor;TintWeapon(line,_crescentColor);line.SetPosition(0,Point(p-tangent,.65f));line.SetPosition(1,Point(p+tangent,.65f));
            }
        }
        private void DrawWeaponEffect(Fx f,LabFx e)
        {
            f.Line.positionCount=2;f.Line.startWidth=f.Line.endWidth=_weaponStroke;
            if(!_simulation.IsJourney)return;
            if(e.Kind==0){f.Line.gameObject.SetActive(false);return;}
            if(e.Kind<10)return;
            f.Pulse.SetActive(e.Kind==13);f.Line.gameObject.SetActive(e.Kind!=13);
            var color=e.Kind==10?_crescentColor:e.Kind==14?_discColor:_spearColor;
            f.Line.startColor=f.Line.endColor=color;
            TintWeapon(f.Line,color);
            if(e.Kind==10)
            {
                var axis=e.To-e.From;float angle=Mathf.Atan2(axis.y,axis.x);f.Line.positionCount=19;
                for(int i=0;i<19;i++){float a=angle+Mathf.Lerp(-1.2f,1.2f,i/18f);f.Line.SetPosition(i,Point(e.From+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*axis.magnitude,.5f));}
            }
            else {f.Line.SetPosition(0,Point(e.From,.6f));f.Line.SetPosition(1,Point(e.To,.6f));}
        }
    }
}
