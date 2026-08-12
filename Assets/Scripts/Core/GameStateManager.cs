using System;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    public enum GameState { Overworld, ConfirmMission, Deployment, Combat }

    /// <summary>
    /// Oyunun üst düzey durum makinesi: Overworld ↔ Savaş geçişini yönetir.
    /// Tek sahne; savaşa girince grid savaş TileMap'iyle yeniden üretilir,
    /// dönüşte overworld haritası ve oyuncu geri yüklenir. (Tasarım kararı: tek sahne.)
    /// Sistemler OnStateChanged'i dinleyerek kendini açar/kapatır (event-driven).
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager    _grid;
        [Tooltip("Savaş arenasını PROSEDÜREL üretir (2026-08-12). Atanmazsa görevin elle atanmış " +
                 "CombatMap'i kullanılır — eski davranış korunur.")]
        [SerializeField] private CombatMapGenerator _arena;
        [SerializeField] private FogOfWarManager   _fog;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private ActionPointManager _apManager;

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
            EnterDeployment(PendingMission);
        }

        /// <summary>Yerleştirme bittiğinde çağrılır (DeploymentHUD "Savaşı Başlat"). Savaşa geçer.</summary>
        public void StartBattle()
        {
            if (State != GameState.Deployment) return;
            SetState(GameState.Combat);
        }

        // ── Geçişler ──────────────────────────────────────────────────────────

        // Savaş haritasına geçer ve YERLEŞTİRME fazını açar (birimler burada öz ile sürülür).
        private void EnterDeployment(MissionData mission)
        {
            ActiveMission  = mission;
            PendingMission = null;

            // Savaşa girmek sabit bedel (varsayılan 3 AP) — TÜM savaş bu kadar sayılır.
            // Bedel ödendikten SONRA motor dondurulur: savaş boyunca zaman akmaz, gün geçmez.
            if (_apManager != null)
            {
                _apManager.SpendCombatEntryCost();
                _apManager.SetFrozen(true);
            }
            if (_player != null) _savedPlayerCoord = _player.CurrentCoord;

            // Arena: önce PROSEDÜREL üretici denenir (düğüm tipine göre kademe), o yoksa görevin
            // elle atanmış haritası. İkisi de yoksa grid overworld'de kalır ve savaş 550 karoluk
            // kıtada açılırdı — bu yüzden fallback zinciri açıkça yazılı.
            bool arenaBuilt = _arena != null && _arena.Build(CombatMapGenerator.TierFor(mission.Tier));
            if (!arenaBuilt && _grid != null && mission.CombatMap != null)
                _grid.SetTileMap(mission.CombatMap);     // grid'i savaş haritasına çevir
            if (_fog != null) _fog.RevealAll();          // savaşta tam görüş
            if (_player != null) _player.gameObject.SetActive(false); // overworld jetonu gizle

            SetState(GameState.Deployment);
        }

        public void ReturnToOverworld()
        {
            if (State != GameState.Combat && State != GameState.Deployment) return;
            ActiveMission = null;

            // Savaş bitti → zaman yeniden akmaya başlar (giriş bedeli zaten ödenmişti).
            if (_apManager != null) _apManager.SetFrozen(false);

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
