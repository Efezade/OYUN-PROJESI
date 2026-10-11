using UnityEngine;

namespace TacticalRPG.Data
{
    [CreateAssetMenu(fileName="BiomeTerrainStyle",menuName="TacticalRPG/Biome Terrain Style")]
    public sealed class BiomeTerrainStyleSO : ScriptableObject
    {
        [System.Serializable] private struct Theme
        {
            public string id;
            public Color ground, foliage;
            public Theme(string key,Color soil,Color leaves){id=key;ground=soil;foliage=leaves;}
        }
        [SerializeField] private Shader _shader;
        [SerializeField,Range(.15f,1.5f)] private float _blendWidth=.65f;
        [SerializeField,Range(0,1)] private float _groundStrength=.94f;
        [SerializeField,Range(0,1)] private float _foliageStrength=.72f;
        [SerializeField,Range(0,1)] private float _stoneStrength=.42f;
        [SerializeField] private Color _stoneTone=new(.35f,.37f,.32f);
        [SerializeField] private Color _combatGround=new(.33f,.37f,.29f);
        [SerializeField] private Color _combatFoliage=new(.29f,.39f,.26f);
        [SerializeField] private Theme[] _themes={
            new("yirtik_koru",new(.37f,.365f,.27f),new(.37f,.40f,.27f)),
            new("halka_koyu",new(.40f,.345f,.265f),new(.31f,.39f,.27f)),
            new("fisilti_batakligi",new(.28f,.345f,.29f),new(.27f,.37f,.29f)),
            new("uyuyanlar_vadisi",new(.32f,.405f,.32f),new(.29f,.40f,.30f)),
            new("oyuk_tepeler",new(.405f,.355f,.285f),new(.34f,.395f,.28f)),
            new("bayterek_kalbi",new(.305f,.365f,.265f),new(.27f,.37f,.25f)),
            new("kok_ahdi_korulari",new(.34f,.415f,.29f),new(.31f,.415f,.27f))
        };
        public Shader Shader=>_shader;
        public float BlendWidth=>_blendWidth;
        public Vector4 Strengths=>new(_groundStrength,_foliageStrength,_stoneStrength,0);
        public Color StoneTone=>_stoneTone;
        public Color Ground(string id){foreach(var t in _themes)if(t.id==id)return t.ground;return _combatGround;}
        public Color Foliage(string id){foreach(var t in _themes)if(t.id==id)return t.foliage;return _combatFoliage;}
    }
}
