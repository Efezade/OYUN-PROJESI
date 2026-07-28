using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Sol tıklama ile hex karosu seçimini algılar ve A* yolu tetikler.
    /// Overworld hareketi İKİ AŞAMALI (XCOM / Desperados 3): 1. tık yolu <see cref="PathPreview"/>
    /// ile gösterir, aynı karoya 2. tık yürütür. Farklı karo yeni önizleme açar, boşluk iptal eder.
    /// Menzil (<see cref="_maxMoveRange"/>) yalnız SAVAŞ SİSİ VARKEN uygulanır; sis kule ile
    /// kalıcı kaldırılmışsa serbest yürüyüş.
    /// </summary>
    public class MapInputHandler : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private Camera           _camera;
        [SerializeField] private HexGridManager   _gridManager;
        [SerializeField] private PlayerController _player;
        [Tooltip("Opsiyonel — atanmışsa, yetenek hazırken tıklama hedefleme olur.")]
        [SerializeField] private AbilityCaster    _caster;
        [Tooltip("Opsiyonel — atanmışsa sadece Overworld state'te tıklama işlenir + görev tıklaması.")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private MissionManager   _missionManager;
        [Tooltip("Opsiyonel — Deployment state'inde tıklama buraya yerleştirme olur.")]
        [SerializeField] private DeploymentManager _deployment;
        [Tooltip("Opsiyonel — Combat state'inde tıklama aktif birim hareket/saldırı olur.")]
        [SerializeField] private TurnManager _turnManager;
        [Tooltip("Opsiyonel — atanmışsa portal ışınlaması sürerken (IsBusy) tıklama yok sayılır + " +
                 "ada değişince yol önizlemesi temizlenir.")]
        [SerializeField] private WorldGridManager _worldGrid;

        [Tooltip("Opsiyonel — atanmışsa bölüm kaybedilince (sert kesim) harita tıklamaları kilitlenir.")]
        [SerializeField] private ChapterRunManager _run;
        [Tooltip("Opsiyonel — atanmışsa SAVAŞ SİSİ kalkmış adada (kule ile) menzil sınırı kalkar.")]
        [SerializeField] private FogOfWarManager _fogManager;
        [Tooltip("Opsiyonel — atanmışsa tıklanan yol önce çizgiyle gösterilir (ÇİFT TIK = yürü).")]
        [SerializeField] private PathPreview _preview;

        [Header("Raycast")]
        [SerializeField] private LayerMask _clickableLayers = ~0;
        [SerializeField] private float     _rayDistance     = 300f;

        [Header("Hareket Menzili")]
        [Tooltip("SAVAŞ SİSİ VARKEN tek seferde en fazla kaç karo yürünür. Sis kule ile kalıcı " +
                 "kaldırılmışsa (FogOfWarManager.IsFullyRevealed) bu sınır UYGULANMAZ — serbest yürüyüş.")]
        [SerializeField] private int _maxMoveRange = 2;

        private HexPathfinder _pathfinder;

        // İki aşamalı hareket (XCOM/Desperados): 1. tık = yolu göster, 2. tık (aynı karo) = yürü.
        private HexCoordinate _pendingCoord;
        private List<HexCell> _pendingPath;   // menzil dışıysa null → 2. tık yürütmez, iptal eder
        private bool          _hasPending;

        /// <summary>Mağaza "Menzil" iksiri/pusulası buradan tek-tık menzilini artırır (PlayerBuffs yönetir).</summary>
        public int BonusMoveRange { get; set; }

        /// <summary>Savaş sisi kule ile kaldırılmışsa menzil sınırı yok (serbest yürüyüş).</summary>
        private int EffectiveMoveRange =>
            (_fogManager != null && _fogManager.IsFullyRevealed) ? int.MaxValue : _maxMoveRange + BonusMoveRange;

        private void Awake()
        {
            _pathfinder = new HexPathfinder();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            if (_worldGrid != null) _worldGrid.OnMapChanged += ClearPreview;
        }

        private void OnDisable()
        {
            if (_worldGrid != null) _worldGrid.OnMapChanged -= ClearPreview;
        }

        private void Update()
        {
            // Önizleme geçersizleştiyse temizle (savaşa girildi / karakter yürümeye başladı).
            // _hasPending false iken bedava — Update'te ağır iş yok.
            if (_hasPending &&
                ((_stateManager != null && _stateManager.State != GameState.Overworld) || _player.IsMoving))
                ClearPreview();

            if (!Input.GetMouseButtonDown(0)) return;

            // IMGUI paneli (portal/kule istemi, savaş HUD'u …) üstüne tıklandıysa harita tıklaması
            // DEĞİLDİR. Update, OnGUI'den önce çalıştığı için aksi halde "Evet, Isinlan" butonuna
            // basmak panelin arkasındaki karoda yol önizlemesi açıyordu.
            if (ImguiBlocker.IsPointerOver(Input.mousePosition)) return;

            // uGUI menü kabuğu (KİTAP/ÇANTA/HARİTA paneli veya sekme/ayar düğmesi) üstüne
            // tıklandıysa harita tıklaması DEĞİLDİR — IMGUI için ImguiBlocker'ın uGUI karşılığı.
            // Tam-ekran panel açıkken zeminin raycast hedefi tüm ekranı kapladığı için harita
            // girişi de doğal olarak bloklanır. EventSystem yoksa (henüz kurulmadıysa) atlanır.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // Deployment: tıklama = seçili kartı hex'e yerleştir.
            if (_stateManager != null && _stateManager.State == GameState.Deployment)
            {
                if (_deployment != null && TryGetClickedCoord(out HexCoordinate deployCoord))
                    _deployment.TryDeployAt(deployCoord);
                return;
            }

            // Combat: Kam büyü hazırsa tıklama = büyü hedefleme; değilse hareket/saldırı.
            if (_stateManager != null && _stateManager.State == GameState.Combat)
            {
                if (!TryGetClickedCoord(out HexCoordinate combatCoord)) return;
                if (_caster != null && _caster.HasArmedAbility) { _caster.TryCastAt(combatCoord); return; }
                if (_turnManager != null) _turnManager.HandlePlayerClick(combatCoord);
                return;
            }

            // Diğer savaş/onay durumlarında harita tıklaması işlenmez (akış HUD'larca yönetilir).
            if (_stateManager != null && _stateManager.State != GameState.Overworld) return;
            if (_player.IsMoving) return;
            if (_worldGrid != null && _worldGrid.IsBusy) return;   // portal ışınlaması sürerken giriş yok
            // SERT KESİM (TASK-007): bölüm kaybedildiyse harita ARTIK İLERLENEMEZ — yalnız
            // "Yeniden Başla" düğmesi çalışır (ChapterRunHUD).
            if (_run != null && _run.ChapterLost) return;

            // Boşluğa tıklama → önizleme varsa iptal. (Adalar arası geçiş yalnız PORTAL karosuyla
            // olur — TeleportManager oyuncu portala basınca devreye girer.)
            if (!TryGetClickedCoord(out HexCoordinate coord)) { ClearPreview(); return; }

            // 1) Yetenek hazırsa → hedefleme
            if (_caster != null && _caster.HasArmedAbility) { _caster.TryCastAt(coord); return; }

            // 2) Görev karosuna tıklandıysa → YETERİNCE YAKINSA onay akışı, uzaksa oraya yürü.
            if (_stateManager != null && _missionManager != null)
            {
                MissionData mission = _missionManager.GetMissionAt(coord);
                if (mission != null &&
                    _player.CurrentCoord.DistanceTo(coord) <= _missionManager.EnterRange)
                { ClearPreview(); _stateManager.RequestMission(mission); return; }
                // Çok uzak → göreve girme; marker'a doğru yolu göster (aşağıya düş).
            }

            // 3) Aksi halde → İKİ AŞAMALI hareket (1. tık yol, 2. tık yürü).
            HandleMoveClick(coord);
        }

        // XCOM/Desperados akışı: aynı karoya ikinci tık onaydır; farklı karo yeni önizleme açar.
        private void HandleMoveClick(HexCoordinate targetCoord)
        {
            // İKİNCİ tık (aynı karo) → onay.
            if (_hasPending && _pendingCoord.Equals(targetCoord))
            {
                List<HexCell> path = _pendingPath;   // menzil dışıysa null → sadece iptal
                ClearPreview();
                if (path != null) _player.MoveAlongPath(path);
                return;
            }

            // İLK tık → yolu hesapla + göster.
            if (!_gridManager.TryGetCell(targetCoord, out HexCell target) || !target.IsWalkable)
            { ClearPreview(); return; }
            // Sisli (Hidden) karoya YÜRÜNEBİLİR — görüş 1, menzil 2: sisin içine girilir.
            if (!_gridManager.TryGetCell(_player.CurrentCoord, out HexCell start))
            { ClearPreview(); return; }

            List<HexCell> found = _pathfinder.FindPath(start, target, _gridManager);
            if (found == null || found.Count < 2) { ClearPreview(); return; }

            // Menzil kapısı yalnız SAVAŞ SİSİ VARKEN; sis kalkmışsa EffectiveMoveRange sınırsız.
            bool reachable = found.Count - 1 <= EffectiveMoveRange;

            _pendingCoord = targetCoord;
            _pendingPath  = reachable ? found : null;
            _hasPending   = true;
            if (_preview != null) _preview.Show(found, reachable);
        }

        private void ClearPreview()
        {
            _hasPending  = false;
            _pendingPath = null;
            if (_preview != null) _preview.Hide();
        }

        private bool TryGetClickedCoord(out HexCoordinate coord)
        {
            coord = default;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _clickableLayers))
                return false;
            coord = _gridManager.WorldToHex(hit.point);
            return true;
        }

    }
}
