using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Prototypes.CombatLab
{
    public enum LabMode { Tactical, Realtime }
    public enum LabPhase { Ready, Player, Enemy, Augment, Won, Lost }
    public enum LabRole { Kam, Guard, Archer, Raider, Gunner, Boss }
    public enum LabSkill { Pull, Burst }

    public readonly struct LabHex : IEquatable<LabHex>
    {
        public int Q { get; }
        public int R { get; }
        public LabHex(int q, int r) { Q = q; R = r; }
        public int Distance(LabHex other) => (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(Q + R - other.Q - other.R)) / 2;
        public bool Equals(LabHex other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is LabHex h && Equals(h);
        public override int GetHashCode() => Q * 397 ^ R;
        public static LabHex operator +(LabHex a, LabHex b) => new LabHex(a.Q + b.Q, a.R + b.R);
        public Vector2 World(float radius) => new Vector2(Mathf.Sqrt(3f) * radius * (Q + R * .5f), radius * 1.5f * R);
    }

    public sealed class LabUnit
    {
        public int Id { get; internal set; }
        public LabRole Role { get; internal set; }
        public Vector2 Position { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public int Damage { get; internal set; }
        public float Range { get; internal set; }
        public bool Moved { get; internal set; }
        public bool Acted { get; internal set; }
        public float Mark { get; internal set; }
        public float Cooldown { get; internal set; }
        public float Windup { get; internal set; }
        public Vector2 Aim { get; internal set; }
        public bool Alive => Health > 0;
        public bool Ally => Role <= LabRole.Archer;
        public string Name => Role switch { LabRole.Kam => "Kam", LabRole.Guard => "Muhafız", LabRole.Archer => "Okçu", LabRole.Gunner => "Nişancı", LabRole.Boss => "Kapı Bekçisi", _ => "Akıncı" };
    }

    public readonly struct LabFx
    {
        public Vector2 From { get; }
        public Vector2 To { get; }
        public int Amount { get; }
        public int Kind { get; } // 0 hit, 1 chain, 2 pull, 3 burst, 4 heal
        public LabFx(Vector2 from, Vector2 to, int amount, int kind) { From = from; To = to; Amount = amount; Kind = kind; }
    }

    // Isolated simulation: no references to campaign wallets, saves, Unit or TurnManager.
    // Runtime state is owned here; the shared ScriptableObject is never modified.
    public sealed class CombatLabSimulation
    {
        private static readonly LabHex[] Directions = { new LabHex(1, 0), new LabHex(1, -1), new LabHex(0, -1), new LabHex(-1, 0), new LabHex(-1, 1), new LabHex(0, 1) };
        private readonly CombatLabConfig _config;
        private readonly List<LabUnit> _units = new List<LabUnit>();
        private readonly List<LabHex> _cells = new List<LabHex>();
        private readonly HashSet<LabHex> _blocked = new HashSet<LabHex>();
        private int _nextId;
        private int _enemyIndex;
        private float _enemyClock;
        private int _chainBonus;
        private bool _manaOnChain;
        private int _focus = -1;
        public event Action<LabFx> Effect;
        public event Action Changed;
        public LabMode Mode { get; private set; }
        public LabPhase Phase { get; private set; }
        public IReadOnlyList<LabUnit> Units => _units;
        public IReadOnlyList<LabHex> Cells => _cells;
        public LabUnit Kam => _units[0];
        public float Mana { get; private set; }
        public float Elapsed { get; private set; }
        public float EnemyWait { get; private set; }
        public float PullCooldown { get; private set; }
        public float BurstCooldown { get; private set; }
        public int Round { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public int Chains { get; private set; }
        public int Commands { get; private set; }
        public int DamageTaken { get; private set; }
        public int SelectedId { get; private set; }
        public string Hint { get; private set; }
        public bool Paused { get; private set; }
        public bool Active => Phase == LabPhase.Player || Phase == LabPhase.Enemy;
        public int LivingEnemies { get { int n = 0; foreach (var u in _units) if (u.Alive && !u.Ally) n++; return n; } }

        public CombatLabSimulation(CombatLabConfig config)
        {
            _config = config;
            for (int q = -config.ArenaRadius; q <= config.ArenaRadius; q++)
                for (int r = -config.ArenaRadius; r <= config.ArenaRadius; r++)
                { var h = new LabHex(q, r); if (h.Distance(default) <= config.ArenaRadius) _cells.Add(h); }
            _blocked.Add(new LabHex(0, -2)); _blocked.Add(new LabHex(0, 2));
            Phase = LabPhase.Ready;
        }

        public void Start(LabMode mode)
        {
            _units.Clear(); _nextId = 0; _focus = -1; _chainBonus = 0; _manaOnChain = false;
            Mode = mode; Phase = LabPhase.Player; Paused = false; Mana = _config.Mana;
            Elapsed = EnemyWait = PullCooldown = BurstCooldown = 0;
            Round = Wave = 1; Kills = Chains = Commands = DamageTaken = 0; SelectedId = 0;
            Add(LabRole.Kam, new LabHex(-3, 0), _config.KamHealth, 4, 4.3f);
            Add(LabRole.Guard, new LabHex(-2, -1), _config.GuardHealth, 6, 1.7f);
            Add(LabRole.Archer, new LabHex(-3, 1), _config.ArcherHealth, 3, 5.2f);
            SpawnWave();
            Hint = mode == LabMode.Tactical ? "Birlik seç → hareket et → saldır. Q: çek + işaretle, E: patlat. Space: turu bitir." : "WASD: Kam. Q: imlece çek + işaretle, E: çevreni patlat. Tıkla: ekibe hedef ver.";
            Changed?.Invoke();
        }

        private void Add(LabRole role, LabHex h, int hp, int damage, float range)
        {
            h = NearestFree(h.World(_config.TileRadius));
            _units.Add(new LabUnit { Id = _nextId++, Role = role, Position = h.World(_config.TileRadius), Health = hp, MaxHealth = hp, Damage = damage, Range = range, Cooldown = .6f });
        }

        private void SpawnWave()
        {
            if (Wave == 1)
            {
                Add(LabRole.Raider, new LabHex(1, -1), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Raider, new LabHex(2, -1), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Gunner, new LabHex(3, 0), _config.EnemyHealth, 3, 4.5f);
                Add(LabRole.Raider, new LabHex(1, 2), _config.EnemyHealth, 3, 1.7f);
            }
            else if (Wave == 2)
            {
                Add(LabRole.Raider, new LabHex(3, -2), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Raider, new LabHex(3, -1), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Raider, new LabHex(2, 0), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Gunner, new LabHex(3, 1), _config.EnemyHealth, 3, 4.5f);
            }
            else
            {
                Add(LabRole.Boss, new LabHex(2, 0), _config.BossHealth, 5, 2.1f);
                Add(LabRole.Raider, new LabHex(2, -2), _config.EnemyHealth, 3, 1.7f);
                Add(LabRole.Gunner, new LabHex(2, 2), _config.EnemyHealth, 3, 4.5f);
            }
        }

        public LabUnit Find(int id) { foreach (var u in _units) if (u.Id == id) return u; return null; }
        public bool Blocked(LabHex h) => _blocked.Contains(h);
        public bool OnBoard(LabHex h) => h.Distance(default) <= _config.ArenaRadius;
        public LabHex Nearest(Vector2 p)
        {
            var result = default(LabHex); float best = float.PositiveInfinity;
            foreach (var h in _cells) { float d = (h.World(_config.TileRadius) - p).sqrMagnitude; if (d < best) { best = d; result = h; } }
            return result;
        }
        private bool Occupied(LabHex h, int except = -1)
        { foreach (var u in _units) if (u.Alive && u.Id != except && Nearest(u.Position).Equals(h)) return true; return false; }
        private LabHex NearestFree(Vector2 p, int except = -1)
        {
            var result = Nearest(p); float best = float.PositiveInfinity;
            foreach (var h in _cells)
            {
                if (Blocked(h) || Occupied(h, except)) continue;
                float d = (h.World(_config.TileRadius) - p).sqrMagnitude;
                if (d < best) { best = d; result = h; }
            }
            return result;
        }
        public bool HasLine(Vector2 a, Vector2 b)
        {
            foreach (var h in _blocked)
            {
                Vector2 p = h.World(_config.TileRadius), ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude));
                if (Vector2.Distance(p, a + ab * t) < _config.TileRadius * .72f) return false;
            }
            return true;
        }

        public Dictionary<LabHex, int> Reachable(LabUnit unit, int steps)
        {
            var result = new Dictionary<LabHex, int>(); var queue = new Queue<LabHex>();
            var start = Nearest(unit.Position); result.Add(start, 0); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue(); int n = result[at]; if (n >= steps) continue;
                foreach (var dir in Directions)
                {
                    var to = at + dir;
                    if (!OnBoard(to) || Blocked(to) || Occupied(to, unit.Id) || result.ContainsKey(to)) continue;
                    result.Add(to, n + 1); queue.Enqueue(to);
                }
            }
            return result;
        }
        public void Select(int id)
        {
            var u = Find(id); if (u == null || !u.Alive || !u.Ally || Mode != LabMode.Tactical || Phase != LabPhase.Player || Paused) return;
            SelectedId = id; Changed?.Invoke();
        }
        private bool Reject(string message) { Hint = message; Changed?.Invoke(); return false; }
        public bool TryMove(LabHex cell)
        {
            var u = Find(SelectedId);
            if (Paused || Phase != LabPhase.Player || Mode != LabMode.Tactical || u == null || !u.Alive) return false;
            if (u.Moved) return Reject("Bu birlik hareketini kullandı. Başka birlik seç veya saldır.");
            if (cell.Equals(Nearest(u.Position)) || !Reachable(u, _config.MoveHexes).ContainsKey(cell)) return Reject("Bu karoya bu tur ulaşamıyorsun.");
            u.Position = cell.World(_config.TileRadius); u.Moved = true; Commands++; Hint = "Konum değişti. Saldırı / yetenek hakkın ayrı."; Changed?.Invoke(); return true;
        }
        public bool TryAttack(int enemyId)
        {
            if (Paused || Phase != LabPhase.Player) return false;
            var target = Find(enemyId); if (target == null || !target.Alive || target.Ally) return false;
            if (Mode == LabMode.Realtime) { _focus = enemyId; Commands++; Hint = "Ekip hedefi: " + target.Name; Changed?.Invoke(); return true; }
            var u = Find(SelectedId); if (u == null || !u.Alive) return false;
            if (u.Acted) return Reject("Bu birlik saldırısını kullandı.");
            if (!CanHit(u, target)) return Reject("Hedef menzil dışında veya sütunun arkasında.");
            u.Acted = true; Commands++; Strike(u, target); CheckPhase(); Changed?.Invoke(); return true;
        }
        public bool CanHit(LabUnit a, LabUnit b) => a.Alive && b.Alive && Vector2.Distance(a.Position, b.Position) <= a.Range && HasLine(a.Position, b.Position);

        public bool TrySkill(LabSkill skill, Vector2 target)
        {
            if (Paused || Phase != LabPhase.Player || !Kam.Alive) return false;
            if (Mode == LabMode.Tactical && Kam.Acted) return Reject("Kam bu tur eylemini kullandı.");
            int cost = skill == LabSkill.Pull ? _config.PullCost : _config.BurstCost;
            if (Mana + .001f < cost) return Reject("Mana yetersiz. Normal saldırı kullan veya yenilenmesini bekle.");
            if (Mode == LabMode.Realtime && (skill == LabSkill.Pull ? PullCooldown : BurstCooldown) > 0) return Reject("Yetenek henüz hazır değil.");
            if (skill == LabSkill.Pull && (Vector2.Distance(Kam.Position, target) > 5.5f || !HasLine(Kam.Position, target))) return Reject("Çekim merkezi Kam'a yakın ve görünür olmalı.");
            Mana -= cost; Commands++; if (Mode == LabMode.Tactical) Kam.Acted = true;
            if (skill == LabSkill.Pull)
            {
                PullCooldown = _config.PullCooldown;
                foreach (var u in _units)
                {
                    if (!u.Alive || u.Ally || Vector2.Distance(u.Position, target) > _config.PullRadius) continue;
                    Vector2 old = u.Position;
                    Vector2 goal = Vector2.MoveTowards(old, target, 1.8f);
                    if (Mode == LabMode.Tactical) u.Position = NearestFree(goal, u.Id).World(_config.TileRadius);
                    else MoveContinuous(u, goal - old, (goal - old).magnitude);
                    u.Mark = Mode == LabMode.Tactical ? 3f : 6f;
                    Effect?.Invoke(new LabFx(old, u.Position, 0, 2));
                }
                Hint = "ÇEKİM: Düşmanlar işaretlendi. Muhafız veya patlama ile zinciri başlat!";
            }
            else
            {
                BurstCooldown = _config.BurstCooldown;
                Effect?.Invoke(new LabFx(Kam.Position, Kam.Position, 0, 3));
                // Snapshot: chain explosions may kill other targets while resolving this cast.
                foreach (var u in _units) if (!u.Ally && u.Alive && Vector2.Distance(Kam.Position, u.Position) <= _config.BurstRadius && HasLine(Kam.Position, u.Position)) Hit(u, _config.BurstDamage, Kam.Position, true);
                Hint = "PATLAMA: Yakındaki işaretler zincirleme tetiklenir.";
            }
            CheckPhase(); Changed?.Invoke(); return true;
        }
        private void Strike(LabUnit source, LabUnit target)
        {
            if (source.Role == LabRole.Archer) target.Mark = Mode == LabMode.Tactical ? 3f : 6f;
            Hit(target, source.Damage, source.Position, source.Role != LabRole.Archer);
        }
        private void Hit(LabUnit target, int amount, Vector2 source, bool trigger)
        {
            if (!target.Alive) return;
            bool chain = trigger && !target.Ally && target.Mark > 0;
            if (chain) target.Mark = 0;
            int applied = Mathf.Min(target.Health, amount); target.Health -= applied;
            Effect?.Invoke(new LabFx(source, target.Position, applied, 0));
            if (target.Ally) DamageTaken += applied;
            if (!target.Alive && !target.Ally) Kills++;
            if (!chain) return;
            Chains++;
            if (_manaOnChain) Mana = Mathf.Min(_config.Mana, Mana + 1);
            Effect?.Invoke(new LabFx(target.Position, target.Position, 0, 1));
            // Mark is consumed BEFORE recursion, so each mark can detonate only once.
            foreach (var other in _units)
                if (other.Alive && !other.Ally && other.Id != target.Id && Vector2.Distance(other.Position, target.Position) <= _config.ChainRadius)
                    Hit(other, _config.ChainDamage + _chainBonus, target.Position, true);
        }
        public bool EndTurn()
        {
            if (Paused || Mode != LabMode.Tactical || Phase != LabPhase.Player) return false;
            Phase = LabPhase.Enemy; _enemyIndex = 3; _enemyClock = .2f; Commands++;
            Hint = "Düşman karşılığı…"; Changed?.Invoke(); return true;
        }
        public void TogglePause() { if (!Active) return; Paused = !Paused; Changed?.Invoke(); }
        public void Tick(float dt, Vector2 movement)
        {
            if (!Active || Paused) return;
            dt = Mathf.Clamp(dt, 0, .05f); Elapsed += dt;
            if (Mode == LabMode.Tactical)
            {
                if (Phase != LabPhase.Enemy) return;
                EnemyWait += dt; _enemyClock -= dt;
                if (_enemyClock > 0) return;
                while (_enemyIndex < _units.Count && !_units[_enemyIndex].Alive) _enemyIndex++;
                if (_enemyIndex < _units.Count)
                {
                    var enemy = _units[_enemyIndex++]; TacticalEnemy(enemy); _enemyClock = _config.EnemyStepSeconds;
                    CheckPhase(); Changed?.Invoke();
                }
                else
                {
                    Round++; Phase = LabPhase.Player; Mana = Mathf.Min(_config.Mana, Mana + _config.ManaPerRound);
                    foreach (var u in _units) { u.Moved = u.Acted = false; u.Mark = Mathf.Max(0, u.Mark - 1); }
                    if (!Find(SelectedId).Alive) SelectedId = 0;
                    Hint = "Senin turun. Birliklerin sırasını sen belirle."; Changed?.Invoke();
                }
                return;
            }
            Mana = Mathf.Min(_config.Mana, Mana + _config.ManaPerSecond * dt);
            PullCooldown = Mathf.Max(0, PullCooldown - dt); BurstCooldown = Mathf.Max(0, BurstCooldown - dt);
            MoveContinuous(Kam, movement, _config.MoveSpeed * dt);
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                u.Mark = Mathf.Max(0, u.Mark - dt); u.Cooldown = Mathf.Max(0, u.Cooldown - dt);
                if (u.Ally) RealtimeAlly(u, dt); else RealtimeEnemy(u, dt);
                if (!Kam.Alive) break;
            }
            CheckPhase();
        }
        private LabUnit NearestOpponent(LabUnit source)
        {
            var focused = Find(_focus);
            if (source.Ally && focused != null && focused.Alive && Vector2.Distance(source.Position, focused.Position) < 7f) return focused;
            LabUnit found = null; float best = float.PositiveInfinity;
            foreach (var u in _units)
            {
                if (!u.Alive || u.Ally == source.Ally) continue;
                float d = (u.Position - source.Position).sqrMagnitude;
                if (d < best) { best = d; found = u; }
            }
            return found;
        }
        private void TacticalEnemy(LabUnit enemy)
        {
            var target = NearestOpponent(enemy); if (target == null) return;
            if (!CanHit(enemy, target))
            {
                var cells = Reachable(enemy, enemy.Role == LabRole.Boss ? 2 : 3);
                Vector2 best = enemy.Position; float score = float.PositiveInfinity;
                foreach (var pair in cells)
                {
                    Vector2 p = pair.Key.World(_config.TileRadius); float d = Vector2.Distance(p, target.Position);
                    float s = d + (HasLine(p, target.Position) ? 0 : 8);
                    if (s < score) { score = s; best = p; }
                }
                enemy.Position = best;
            }
            if (CanHit(enemy, target)) Strike(enemy, target);
        }
        private void RealtimeAlly(LabUnit u, float dt)
        {
            var target = NearestOpponent(u);
            if (u.Role != LabRole.Kam)
            {
                Vector2 follow = Kam.Position + (u.Role == LabRole.Guard ? new Vector2(1.2f, -.6f) : new Vector2(-1.1f, .7f));
                Vector2 goal = target != null && Vector2.Distance(target.Position, Kam.Position) < 5f ? target.Position : follow;
                float stop = target != null && goal == target.Position ? u.Range * .85f : .35f;
                if (Vector2.Distance(u.Position, goal) > stop) MoveContinuous(u, goal - u.Position, _config.MoveSpeed * .85f * dt);
            }
            if (target != null && u.Cooldown <= 0 && CanHit(u, target)) { Strike(u, target); u.Cooldown = _config.AllyAttackSeconds; }
        }
        private void RealtimeEnemy(LabUnit u, float dt)
        {
            if (u.Windup > 0)
            {
                u.Windup -= dt;
                if (u.Windup <= 0)
                {
                    float radius = u.Role == LabRole.Boss ? 1.7f : .85f;
                    Effect?.Invoke(new LabFx(u.Position, u.Aim, 0, 3));
                    foreach (var ally in _units) if (ally.Ally && ally.Alive && Vector2.Distance(ally.Position, u.Aim) < radius && HasLine(u.Position, ally.Position)) Hit(ally, u.Damage, u.Position, false);
                    u.Cooldown = _config.EnemyAttackSeconds;
                }
                return;
            }
            var target = NearestOpponent(u); if (target == null) return;
            if (!CanHit(u, target)) MoveContinuous(u, target.Position - u.Position, _config.EnemySpeed * dt);
            else if (u.Cooldown <= 0) { u.Aim = target.Position; u.Windup = _config.EnemyWindup; }
        }
        private void MoveContinuous(LabUnit u, Vector2 direction, float distance)
        {
            if (direction.sqrMagnitude < .0001f) return;
            Vector2 delta = direction.normalized * distance;
            Vector2 candidate = u.Position + delta;
            if (WalkablePoint(candidate)) { u.Position = candidate; return; }
            // Slide along blockers; deterministic two-axis fallback avoids walking through pillars.
            candidate = u.Position + new Vector2(delta.x, 0);
            if (WalkablePoint(candidate)) u.Position = candidate;
            else { candidate = u.Position + new Vector2(0, delta.y); if (WalkablePoint(candidate)) u.Position = candidate; }
        }
        private bool WalkablePoint(Vector2 p)
        {
            var nearest = Nearest(p);
            if (Vector2.Distance(nearest.World(_config.TileRadius), p) > _config.TileRadius * .98f) return false;
            foreach (var h in _blocked) if (Vector2.Distance(h.World(_config.TileRadius), p) < _config.TileRadius * .9f) return false;
            return true;
        }
        private void CheckPhase()
        {
            if (!Kam.Alive) { Phase = LabPhase.Lost; Hint = "Kam düştü. Aynı senaryoyu veya diğer yaklaşımı dene."; Changed?.Invoke(); return; }
            if (LivingEnemies != 0) return;
            Phase = Wave == 3 ? LabPhase.Won : LabPhase.Augment;
            Hint = Wave == 3 ? "Kapı açıldı. Şimdi diğer yaklaşımı aynı ekiple karşılaştır." : "Bir geliştirme seç. Sonraki dalgada etkisini hemen göreceksin.";
            Changed?.Invoke();
        }
        public bool ChooseAugment(int choice)
        {
            if (Phase != LabPhase.Augment || choice < 0 || choice > 2) return false;
            if (choice == 0) _chainBonus += 2;
            else if (choice == 1) { _manaOnChain = true; Mana = _config.Mana; }
            else foreach (var u in _units) if (u.Ally) { u.MaxHealth += 4; u.Health = Mathf.Min(u.MaxHealth, u.Health + 10); }
            // A fallen ally remains fallen except when this explicit rally option is chosen.
            foreach (var u in _units) if (u.Ally) { u.Moved = u.Acted = false; if (u.Alive) u.Health = Mathf.Min(u.MaxHealth, u.Health + 4); }
            Wave++; SpawnWave(); Phase = LabPhase.Player; SelectedId = 0; Mana = Mathf.Min(_config.Mana, Mana + 3);
            Hint = "Yeni dalga. İşaretle → yaklaştır → zinciri tetikle."; Changed?.Invoke(); return true;
        }
    }
}
