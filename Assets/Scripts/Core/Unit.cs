using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    public enum UnitTeam { Player, Enemy }

    /// <summary>
    /// Hex üstünde duran bir savaş birimi (oyuncu veya düşman).
    /// Bir CharacterCard'a BAĞLIYSA HP/stat tek kaynaktan (karttan) gelir; bağlı değilse
    /// kendi _maxHP'sini kullanır (basit düşman kuklası için geri uyumlu).
    /// Faz B (deployment) oyuncu birimini Bind(card) + PlaceAt(coord) ile spawn eder.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [Header("Kimlik")]
        [SerializeField] private string   _displayName = "Birim";
        [SerializeField] private UnitTeam _team        = UnitTeam.Enemy;

        [Header("Konum")]
        [SerializeField] private HexCoordinate _coord;
        [SerializeField] private float         _heightOffset = 0.8f;
        [SerializeField] private float         _moveSpeed    = 8f;

        [Header("Can (yalnızca KARTSIZ birimlerde kullanılır)")]
        [SerializeField, Min(1)] private int _maxHP = 10;

        [Header("Savaş statları (yalnızca KARTSIZ birimlerde)")]
        [SerializeField, Min(0)] private int _attack      = 3;
        [SerializeField, Min(0)] private int _defense     = 0;
        [SerializeField, Min(1)] private int _speed       = 5;
        [SerializeField, Min(1)] private int _moveRange   = 3;
        [SerializeField, Min(1)] private int _attackRange = 1;

        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager _gridManager;
        [SerializeField] private UnitManager    _unitManager;

        private CharacterCard _card;        // bağlıysa stat kaynağı (oyuncu/kartlı birim)
        private int           _currentHP;   // yalnızca kartsız birimde geçerli
        private bool          _diedNotified;

        // ── Kimlik / konum ────────────────────────────────────────────────────
        public string        DisplayName => _card != null ? _card.Data.ClassName : _displayName;
        public UnitTeam      Team        => _team;
        public HexCoordinate Coordinate  => _coord;
        public CharacterCard Card        => _card;
        public bool          IsCommander => _card != null && _card.IsCommander;

        // ── Can / kalkan ──────────────────────────────────────────────────────
        public int  MaxHP     => _card != null ? _card.MaxHP     : _maxHP;
        public int  CurrentHP => _card != null ? _card.CurrentHP : _currentHP;
        public int  Shield    { get; private set; }
        public bool IsAlive   => CurrentHP > 0;

        // ── Savaş statları (kart varsa karttan; yoksa kartsız fallback alanlarından) ──
        public int Attack      => _card != null ? _card.Attack      : _attack;
        public int Defense     => _card != null ? _card.Defense     : _defense;
        public int Level       => _card != null ? _card.Level       : 1;
        public int MoveRange   => _card != null ? _card.MoveRange   : _moveRange;
        public int Speed       => _card != null ? _card.Speed       : _speed;
        public int AttackRange => _card != null ? _card.AttackRange : _attackRange;

        public event Action<Unit> OnStatsChanged; // HP veya kalkan değişti
        public event Action<Unit> OnDied;

        private void Awake()
        {
            if (_card == null) _currentHP = _maxHP;
        }

        private void OnEnable()
        {
            if (_unitManager != null) _unitManager.Register(this);
        }

        private void OnDisable()
        {
            if (_unitManager != null) _unitManager.Unregister(this);
        }

        private void Start() => SnapToCell();

        private void OnDestroy()
        {
            if (_card != null) _card.OnHPChanged -= HandleCardHPChanged;
        }

        // ── Kart bağlama (Faz B deployment) ───────────────────────────────────

        /// <summary>
        /// Birimi bir CharacterCard'a bağlar — HP/stat artık karttan gelir (tek kaynak).
        /// Düşman da kartlı olabilir; takım bağımsızdır, çağıran ayarlar.
        /// </summary>
        public void Bind(CharacterCard card)
        {
            if (card == null) return;
            if (_card != null) _card.OnHPChanged -= HandleCardHPChanged;

            _card         = card;
            _diedNotified = false;
            _card.OnHPChanged += HandleCardHPChanged;
            OnStatsChanged?.Invoke(this);
        }

        /// <summary>Birimi bir koordinata yerleştirir ve görsel olarak oraya oturtur.</summary>
        public void PlaceAt(HexCoordinate coord)
        {
            _coord = coord;
            SnapToCell();
        }

        /// <summary>
        /// Runtime spawn için bağımlılıkları + takımı ayarlar (DeploymentManager kullanır).
        /// UnitManager'a (yeniden) kaydeder.
        /// </summary>
        public void Configure(HexGridManager grid, UnitManager unitManager, UnitTeam team)
        {
            _gridManager = grid;
            _team        = team;
            if (_unitManager != null && _unitManager != unitManager) _unitManager.Unregister(this);
            _unitManager = unitManager;
            if (_unitManager != null) _unitManager.Register(this);
        }

        private void SnapToCell()
        {
            if (_gridManager != null && _gridManager.TryGetCell(_coord, out HexCell cell))
                transform.position = SurfacePosition(cell);
        }

        // Hücrenin yürüme yüzeyine oturmuş dünya pozisyonu (köprü/engebe SurfaceHeight ile).
        private Vector3 SurfacePosition(HexCell cell) =>
            cell.WorldPosition + Vector3.up * (_heightOffset + cell.SurfaceHeight - HexMetrics.TileHeight);

        // ── Hareket (TurnManager savaşta çağırır) ─────────────────────────────

        public bool IsMoving { get; private set; }

        /// <summary>Birimi verilen hücre yolu boyunca yürütür (path[0] = mevcut konum).</summary>
        public void MoveAlongPath(List<HexCell> path, Action onComplete = null)
        {
            if (path == null || path.Count < 2) { onComplete?.Invoke(); return; }
            StartCoroutine(MoveRoutine(path, onComplete));
        }

        private IEnumerator MoveRoutine(List<HexCell> path, Action onComplete)
        {
            IsMoving = true;
            for (int i = 1; i < path.Count; i++)
            {
                HexCell cell   = path[i];
                Vector3 target = SurfacePosition(cell);
                while ((transform.position - target).sqrMagnitude > 0.0001f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, _moveSpeed * Time.deltaTime);
                    yield return null;
                }
                _coord = cell.Coordinate;
            }
            IsMoving = false;
            onComplete?.Invoke();
        }

        private void HandleCardHPChanged(int current, int max)
        {
            OnStatsChanged?.Invoke(this);
            if (current <= 0 && !_diedNotified)
            {
                _diedNotified = true;
                OnDied?.Invoke(this);
            }
        }

        // ── Etki API'si (AbilityCaster çağırır) ───────────────────────────────

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            int remaining = amount;
            if (Shield > 0)
            {
                int absorbed = Mathf.Min(Shield, remaining);
                Shield    -= absorbed;
                remaining -= absorbed;
            }

            if (remaining <= 0) { OnStatsChanged?.Invoke(this); return; }

            if (_card != null)
            {
                // Kart kendi Defense'ini uygular ve OnHPChanged ile event akışını tetikler.
                _card.TakeDamage(remaining);
            }
            else
            {
                _currentHP = Mathf.Max(0, _currentHP - remaining);
                OnStatsChanged?.Invoke(this);
                if (!IsAlive && !_diedNotified)
                {
                    _diedNotified = true;
                    OnDied?.Invoke(this);
                }
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            if (_card != null) { _card.Heal(amount); return; }

            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            OnStatsChanged?.Invoke(this);
        }

        public void AddShield(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            Shield += amount;
            OnStatsChanged?.Invoke(this);
        }
    }
}
