using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Neighbour-aware colour blending across tile edges. Runs on map/visual changes,
    /// not every frame. Author meshes/textures remain untouched, including personal tiles.</summary>
    public sealed class BiomeTerrainStyling : MonoBehaviour
    {
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private BiomeTerrainStyleSO _style;
        private TileMapSO _overworld;
        private bool _dirty;
        private readonly Dictionary<Material,Material> _materials=new();
        private readonly HashSet<HexCoordinate> _changed=new();
        private MaterialPropertyBlock _block;
        private static readonly int Center=Shader.PropertyToID("_BiomeCenter");
        private static readonly int Ground=Shader.PropertyToID("_BiomeGround");
        private static readonly int Leaves=Shader.PropertyToID("_BiomeFoliage");
        private static readonly int Stone=Shader.PropertyToID("_BiomeStone");
        private static readonly int Strength=Shader.PropertyToID("_BiomeStrength");
        private static readonly int[] Neighbours={Shader.PropertyToID("_Neighbour0"),Shader.PropertyToID("_Neighbour1"),
            Shader.PropertyToID("_Neighbour2"),Shader.PropertyToID("_Neighbour3"),Shader.PropertyToID("_Neighbour4"),Shader.PropertyToID("_Neighbour5")};
        private static readonly int[] LeafNeighbours={Shader.PropertyToID("_Leaf0"),Shader.PropertyToID("_Leaf1"),
            Shader.PropertyToID("_Leaf2"),Shader.PropertyToID("_Leaf3"),Shader.PropertyToID("_Leaf4"),Shader.PropertyToID("_Leaf5")};
        public BiomeTerrainStyleSO Style=>_style;
        private void Awake()=>_block=new MaterialPropertyBlock();
        private void OnEnable()
        {
            if(_grid!=null){_grid.OnGridRegenerated+=Dirty;_grid.OnCellVisualRegenerated+=CellChanged;}
            if(_map!=null)_map.OnMapGenerated+=NewMap;
            _dirty=true;
        }
        private void OnDisable()
        {
            if(_grid!=null){_grid.OnGridRegenerated-=Dirty;_grid.OnCellVisualRegenerated-=CellChanged;}
            if(_map!=null)_map.OnMapGenerated-=NewMap;
        }
        private void OnDestroy(){foreach(var mat in _materials.Values)if(mat!=null)Destroy(mat);}
        private void Dirty()=>_dirty=true;
        private void NewMap(){_overworld=_grid.TileMap;Dirty();}
        private void CellChanged(HexCoordinate c)=>_changed.Add(c);
        private void LateUpdate()
        {
            if(_grid==null || !_grid.HasCells || _style==null || _style.Shader==null)return;
            if(_dirty)
            {
                _dirty=false;_changed.Clear();
                foreach(var pair in _grid.Cells)Apply(pair.Value);
            }
            else if(_changed.Count>0)
            {
                foreach(var c in _changed)if(_grid.TryGetCell(c,out var cell))Apply(cell);
                _changed.Clear();
            }
        }
        private string RegionId(HexCoordinate c)=>_grid.TileMap==_overworld && _map!=null ? _map.RegionAt(c)?.Id : null;
        private static Vector4 Linear(Color color,float a)
        {
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
            return new Vector4(color.r,color.g,color.b,a);
        }
        private void Apply(HexCell cell)
        {
            if(cell.Visual==null)return;
            string region=RegionId(cell.Coordinate);
            bool authored=cell.Visual.GetComponent<BiomeTileAppearance>()!=null;
            // The overworld root is initially hidden by the opening transition.
            // Prepare inactive children too, so the first reveal already has its theme.
            var renderers=cell.Visual.GetComponentsInChildren<MeshRenderer>(true);
            foreach(var renderer in renderers)
            {
                var originals=renderer.sharedMaterials;bool replaced=false;
                for(int i=0;i<originals.Length;i++)
                {
                    var material=originals[i];
                    if(material==null || material.shader==_style.Shader ||
                        (material.HasProperty("_Surface") && material.GetFloat("_Surface")>.5f))continue;
                    if(!_materials.TryGetValue(material,out var styled))
                    {
                        styled=new Material(_style.Shader){name=material.name+" (Biome runtime)"};
                        styled.SetTexture("_BaseMap",material.mainTexture);
                        styled.SetFloat("_UseHeightMask",authored?0:1);
                        _materials.Add(material,styled);
                    }
                    originals[i]=styled;replaced=true;
                }
                if(replaced)renderer.sharedMaterials=originals;
                renderer.GetPropertyBlock(_block);
                _block.SetVector(Center,new Vector4(cell.WorldPosition.x,cell.WorldPosition.y,cell.WorldPosition.z,_grid.HexSize));
                _block.SetVector(Ground,Linear(_style.Ground(region),_style.BlendWidth));
                _block.SetVector(Leaves,Linear(_style.Foliage(region),1));
                _block.SetVector(Stone,Linear(_style.StoneTone,1));
                _block.SetVector(Strength,_style.Strengths);
                for(int d=0;d<6;d++)
                {
                    var c=cell.Coordinate.GetNeighbor(d);bool exists=_grid.TryGetCell(c,out _);
                    string next=RegionId(c);
                    _block.SetVector(Neighbours[d],Linear(_style.Ground(next),exists?1:0));
                    _block.SetVector(LeafNeighbours[d],Linear(_style.Foliage(next),exists?1:0));
                }
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
