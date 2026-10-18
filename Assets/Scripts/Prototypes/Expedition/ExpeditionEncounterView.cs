using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionView
    {
        [SerializeField] private Color _flankerColor=new Color(1,.4f,.7f);
        [SerializeField] private Color _bombColor=new Color(1,.35f,.12f);
        [SerializeField] private float _roleMarkerSize=.45f;
        [SerializeField] private Color _eliteColor=new Color(1,.85f,.2f);
        [SerializeField] private float _eliteScale=1.2f;
        private readonly Dictionary<int,LineRenderer> _roleMarkers=new Dictionary<int,LineRenderer>();
        private readonly Dictionary<int,LineRenderer> _bombWarnings=new Dictionary<int,LineRenderer>();
        private LineRenderer[] _hazardViews;
        private void PrepareEncounterVisuals()
        {
            if(!_simulation.IsJourney||_hazardViews!=null)return;_hazardViews=new LineRenderer[2];
            for(int i=0;i<2;i++){_hazardViews[i]=WeaponLine(transform);TintWeapon(_hazardViews[i],_bombColor);}
        }
        private void ClearEnemyVisuals(){_roleMarkers.Clear();foreach(var line in _bombWarnings.Values)Destroy(line.gameObject);_bombWarnings.Clear();}
        private void AddEnemyVisual(LabUnit u,Transform actor)
        {
            if(!_simulation.IsJourney||u.Ally||_roleMarkers.ContainsKey(u.Id))return;
            var tactic=_simulation.TacticOf(u);if(!_simulation.IsElite(u)&&tactic!=EnemyTactic.Flanker&&tactic!=EnemyTactic.Sniper&&tactic!=EnemyTactic.Bomber)return;
            if(_simulation.IsElite(u))actor.localScale*=_eliteScale;
            var marker=WeaponLine(actor);_roleMarkers.Add(u.Id,marker);TintWeapon(marker,_simulation.IsElite(u)?_eliteColor:tactic==EnemyTactic.Flanker?_flankerColor:tactic==EnemyTactic.Bomber?_bombColor:_spearColor);
            if(tactic==EnemyTactic.Bomber){var warning=WeaponLine(transform);TintWeapon(warning,_bombColor);_bombWarnings.Add(u.Id,warning);}
        }
        private void Circle(LineRenderer line,Vector2 p,float radius)
        {
            line.positionCount=33;
            for(int i=0;i<33;i++){float a=i*Mathf.PI*2/32;line.SetPosition(i,Point(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,.09f));}
        }
        private void RefreshEncounterVisuals()
        {
            if(_hazardViews==null)return;
            foreach(var pair in _roleMarkers)
            {
                var u=_simulation.Find(pair.Key);bool show=u!=null&&u.Alive;var line=pair.Value;line.gameObject.SetActive(show);if(!show)continue;
                var c=Point(u.Position,2.9f);var right=_camera.transform.right*_roleMarkerSize;var up=_camera.transform.up*_roleMarkerSize;
                var tactic=_simulation.TacticOf(u);bool diamond=tactic==EnemyTactic.Bomber||_simulation.IsElite(u);line.positionCount=diamond?5:3;
                line.SetPosition(0,c-right);line.SetPosition(1,c+up);line.SetPosition(2,c+right);
                if(diamond){line.SetPosition(3,c-up);line.SetPosition(4,c-right);}
            }
            foreach(var pair in _bombWarnings)
            {
                var u=_simulation.Find(pair.Key);bool show=u!=null&&u.Alive&&u.Windup>0;pair.Value.gameObject.SetActive(show);if(show)Circle(pair.Value,u.Aim,_simulation.HazardRadius);
            }
            for(int i=0;i<2;i++)
            {
                bool show=i<_simulation.Hazards.Count&&_simulation.Phase==ExpeditionPhase.Fighting;_hazardViews[i].gameObject.SetActive(show);
                if(show){var h=_simulation.Hazards[i];Circle(_hazardViews[i],h.Position,_simulation.HazardRadius);_hazardViews[i].startWidth=_hazardViews[i].endWidth=_weaponStroke*2;}
            }
        }
    }
}
