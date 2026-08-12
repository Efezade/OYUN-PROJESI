using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    public enum CombatResult { Ongoing, PlayerWon, PlayerLost }

    /// <summary>
    /// Savaşın sıra motoru. Combat state'ine girince tüm yaşayan birimleri HIZA GÖRE
    /// (yüksek hız önce; eşitlikte oyuncu önce) tek bir initiative kuyruğuna dizer ve
    /// sırayla tur verir (XCOM/Banner Saga). Oyuncu turunda tıklama ile hareket + saldırı;
    /// düşman turunda basit AI. Win = düşman kalmaz, Lose = oyuncu birimi kalmaz.
    /// Permadeath: ölen birim sahneden silinir. Event-driven; logic-only (görsel CombatHighlighter'da).
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private UnitManager       _unitManager;

        [Header("Tempo")]
        [Tooltip("Düşman turunda hamleler arası kısa gecikme (oyuncu izleyebilsin).")]
        [SerializeField] private float _enemyActionDelay = 0.45f;
        [Tooltip("Ölen birim sahneden silinmeden önce ne kadar kalsın (sn) — ölüm animasyonu oynasın. " +
                 "Ölü birim bu sürede karoyu işgal etmez, hedeflenmez; sadece görseli durur.")]
        [SerializeField, Min(0f)] private float _deathLingerSeconds = 1.2f;

        private readonly List<Unit> _order = new();
        private int  _index;
        private bool _combatActive;
        private bool _busy;             // hareket/AI coroutine sürüyor → oyuncu girdisi kilitli
        private bool _commanderPresent; // savaşta komutan (Kam) var mı → yenilgi koşulunu belirler

        public Unit         CurrentUnit     { get; private set; }
        public CombatResult Result          { get; private set; } = CombatResult.Ongoing;
        public bool         CurrentHasMoved { get; private set; }
        public bool         CurrentHasActed { get; private set; }
        public bool         CombatActive    => _combatActive;
        public bool IsPlayerTurn =>
            _combatActive && !_busy && CurrentUnit != null && CurrentUnit.Team == UnitTeam.Player;

        /// <summary>
        /// Şu andan başlayarak SIRADAKİ birimleri (initiative kuyruğunda ileriye doğru, ölüler
        /// atlanarak) verilen listeye doldurur. İlk eleman = şu an sırası olan birim.
        /// Sıra barı (TurnOrderBarHUD) bunu çizer. Çağıran listeyi tekrar kullanır → çöp üretmez.
        /// Kuyruk tek turdan uzunsa başa sarar (For The King tarzı "sonraki tur" önizlemesi).
        /// </summary>
        public void FillUpcoming(List<Unit> buffer, int count)
        {
            buffer.Clear();
            if (!_combatActive || _order.Count == 0 || count <= 0) return;

            // İki tur ilerisine kadar tara → az birim kaldığında bar boş kalmaz, sonraki turun
            // başı da görünür (For The King'deki sürekli kuyruk hissi).
            int start = Mathf.Max(0, _index);
            int scan  = _order.Count * 2;
            for (int step = 0; step < scan && buffer.Count < count; step++)
            {
                Unit u = _order[(start + step) % _order.Count];
                if (u != null && u.IsAlive) buffer.Add(u);
            }
        }

        /// <summary>Sıra/durum değişti → HUD ve highlighter yenilensin.</summary>
        public event Action               OnTurnChanged;

        /// <summary>TUR (round) = initiative sırasının bir tam turu. Davul temposu buna asılır:
        /// 1 birimin hamlesi değil, herkesin bir kez oynaması bir "tur"dur.</summary>
        public int Round { get; private set; }

        /// <summary>Yeni tur başladı (parametre: tur numarası, 1'den başlar).</summary>
        public event Action<int>          OnRoundStarted;
        /// <summary>Kullanıcıya kısa geri bildirim metni.</summary>
        public event Action<string>       OnMessage;
        /// <summary>Savaş bitti (Win/Lose).</summary>
        public event Action<CombatResult> OnCombatEnded;

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
            UnsubscribeDeaths();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Combat) StartCombat();
            else                           EndCombat();
        }

        // ── Savaş başlangıcı / bitişi ─────────────────────────────────────────

        private void StartCombat()
        {
            BuildOrder();
            if (_order.Count == 0) return;

            _combatActive = true;
            Result        = CombatResult.Ongoing;
            _index        = -1;
            Round         = 0;   // ilk AdvanceTurn'de 1 olur

            // Savaşa bir komutan (Kam) katıldıysa yenilgi = komutan ölümü; aksi halde
            // (komutansız test) yenilgi = tüm oyuncu birimlerinin ölümü.
            _commanderPresent = false;
            foreach (var u in _order) if (u != null && u.IsCommander) { _commanderPresent = true; break; }

            SubscribeDeaths();

            if (CheckEnd()) return; // (teorik) tek taraf varsa hemen bitir
            AdvanceTurn();
        }

        private void EndCombat()
        {
            StopAllCoroutines();
            UnsubscribeDeaths();
            _order.Clear();
            _combatActive   = false;
            _busy           = false;
            CurrentUnit     = null;
            CurrentHasMoved = false;
            CurrentHasActed = false;
            OnTurnChanged?.Invoke();
        }

        private void BuildOrder()
        {
            _order.Clear();
            if (_unitManager == null) return;

            foreach (var u in _unitManager.Units)
                if (u != null && u.IsAlive) _order.Add(u);

            // Hıza göre azalan; eşitlikte oyuncu önce (Team enum: Player=0, Enemy=1).
            _order.Sort((a, b) =>
            {
                int bySpeed = b.Speed.CompareTo(a.Speed);
                return bySpeed != 0 ? bySpeed : ((int)a.Team).CompareTo((int)b.Team);
            });
        }

        // ── Tur akışı ─────────────────────────────────────────────────────────

        private void AdvanceTurn()
        {
            if (!_combatActive) return;
            if (CheckEnd())     return;

            for (int step = 0; step < _order.Count; step++)
            {
                _index = (_index + 1) % _order.Count;
                // Sıra başa sardı → yeni TUR. Davul (CombatDrumManager) bunu dinler.
                if (_index == 0) { Round++; OnRoundStarted?.Invoke(Round); }
                Unit u = _order[_index];
                if (u != null && u.IsAlive) { BeginTurn(u); return; }
            }
            CheckEnd(); // canlı birim kalmadıysa bitir
        }

        private void BeginTurn(Unit unit)
        {
            CurrentUnit     = unit;
            CurrentHasMoved = false;
            CurrentHasActed = false;
            OnTurnChanged?.Invoke();

            if (unit.Team == UnitTeam.Enemy)
                StartCoroutine(EnemyTurn(unit));
            // Oyuncu turu: HandlePlayerClick / EndPlayerTurn bekler.
        }

        // ── Oyuncu eylemleri (MapInputHandler / CombatHUD çağırır) ─────────────

        public void HandlePlayerClick(HexCoordinate coord)
        {
            if (!IsPlayerTurn) return;

            Unit target = _unitManager.GetUnitAt(coord);
            if (target != null && target.Team == UnitTeam.Enemy) { TryAttack(CurrentUnit, target); return; }
            if (target == null)                                   TryMove(CurrentUnit, coord);
        }

        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn) return;
            AdvanceTurn();
        }

        /// <summary>
        /// Kam başarılı bir büyü yaptığında AbilityCaster bunu çağırır: aktif birimin
        /// "saldırı/eylem" hakkını tüketir, win/lose kontrolü + otomatik tur sonu yapar.
        /// (Hasar/mana/etki AbilityCaster'da; burada yalnızca tur defteri tutulur.)
        /// </summary>
        public void RegisterCommanderAction()
        {
            if (!IsPlayerTurn || CurrentHasActed) return;
            CurrentHasActed = true;
            OnTurnChanged?.Invoke();
            if (!CheckEnd()) AutoEndIfDone();
        }

        private void TryMove(Unit unit, HexCoordinate dest)
        {
            if (CurrentHasMoved) { Message("Bu tur zaten hareket etti."); return; }

            List<HexCell> path = BuildPath(unit, dest, out int steps);
            if (path == null)           { Message("Oraya gidilemez.");                          return; }
            if (steps > unit.MoveRange) { Message($"Menzil disi ({steps} > {unit.MoveRange})."); return; }

            CurrentHasMoved = true;
            _busy           = true;
            OnTurnChanged?.Invoke();
            unit.MoveAlongPath(path, () =>
            {
                _busy = false;
                OnTurnChanged?.Invoke();
                AutoEndIfDone();
            });
        }

        private void TryAttack(Unit attacker, Unit target)
        {
            if (CurrentHasActed) { Message("Bu tur zaten saldirdi."); return; }
            int dist = attacker.Coordinate.DistanceTo(target.Coordinate);
            if (dist > attacker.AttackRange)
            {
                Message($"Saldiri menzili disi ({dist} > {attacker.AttackRange}).");
                return;
            }

            CurrentHasActed = true;
            attacker.PerformAttack(target);   // saldırı animasyonu + hasar
            Message($"{attacker.DisplayName} -> {target.DisplayName} ({attacker.Attack} hasar)");
            OnTurnChanged?.Invoke();

            if (!CheckEnd()) AutoEndIfDone();
        }

        private void AutoEndIfDone()
        {
            if (IsPlayerTurn && CurrentHasMoved && CurrentHasActed) AdvanceTurn();
        }

        // ── Düşman AI ─────────────────────────────────────────────────────────

        private IEnumerator EnemyTurn(Unit enemy)
        {
            _busy = true;
            OnTurnChanged?.Invoke();
            yield return new WaitForSeconds(_enemyActionDelay);

            Unit target = NearestPlayer(enemy);
            if (target != null)
            {
                // Saldırı menzilinde değilse hedefe yaklaş.
                if (enemy.Coordinate.DistanceTo(target.Coordinate) > enemy.AttackRange)
                {
                    HexCoordinate dest = ChooseApproach(enemy, target);
                    if (dest != enemy.Coordinate)
                    {
                        List<HexCell> path = BuildPath(enemy, dest, out _);
                        if (path != null)
                        {
                            bool moving = true;
                            enemy.MoveAlongPath(path, () => moving = false);
                            while (moving) yield return null;
                        }
                    }
                    yield return new WaitForSeconds(_enemyActionDelay);
                }

                // Menzile girdiyse saldır.
                if (_combatActive && enemy.IsAlive && target.IsAlive &&
                    enemy.Coordinate.DistanceTo(target.Coordinate) <= enemy.AttackRange)
                {
                    enemy.PerformAttack(target);   // saldırı animasyonu + hasar
                    Message($"{enemy.DisplayName} -> {target.DisplayName} ({enemy.Attack} hasar)");
                    yield return new WaitForSeconds(_enemyActionDelay);
                }
            }

            _busy = false;
            if (!CheckEnd()) AdvanceTurn();
        }

        private Unit NearestPlayer(Unit from)
        {
            Unit best = null;
            int  bestDist = int.MaxValue;
            foreach (var u in _unitManager.Units)
            {
                if (u == null || !u.IsAlive || u.Team != UnitTeam.Player) continue;
                int d = from.Coordinate.DistanceTo(u.Coordinate);
                if (d < bestDist) { bestDist = d; best = u; }
            }
            return best;
        }

        // Hareket menzili içinde hedefe EN YAKIN ulaşılabilir hücreyi seç.
        private HexCoordinate ChooseApproach(Unit mover, Unit target)
        {
            HexCoordinate best     = mover.Coordinate;
            int           bestDist = mover.Coordinate.DistanceTo(target.Coordinate);
            foreach (var c in ComputeReachable(mover, out _))
            {
                int d = c.DistanceTo(target.Coordinate);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        // ── Erişilebilirlik (BFS, birim-engelli) + yol kurma ──────────────────

        /// <summary>mover'ın MoveRange içinde ulaşabileceği boş hücreler (highlight + AI).</summary>
        public List<HexCoordinate> ComputeReachable(Unit mover, out Dictionary<HexCoordinate, HexCoordinate> cameFrom)
        {
            var reachable = new List<HexCoordinate>();
            cameFrom = new Dictionary<HexCoordinate, HexCoordinate>();
            if (mover == null || _grid == null) return reachable;

            var dist  = new Dictionary<HexCoordinate, int> { [mover.Coordinate] = 0 };
            var queue = new Queue<HexCoordinate>();
            queue.Enqueue(mover.Coordinate);

            while (queue.Count > 0)
            {
                HexCoordinate cur = queue.Dequeue();
                int d = dist[cur];
                if (d >= mover.MoveRange) continue;

                foreach (HexCell nb in _grid.GetNeighbors(cur))
                {
                    HexCoordinate nc = nb.Coordinate;
                    if (dist.ContainsKey(nc))                continue;
                    if (!nb.IsWalkable)                      continue;
                    if (_unitManager.GetUnitAt(nc) != null)  continue; // başka birim engeller

                    dist[nc]     = d + 1;
                    cameFrom[nc] = cur;
                    reachable.Add(nc);
                    queue.Enqueue(nc);
                }
            }
            return reachable;
        }

        /// <summary>Mevcut birimin saldırabileceği rakip koordinatları (highlight için).</summary>
        public List<HexCoordinate> ComputeAttackable(Unit attacker)
        {
            var list = new List<HexCoordinate>();
            if (attacker == null || _unitManager == null) return list;
            foreach (var u in _unitManager.Units)
            {
                if (u == null || !u.IsAlive || u.Team == attacker.Team) continue;
                if (attacker.Coordinate.DistanceTo(u.Coordinate) <= attacker.AttackRange)
                    list.Add(u.Coordinate);
            }
            return list;
        }

        // Erişilebilir hedefe hücre yolu (path[0] = başlangıç). Ulaşılamazsa null.
        private List<HexCell> BuildPath(Unit mover, HexCoordinate dest, out int steps)
        {
            steps = 0;
            if (dest == mover.Coordinate) return null;

            ComputeReachable(mover, out var cameFrom);
            if (!cameFrom.ContainsKey(dest)) return null;

            var coords = new List<HexCoordinate> { dest };
            HexCoordinate c = dest;
            while (c != mover.Coordinate && cameFrom.TryGetValue(c, out HexCoordinate prev))
            {
                c = prev;
                coords.Add(c);
            }
            coords.Reverse();
            steps = coords.Count - 1;

            var cells = new List<HexCell>(coords.Count);
            foreach (var cc in coords)
                if (_grid.TryGetCell(cc, out HexCell cell)) cells.Add(cell);
            return cells.Count >= 2 ? cells : null;
        }

        // ── Ölüm / win-lose ───────────────────────────────────────────────────

        private void SubscribeDeaths()
        {
            foreach (var u in _order) if (u != null) u.OnDied += HandleUnitDied;
        }

        private void UnsubscribeDeaths()
        {
            foreach (var u in _order) if (u != null) u.OnDied -= HandleUnitDied;
        }

        private void HandleUnitDied(Unit unit)
        {
            if (unit == null) return;
            Message($"{unit.DisplayName} dustu!");
            // Permadeath — sahneden silinir (OnDisable UnitManager'dan siler; _order'da null'a düşer).
            // Ölüm ANİMASYONU varsa silme gecikir ki klip oynasın: ölü birim IsAlive=false olduğu için
            // bu sürede karoyu işgal etmez, hedeflenmez, tur sırasına girmez (UnitManager/AdvanceTurn
            // hep IsAlive süzer) — yalnız görseli sahnede kalır. Klip yoksa eskisi gibi anında silinir
            // (yoksa ceset boşuna dikilir).
            var anim = unit.GetComponent<CharacterAnimationDriver>();
            float delay = anim != null && anim.HasDeathAnimation ? _deathLingerSeconds : 0f;
            Destroy(unit.gameObject, delay);
        }

        private bool CheckEnd()
        {
            if (!_combatActive) return true;
            if (_unitManager == null) return false;

            if (_unitManager.CountAlive(UnitTeam.Enemy) == 0)
            { Finish(CombatResult.PlayerWon); return true; }

            // Yenilgi: komutan (Kam) varsa onun ölümü; yoksa tüm oyuncu birimleri.
            bool lost = _commanderPresent
                ? !_unitManager.HasAliveCommander()
                : _unitManager.CountAlive(UnitTeam.Player) == 0;
            if (lost) { Finish(CombatResult.PlayerLost); return true; }
            return false;
        }

        private void Finish(CombatResult result)
        {
            Result        = result;
            _combatActive = false;
            CurrentUnit   = null;
            OnTurnChanged?.Invoke();
            Message(result == CombatResult.PlayerWon ? "ZAFER!" : "YENILGI...");
            OnCombatEnded?.Invoke(result);
        }

        private void Message(string text)
        {
            Debug.Log($"[Turn] {text}");
            OnMessage?.Invoke(text);
        }
    }
}
