using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
namespace TacticalRPG.Core
{
    /// <summary>Persistent animated voids follow collapse data and fog without restoring terrain.</summary>
    public sealed class WorldVoidPresentation : MonoBehaviour
    {
        [SerializeField] private MapCollapseManager _collapse;
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private GameStateManager _state;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private GameObject _voidPrefab;
        private readonly Dictionary<HexCoordinate,GameObject> _voids=new();
        private bool _dirty=true;private int _exploration=-1;
        private MaterialPropertyBlock _block;
        private void Awake()=>_block=new MaterialPropertyBlock();
        private void OnEnable(){if(_collapse!=null){_collapse.OnTileCollapsed+=Changed;_collapse.OnTileRestored+=Restored;_collapse.OnTileVanished+=Vanished;}if(_grid!=null)_grid.OnGridRegenerated+=Rebuild;if(_state!=null)_state.OnStateChanged+=State;}
        private void OnDisable(){if(_collapse!=null){_collapse.OnTileCollapsed-=Changed;_collapse.OnTileRestored-=Restored;_collapse.OnTileVanished-=Vanished;}if(_grid!=null)_grid.OnGridRegenerated-=Rebuild;if(_state!=null)_state.OnStateChanged-=State;Clear();}
        private void Changed(int count,int total)=>_dirty=true;
        private void State(GameState state)=>_dirty=true;
        private void Restored(HexCoordinate c){if(_voids.Remove(c,out var go))Destroy(go);SoundSignals.Play("void_close");_dirty=true;}
        private void Vanished(HexCoordinate c){Create(c,true);_dirty=true;}
        private void Rebuild(){Clear();_dirty=true;}
        private void Clear(){foreach(var go in _voids.Values)if(go!=null)Destroy(go);_voids.Clear();}
        private void LateUpdate()
        {
            if(_collapse==null||_grid==null||_voidPrefab==null)return;
            if(_fog!=null&&_exploration!=_fog.ExplorationVersion){_exploration=_fog.ExplorationVersion;_dirty=true;}
            if(!_dirty)return;_dirty=false;
            bool world=_state==null||_state.State==GameState.Overworld||_state.State==GameState.ConfirmMission;
            if(world)foreach(var coord in _collapse.CollapsedTiles)Create(coord,false);
            foreach(var kv in _voids)
            {
                bool active=world&&_collapse.IsCollapsed(kv.Key)&&(_fog==null||_fog.CanRenderWorldModel(kv.Key));
                if(kv.Value!=null)kv.Value.SetActive(active);
                if(world&&_collapse.IsCollapsed(kv.Key)&&_grid.TryGetCell(kv.Key,out var cell))cell.RefreshVisualVisibility();
            }
        }
        private void Create(HexCoordinate coord,bool animate)
        {
            if(_voidPrefab==null||_grid==null||_voids.ContainsKey(coord))return;
            var go=Instantiate(_voidPrefab,transform);go.name="Void_"+coord;
            go.transform.position=coord.ToWorldPosition(_grid.HexSize)+Vector3.up*.34f;
            go.transform.localScale=Vector3.one*_grid.HexSize;
            var renderer=go.GetComponent<MeshRenderer>();_block.Clear();_block.SetFloat("_Born",animate?Time.time:-100);renderer.SetPropertyBlock(_block);
            _voids.Add(coord,go);if(animate&&(_fog==null||_fog.IsKnown(coord)))SoundSignals.At("void_open",go.transform.position,.6f);
        }
    }
}
