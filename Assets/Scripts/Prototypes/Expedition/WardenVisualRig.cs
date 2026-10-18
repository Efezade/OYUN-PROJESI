using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed class WardenVisualRig : MonoBehaviour
    {
        [SerializeField] private Transform _bladePivot;
        [SerializeField] private Transform _torso;
        [SerializeField] private Renderer _core;
        [SerializeField] private Transform _leftLeg;
        [SerializeField] private Transform _rightLeg;
        [SerializeField] private Color _fire=new Color(1,.28f,.08f);
        [SerializeField] private Color _open=new Color(.2f,1,.7f);
        [SerializeField] private Color _shield=new Color(.7f,.35f,1);
        [SerializeField] private float _bladeWindup=65;
        private MaterialPropertyBlock _tint;
        public void Pose(Vector2 facing,WardenMove move,int stage,float windup,bool shielded,float clock)
        {
            if(facing.sqrMagnitude<.001f)facing=Vector2.up;
            transform.localRotation=Quaternion.LookRotation(new Vector3(facing.x,0,facing.y));
            bool tired=move==WardenMove.Exposed||move==WardenMove.Recovery;
            _torso.localRotation=Quaternion.Euler(tired?14:move==WardenMove.Meteor?-20:stage==3?-8:0,0,0);
            float stride=(move==WardenMove.Approach||move==WardenMove.Rush)?Mathf.Sin(clock*(stage+3))*20:0;
            if(_leftLeg!=null)_leftLeg.localRotation=Quaternion.Euler(stride,0,0);
            if(_rightLeg!=null)_rightLeg.localRotation=Quaternion.Euler(-stride,0,0);
            _bladePivot.localRotation=Quaternion.Slerp(_bladePivot.localRotation,Quaternion.Euler(move==WardenMove.Cleave?-_bladeWindup*(1-Mathf.Clamp01(windup)):tired?20:-15,move==WardenMove.Cleave?65*(1-Mathf.Clamp01(windup)):0,move==WardenMove.Rush?-50:20),1-Mathf.Exp(-Time.deltaTime*16));
            if(_tint==null)_tint=new MaterialPropertyBlock();_tint.SetColor("_BaseColor",move==WardenMove.Exposed?_open:shielded?_shield:_fire);_core.SetPropertyBlock(_tint);
        }
    }
}
