using System;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    public enum GameState { Overworld, ConfirmMission, Combat }

    /// <summary>
    /// Oyunun üst düzey durum makinesi: Overworld ↔ Savaş geçişini yönetir.
    /// Tek sahne; savaşa girince grid savaş TileMap'iyle yeniden üretilir,
    /// dönüşte overworld haritası ve oyuncu geri yüklenir. (Tasarım kararı: tek sahne.)
    /// Sistemler OnStateChanged'i dinleyerek kendini açar/kapatır (event-driven).
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager  _grid;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private PlayerController _player;

        public GameState   State          { get; private set; } = GameState.Overworld;
        public MissionData PendingMission { get; private set; }
        public MissionData ActiveMission  { get; private set; }

        public event Action<GameState> OnStateChanged;

        private TileMapSO     _overworldMap;
        private HexCoordinate _savedPlayerCoord;

        private void Start()
        {
            if (_grid != null) _overworldMap = _grid.TileMap; // overworld haritasını sakla
            SetState(GameState.Overworld);
        }

        // ── Görev akışı ───────────────────────────────────────────────────────

        public void RequestMission(MissionData mission)
        {
            if (State != GameState.Overworld || mission == null) return;
            PendingMission = mission;
            SetState(GameState.ConfirmMission);
        }

        public void CancelMission()
        {
            if (State != GameState.ConfirmMission) return;
            PendingMission = null;
            SetState(GameState.Overworld);
        }

        public void ConfirmMission()
        {
            if (State != GameState.ConfirmMission || PendingMission == null) return;
            EnterCombat(PendingMission);
        }

        // ── Geçişler ──────────────────────────────────────────────────────────

        private void EnterCombat(MissionData mission)
        {
            ActiveMission  = mission;
            PendingMission = null;

            if (_player != null) _savedPlayerCoord = _player.CurrentCoord;

            if (_grid != null && mission.CombatMap != null)
                _grid.SetTileMap(mission.CombatMap);     // grid'i savaş haritasına çevir
            if (_fog != null) _fog.RevealAll();          // savaşta tam görüş
            if (_player != null) _player.gameObject.SetActive(false); // overworld jetonu gizle

            SetState(GameState.Combat);
        }

        public void ReturnToOverworld()
        {
            if (State != GameState.Combat) return;
            ActiveMission = null;

            if (_grid != null && _overworldMap != null)
                _grid.SetTileMap(_overworldMap);         // overworld haritasını geri yükle
            if (_fog != null) _fog.ResetFog();
            if (_player != null)
            {
                _player.gameObject.SetActive(true);
                _player.Initialize(_savedPlayerCoord);   // konum + görüş geri gelir
            }

            SetState(GameState.Overworld);
        }

        private void SetState(GameState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
