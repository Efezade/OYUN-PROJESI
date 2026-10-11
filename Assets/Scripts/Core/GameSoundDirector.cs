using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;
namespace TacticalRPG.Core
{
    /// <summary>Game events select the score and Foley; no scene searches or per-frame polling.</summary>
    public sealed class GameSoundDirector : MonoBehaviour
    {
        [SerializeField] private GameAudio _audio;
        [SerializeField] private GameStateManager _state;
        [SerializeField] private RegionPresence _region;
        [SerializeField] private PlayerController _player;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private ActionPointManager _time;
        [SerializeField] private ChapterNodeManager _nodes;
        [SerializeField] private MandatoryQuestDirector _quests;
        [SerializeField] private EssenceFieldManager _essence;
        [SerializeField] private PartyManager _party;
        [SerializeField] private TurnManager _turns;
        [SerializeField] private CorruptionManager _corruption;
        [SerializeField] private TravelOrbVisual _travel;
        [SerializeField] private MenuNavigator _menus;
        [SerializeField] private UnitManager _units;
        [SerializeField] private CombatDrumManager _drum;
        [SerializeField] private KamSkillCaster _skills;
        private readonly List<Action> _unbind=new();
        private readonly Dictionary<Unit,Action> _unitUnbind=new();
        private void OnEnable()
        {
            if(_state!=null){_state.OnStateChanged+=State;_unbind.Add(()=>_state.OnStateChanged-=State);}
            if(_region!=null){_region.OnRegionEntered+=Region;_unbind.Add(()=>_region.OnRegionEntered-=Region);}
            if(_player!=null){_player.OnMoved+=Step;_unbind.Add(()=>_player.OnMoved-=Step);}
            if(_time!=null){_time.OnDayNightChanged+=Night;_unbind.Add(()=>_time.OnDayNightChanged-=Night);}
            if(_nodes!=null){_nodes.OnNodeCompleted+=Completed;_unbind.Add(()=>_nodes.OnNodeCompleted-=Completed);}
            if(_quests!=null){_quests.OnQuestUnlocked+=Quest;_unbind.Add(()=>_quests.OnQuestUnlocked-=Quest);}
            if(_essence!=null){_essence.OnDepositRemoved+=Harvest;_unbind.Add(()=>_essence.OnDepositRemoved-=Harvest);}
            if(_party!=null){_party.OnCardLeveledUp+=Level;_unbind.Add(()=>_party.OnCardLeveledUp-=Level);}
            if(_turns!=null){_turns.OnUnitTurnBegan+=Turn;_turns.OnCombatEnded+=CombatEnd;_turns.OnRoundStarted+=Round;
                _unbind.Add(()=>{_turns.OnUnitTurnBegan-=Turn;_turns.OnCombatEnded-=CombatEnd;_turns.OnRoundStarted-=Round;});}
            if(_corruption!=null){_corruption.OnSourcePurified+=Purified;_unbind.Add(()=>_corruption.OnSourcePurified-=Purified);}
            if(_travel!=null){_travel.OnTravelBegan+=Travel;_travel.OnTravelFinished+=Arrived;
                _unbind.Add(()=>{_travel.OnTravelBegan-=Travel;_travel.OnTravelFinished-=Arrived;});}
            if(_menus!=null){_menus.OnMenuOpenChanged+=Menu;_menus.OnScreenChanged+=Screen;
                _unbind.Add(()=>{_menus.OnMenuOpenChanged-=Menu;_menus.OnScreenChanged-=Screen;});}
            if(_drum!=null){_drum.OnChoicesOffered+=Draft;_drum.OnAugmentPlaced+=Placed;
                _unbind.Add(()=>{_drum.OnChoicesOffered-=Draft;_drum.OnAugmentPlaced-=Placed;});}
            if(_skills!=null){_skills.OnSkillFinished+=Skill;_unbind.Add(()=>_skills.OnSkillFinished-=Skill);}
            if(_units!=null){foreach(var unit in _units.Units)BindUnit(unit);_units.OnUnitRegistered+=BindUnit;_units.OnUnitRemoved+=UnbindUnit;
                _unbind.Add(()=>{_units.OnUnitRegistered-=BindUnit;_units.OnUnitRemoved-=UnbindUnit;});}
        }
        private void Start()=>State(_state!=null?_state.State:GameState.Overworld);
        private void OnDisable(){foreach(var undo in _unbind)undo();_unbind.Clear();foreach(var undo in _unitUnbind.Values)undo();_unitUnbind.Clear();}
        private void State(GameState state)=>_audio?.SetTheme(state==GameState.Overworld||state==GameState.ConfirmMission ? _region?.Current?.Id??"kok_ahdi_korulari" : _state?.ActiveMission?.Tier==MapNodeType.Boss ? "boss" : "combat");
        private void Region(RegionSO region){if(_state==null||_state.State==GameState.Overworld)_audio?.SetTheme(region!=null?region.Id:"kok_ahdi_korulari");SoundSignals.Play("region",.55f);}
        public static string SurfaceCue(string id)
        {
            id=(id??"").ToLowerInvariant();
            if(id.Contains("su")||id.Contains("nehir")||id.Contains("gol"))return "step_water";
            if(id.Contains("batak")||id.Contains("camur"))return "step_mud";
            if(id.Contains("kopru")||id.Contains("odun")||id.Contains("kovuk"))return "step_wood";
            if(id.Contains("tas")||id.Contains("kaya")||id.Contains("kule"))return "step_stone";
            if(id.Contains("kum")||id.Contains("oyuk"))return "step_gravel";
            return "step_grass";
        }
        private void Step(HexCoordinate c)=>SoundSignals.At(SurfaceCue(_map!=null?_map.TerrainIdAt(c):null),_player.transform.position,.7f);
        private void Night(bool night)=>SoundSignals.Play(night?"night":"day",.6f);
        private void Completed(ChapterNodeManager.MapNode node)=>SoundSignals.Play("quest_complete");
        private void Quest(int index,HexCoordinate coord)=>SoundSignals.Play("quest_new");
        private void Harvest(HexCoordinate c,bool depleted)=>SoundSignals.Play("harvest",.7f);
        private void Level(CharacterCard card)=>SoundSignals.Play("level_up");
        private void Turn(Unit unit)=>SoundSignals.Play(unit.Team==UnitTeam.Player?"turn_player":"turn_enemy",.5f);
        private void Round(int round)=>SoundSignals.Play("round",.5f);
        private void CombatEnd(CombatResult result)=>SoundSignals.Play(result==CombatResult.PlayerWon?"victory":"defeat");
        private void Purified(int region)=>SoundSignals.Play("purify");
        private void Travel()=>SoundSignals.Play("teleport");
        private void Arrived()=>SoundSignals.Play("travel_end");
        private void Menu(bool open){_audio?.SetMenuDucking(open);SoundSignals.Play(open?"ui_open":"ui_close");}
        private void Screen(MenuScreen screen)=>SoundSignals.Play("page_turn",.65f);
        private void Draft()=>SoundSignals.Play("card_pick");
        private void Placed(HexCoordinate c,AugmentCatalog.Entry entry)=>SoundSignals.Play("item_drop");
        private void Skill(KamSkillCatalog.Entry skill,bool success){if(!success)SoundSignals.Play("ui_close",.3f);}
        private void BindUnit(Unit unit)
        {
            if(unit==null||_unitUnbind.ContainsKey(unit))return;
            void Attack(Unit target)=>SoundSignals.At(unit.AttackRange>1?"bow":"sword",unit.transform.position);
            void Hurt(Unit target,int amount)=>SoundSignals.At("impact",target.transform.position,.8f);
            void Death(Unit target)=>SoundSignals.At("death",target.transform.position);
            void Move(Unit target,HexCoordinate c)=>SoundSignals.At("step_stone",target.transform.position,.5f);
            unit.OnAttackPerformed+=Attack;unit.OnDamaged+=Hurt;unit.OnDied+=Death;unit.OnEnteredCell+=Move;
            _unitUnbind[unit]=()=>{if(unit==null)return;unit.OnAttackPerformed-=Attack;unit.OnDamaged-=Hurt;unit.OnDied-=Death;unit.OnEnteredCell-=Move;};
        }
        private void UnbindUnit(Unit unit){if(_unitUnbind.TryGetValue(unit,out var undo)){undo();_unitUnbind.Remove(unit);}}
    }
}
