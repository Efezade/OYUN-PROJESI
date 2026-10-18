using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    [CreateAssetMenu(menuName="TacticalRPG/Prototypes/Hybrid battle layout")]
    public sealed class HybridBattleLayout : ScriptableObject
    {
        [SerializeField] private GameObject _groundPrefab;
        [SerializeField] private GameObject _gatePrefab;
        [SerializeField] private Material _bridgeMaterial;
        [SerializeField] private Material _trimMaterial;
        [SerializeField] private Vector3Int _arenaRadii=new Vector3Int(5,5,6);
        [SerializeField] private Vector3 _levels=new Vector3(.3f,.9f,1.5f);
        [SerializeField] private int _stageSpacing=16;
        [SerializeField] private float _bridgeHalfWidth=2.1f;
        [SerializeField] private Vector2 _gatePosition=new Vector2(-5.2f,0);
        [SerializeField] private Vector2[] _coverOffsets={new Vector2(-1,-3),new Vector2(-1,2.8f),new Vector2(3,4)};
        public GameObject GroundPrefab=>_groundPrefab;
        public GameObject GatePrefab=>_gatePrefab;
        public Material BridgeMaterial=>_bridgeMaterial;
        public Material TrimMaterial=>_trimMaterial;
        public int Spacing=>_stageSpacing;
        public int Radius(int stage)=>_arenaRadii[stage];
        public float Level(int stage)=>_levels[stage];
        public float HalfWidth=>_bridgeHalfWidth;
        public Vector2 GatePosition=>_gatePosition;
        public Vector2[] CoverOffsets=>_coverOffsets;
        public float CenterX(int stage)=>stage*_stageSpacing*Mathf.Sqrt(3);
        public int Region(Vector2 p)=>Mathf.Clamp(Mathf.RoundToInt(p.x/CenterX(1)),0,2);
        public float BridgeStart(int stage)=>CenterX(stage)+(Radius(stage)-1)*Mathf.Sqrt(3);
        public float BridgeEnd(int stage)=>CenterX(stage+1)-(Radius(stage+1)-1)*Mathf.Sqrt(3);
        public float RiseStart(int stage)=>CenterX(stage)+(Radius(stage)+.6f)*Mathf.Sqrt(3);
        public float RiseEnd(int stage)=>CenterX(stage+1)-(Radius(stage+1)+.6f)*Mathf.Sqrt(3);
        public bool OnBridge(Vector2 p,int stage)=>p.x>=BridgeStart(stage)&&p.x<=BridgeEnd(stage)&&Mathf.Abs(p.y)<=_bridgeHalfWidth;
        public float Height(Vector2 p)
        {
            for(int i=0;i<2;i++)if(OnBridge(p,i))return Mathf.Lerp(Level(i),Level(i+1),Mathf.InverseLerp(RiseStart(i),RiseEnd(i),p.x));
            return Level(Region(p));
        }
        public bool IsSlope(Vector2 p)
        {for(int i=0;i<2;i++)if(OnBridge(p,i)&&p.x>RiseStart(i)&&p.x<RiseEnd(i))return true;return false;}
        // Footprints match the supplied gate feet, rotated 90 degrees, with 15 cm clearance.
        public bool GateBlocks(Vector2 p)
        {
            var v=p-_gatePosition;
            return Mathf.Abs(v.x)<.565f&&(Mathf.Abs(v.y-1.12f)<.8f||Mathf.Abs(v.y+1.25f)<.6f);
        }
        public string StageName(int stage)=>stage==0?"SARMAŞIKLI KAPI / ALT AVLU":stage==1?"KÖPRÜ / ORTA TERAS":"YÜKSEK AVLU / MUHAFIZ";
    }
}
