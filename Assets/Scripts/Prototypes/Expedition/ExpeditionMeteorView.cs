using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionView
    {
        [SerializeField] private GameObject _meteorPrefab;
        private Transform[] _meteorBodies;
        private LineRenderer[] _meteorMarks,_meteorTrails;
        private void BindMeteorVisuals()
        {
            if(_config.Pressure==null||_meteorBodies!=null||_meteorPrefab==null)return;
            _meteorBodies=new Transform[5];_meteorMarks=new LineRenderer[5];_meteorTrails=new LineRenderer[5];
            for(int i=0;i<5;i++)
            {
                _meteorBodies[i]=Instantiate(_meteorPrefab,transform).transform;
                _meteorMarks[i]=WeaponLine(transform);_meteorTrails[i]=WeaponLine(transform);
                _meteorBodies[i].gameObject.SetActive(false);_meteorMarks[i].gameObject.SetActive(false);_meteorTrails[i].gameObject.SetActive(false);
            }
        }
        private void RefreshMeteorVisuals()
        {
            if(_meteorBodies==null)return;
            var s=_simulation;var profile=_config.Pressure;
            for(int i=0;i<5;i++)
            {
                bool active=s.WardenVisible&&i<s.Meteors.Count;
                var body=_meteorBodies[i];var mark=_meteorMarks[i];var trail=_meteorTrails[i];
                body.gameObject.SetActive(false);mark.gameObject.SetActive(active);trail.gameObject.SetActive(false);
                if(!active)continue;
                var meteor=s.Meteors[i];TintWeapon(mark,meteor.Impacted?_wardenOpening:_wardenDanger);
                mark.startWidth=mark.endWidth=_weaponStroke*(meteor.Remaining<profile.MeteorFall?2:1);
                Circle(mark,meteor.Position,profile.MeteorRadius);
                if(meteor.Remaining>profile.MeteorFall||meteor.Impacted)continue;
                float t=1-Mathf.Clamp01(meteor.Remaining/profile.MeteorFall);
                var floor=Point(meteor.Position,.1f);var above=floor+Vector3.up*(profile.MeteorHeight*(1-t*t));
                body.gameObject.SetActive(true);body.position=above;body.rotation=Quaternion.Euler(s.Elapsed*150,i*71,s.Elapsed*80);
                trail.gameObject.SetActive(true);TintWeapon(trail,_wardenDanger);trail.startWidth=.28f;trail.endWidth=.04f;trail.positionCount=2;trail.SetPosition(0,above);trail.SetPosition(1,above+Vector3.up*1.7f);
            }
        }
    }
}
