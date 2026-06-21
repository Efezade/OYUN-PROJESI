using System;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    public enum UnitTeam { Player, Enemy }

    /// <summary>
    /// Hex üstünde duran bir savaş birimi (oyuncu veya düşman).
    /// Can/kalkan tutar, hasar/iyileşme alır, UnitManager'a kaydolur.
    /// Faz 3/4'te (TurnManager, combat) bu katman üstüne kurulacak.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [Header("Kimlik")]
        [SerializeField] private string   _displayName = "Birim";
        [SerializeField] private UnitTeam _team        = UnitTeam.Enemy;

        [Header("Konum")]
        [SerializeField] private HexCoordinate _coord;
        [SerializeField] private float         _heightOffset = 0.8f;

        [Header("Can")]
        [SerializeField, Min(1)] private int _maxHP = 10;

        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager _gridManager;
        [SerializeField] private UnitManager    _unitManager;

        public string        DisplayName => _displayName;
        public UnitTeam       Team        => _team;
        public HexCoordinate  Coordinate  => _coord;
        public int            MaxHP       => _maxHP;
        public int            CurrentHP   { get; private set; }
        public int            Shield      { get; private set; }
        public bool           IsAlive     => CurrentHP > 0;

        /// <summary>HP veya kalkan değişti.</summary>
        public event Action<Unit> OnStatsChanged;
        public event Action<Unit> OnDied;

        private void Awake() => CurrentHP = _maxHP;

        private void OnEnable()
        {
            if (_unitManager != null) _unitManager.Register(this);
        }

        private void OnDisable()
        {
            if (_unitManager != null) _unitManager.Unregister(this);
        }

        private void Start()
        {
            // Grid referansı varsa kendini koordinatına göre konumla.
            // SurfaceHeight ile köprü/engebe karolarında yüzeyin üstüne oturur.
            if (_gridManager != null && _gridManager.TryGetCell(_coord, out HexCell cell))
                transform.position = cell.WorldPosition
                    + Vector3.up * (_heightOffset + cell.SurfaceHeight - HexMetrics.TileHeight);
        }

        // ── Etki API'si (AbilityCaster çağırır) ──────────────────────────────

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

            CurrentHP = Mathf.Max(0, CurrentHP - remaining);
            OnStatsChanged?.Invoke(this);

            if (!IsAlive) OnDied?.Invoke(this);
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            CurrentHP = Mathf.Min(_maxHP, CurrentHP + amount);
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
