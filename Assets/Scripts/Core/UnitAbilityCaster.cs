using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// SINIF YETENEKLERİ (Efe'nin isteği 2026-10-03): Kam dışındaki her birimin sınıfına göre ÜÇ
    /// yeteneği var (<see cref="CharacterClassData.ClassAbilities"/>). Birimin turunda 1/2/3 ya da
    /// HUD düğmesiyle seçilir, hedefe tıklanınca kullanılır — normal saldırının YERİNE geçer (eylem
    /// hakkını tüketir) ve bekleme süresine girer.
    ///
    /// AKIŞ: seç → hedef doğrula (menzil · takım · görüş hattı) → yer tutucu animasyon
    /// (<see cref="UnitAbilityFx"/>) → etki "vurduğu an"da uygulanır → bekleme başlar →
    /// <see cref="TurnManager.RegisterUnitAction"/> (zafer/yenilgi + otomatik tur sonu).
    ///
    /// BEKLEME: "N tur bekler" = birimin kendi N turu boyunca kullanılamaz. Evrim (Rahip III gibi)
    /// süreyi kısaltabilir. Savaş başında bütün beklemeler sıfırlanır.
    ///
    /// Kam'ın büyüleri BURADA DEĞİL — onlar davuldan gelir ve mana ister (CombatDrumManager).
    /// </summary>
    [DefaultExecutionOrder(-20)]   // Esc'yi menü gezgininden önce okusun (EscapeKeyClaim)
    public class UnitAbilityCaster : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private TurnManager    _turns;
        [SerializeField] private UnitManager    _units;
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private UnitAbilityFx  _fx;

        [Header("Ölçü")]
        [Tooltip("Bir karonun dünya genişliği (alan halkasının boyu için).")]
        [SerializeField] private float _hexWorldSize = 1.9f;

        private readonly Dictionary<Unit, int[]> _cooldowns = new();

        /// <summary>Seçili yetenek (-1 = yok).</summary>
        public int ArmedIndex { get; private set; } = -1;
        public bool HasArmed => ArmedIndex >= 0;

        /// <summary>Animasyon sürüyor — savaş girdisi kilitli.</summary>
        public bool Busy { get; private set; }

        /// <summary>Durum değişti (HUD yenilensin).</summary>
        public event System.Action OnChanged;

        private void OnEnable()
        {
            if (_turns == null) return;
            _turns.OnUnitTurnBegan += TickCooldown;
            _turns.OnRoundStarted  += HandleRoundStarted;
        }

        private void OnDisable()
        {
            if (_turns == null) return;
            _turns.OnUnitTurnBegan -= TickCooldown;
            _turns.OnRoundStarted  -= HandleRoundStarted;
        }

        private void HandleRoundStarted(int round)
        {
            if (round == 1) { _cooldowns.Clear(); ArmedIndex = -1; }   // yeni savaş
        }

        private void TickCooldown(Unit unit)
        {
            if (unit == null || !_cooldowns.TryGetValue(unit, out int[] cd)) return;
            for (int i = 0; i < cd.Length; i++) if (cd[i] > 0) cd[i]--;
            OnChanged?.Invoke();
        }

        private void Update()
        {
            Unit u = CurrentCaster;
            if (u == null)
            {
                if (HasArmed) Disarm();
                return;
            }

            if      (Input.GetKeyDown(KeyCode.Alpha1)) Arm(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Arm(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Arm(2);
            else if (Input.GetKeyDown(KeyCode.Escape) && HasArmed)
            {
                Disarm();
                EscapeKeyClaim.Claim();   // aynı karede ayarlar açılmasın
            }
        }

        // ── Sorgular (HUD) ───────────────────────────────────────────────────

        /// <summary>Şu an yetenek kullanabilecek birim: oyuncu turunda, Kam DEĞİL, sınıfı belli.</summary>
        public Unit CurrentCaster
        {
            get
            {
                if (Busy || _turns == null || !_turns.IsPlayerTurn) return null;
                Unit u = _turns.CurrentUnit;
                return u != null && !u.IsCommander && u.Card != null && u.Card.Data != null ? u : null;
            }
        }

        public IReadOnlyList<ClassAbility> AbilitiesOf(Unit u)
            => u != null && u.Card != null && u.Card.Data != null
               ? u.Card.Data.ClassAbilities : System.Array.Empty<ClassAbility>();

        public int CooldownLeft(Unit u, int index)
            => u != null && _cooldowns.TryGetValue(u, out int[] cd) && index >= 0 && index < cd.Length ? cd[index] : 0;

        public bool CanUse(Unit u, int index, out string reason)
        {
            reason = "";
            var list = AbilitiesOf(u);
            if (index < 0 || index >= list.Count || list[index] == null) { reason = "Yetenek yok."; return false; }
            if (_turns.CurrentHasActed) { reason = "Bu tur eylem yapıldı."; return false; }
            int cd = CooldownLeft(u, index);
            if (cd > 0) { reason = $"{cd} tur bekliyor."; return false; }
            return true;
        }

        // ── Seçim ────────────────────────────────────────────────────────────

        public void Arm(int index)
        {
            Unit u = CurrentCaster;
            if (u == null) return;
            if (!CanUse(u, index, out string reason)) { _turns.Announce(reason); return; }

            ClassAbility ab = AbilitiesOf(u)[index];
            ArmedIndex = ArmedIndex == index ? -1 : index;    // aynı tuş ikinci kez = vazgeç
            if (HasArmed)
                _turns.Announce(ab.Range == 0
                    ? $"{ab.Name} hazır — herhangi bir karoya tıkla (kendi çevresine)."
                    : $"{ab.Name} hazır — {(ab.TargetsAlly ? "dosta" : "hedefe")} tıkla (Esc iptal).");
            OnChanged?.Invoke();
        }

        public void Disarm()
        {
            if (!HasArmed) return;
            ArmedIndex = -1;
            OnChanged?.Invoke();
        }

        // ── Kullanım ─────────────────────────────────────────────────────────

        /// <summary>Hazır yeteneği bu karoya kullanır (MapInputHandler çağırır). false = geçersiz hedef.</summary>
        public bool TryCastAt(HexCoordinate coord)
        {
            Unit u = CurrentCaster;
            if (u == null || !HasArmed) return false;
            int index = ArmedIndex;
            ClassAbility ab = AbilitiesOf(u)[index];

            int range = ab.Range < 0 ? u.AttackRange : ab.Range;
            HexCoordinate center = range == 0 ? u.Coordinate : coord;
            int dist = u.Coordinate.DistanceTo(center);
            if (dist > range) { _turns.Announce($"Menzil dışı ({dist} > {range})."); return false; }

            Unit target = _units != null ? _units.GetUnitAt(center) : null;

            if (ab.Kind != UnitAbilityKind.AreaBlast)
            {
                bool wantAlly = ab.TargetsAlly;
                if (target == null || !target.IsAlive || (target.Team == UnitTeam.Player) != wantAlly)
                {
                    _turns.Announce(wantAlly ? "Bu yetenek dosta kullanılır." : "Bu yetenek düşmana kullanılır.");
                    return false;
                }
            }

            if (!ab.TargetsAlly && range > 1 && !_turns.HasSight(u.Coordinate, center))
            {
                _turns.Announce("Arada duvar var — görüş hattı kapalı.");
                return false;
            }

            StartCoroutine(Execute(u, ab, index, center, target));
            return true;
        }

        private IEnumerator Execute(Unit u, ClassAbility ab, int index, HexCoordinate center, Unit target)
        {
            Busy       = true;
            ArmedIndex = -1;
            OnChanged?.Invoke();

            Vector3 from = u.transform.position;
            Vector3 to   = WorldOf(center);
            bool melee   = u.Coordinate.DistanceTo(center) <= 1;
            int  damage  = Mathf.Max(1, u.Attack * ab.Power / 100);

            if (target != null && target != u) u.AnnounceAttack(target);   // model saldırı klibi + dönüş

            switch (ab.Kind)
            {
                case UnitAbilityKind.PowerStrike:
                    yield return Strike(u, melee, from, to, ab.Color, () => u.DealDamageTo(target, damage));
                    break;

                case UnitAbilityKind.MultiStrike:
                    for (int h = 0; h < ab.Hits && target != null && target.IsAlive; h++)
                    {
                        yield return Strike(u, melee, from, to, ab.Color, () => u.DealDamageTo(target, damage));
                        yield return new WaitForSeconds(0.08f);
                    }
                    break;

                case UnitAbilityKind.StunStrike:
                    yield return Strike(u, melee, from, to, ab.Color, () =>
                    {
                        u.DealDamageTo(target, damage);
                        if (target != null && target.IsAlive) target.ApplyStun(1);
                    });
                    break;

                case UnitAbilityKind.AreaBlast:
                    if (center != u.Coordinate && _fx != null)
                        yield return _fx.Projectile(from, to, ab.Color, null);
                    if (_fx != null)
                        yield return _fx.Burst(to, (ab.Radius + 0.5f) * _hexWorldSize, ab.Color,
                                               () => HitArea(u, center, ab.Radius, damage));
                    else HitArea(u, center, ab.Radius, damage);
                    break;

                case UnitAbilityKind.HealAlly:
                    if (_fx != null) yield return _fx.Rise(target.transform.position, ab.Color, () => target.Heal(ab.Power));
                    else target.Heal(ab.Power);
                    break;

                case UnitAbilityKind.ShieldAlly:
                    if (_fx != null) yield return _fx.Rise(target.transform.position, ab.Color, () => target.AddShield(ab.Power));
                    else target.AddShield(ab.Power);
                    break;
            }

            // Bekleme: birimin kendi N turu boyunca kullanılamaz (+1: sıradaki tur başı hemen düşürür).
            int wait = Mathf.Max(0, ab.Cooldown - u.Evolution.CooldownReduction);
            if (!_cooldowns.TryGetValue(u, out int[] cd) || cd.Length < AbilitiesOf(u).Count)
                _cooldowns[u] = cd = new int[Mathf.Max(3, AbilitiesOf(u).Count)];
            cd[index] = wait > 0 ? wait + 1 : 0;

            _turns.Announce($"{u.DisplayName}: {ab.Name}!");
            Busy = false;
            OnChanged?.Invoke();
            _turns.RegisterUnitAction();     // eylem harcandı + zafer/yenilgi + otomatik tur sonu
        }

        /// <summary>Tek hedefe vuruş animasyonu: bitişikse hamle, uzaksa mermi.</summary>
        private IEnumerator Strike(Unit u, bool melee, Vector3 from, Vector3 to, Color color, System.Action onHit)
        {
            if (_fx == null) { onHit?.Invoke(); yield break; }
            if (melee) yield return _fx.Lunge(u.transform, to, onHit);
            else       yield return _fx.Projectile(from, to, color, onHit);
        }

        /// <summary>Alan hasarı: merkezin <paramref name="radius"/> karo içindeki DÜŞMANLAR.
        /// Dost ateşi YOK — sınıf yeteneği Kam'ın büyüleri gibi "herkese" vurmaz.</summary>
        private void HitArea(Unit u, HexCoordinate center, int radius, int damage)
        {
            if (_units == null) return;
            var victims = new List<Unit>();
            foreach (Unit v in _units.Units)
                if (v != null && v.IsAlive && v.Team != u.Team && v.Coordinate.DistanceTo(center) <= radius)
                    victims.Add(v);
            foreach (Unit v in victims) u.DealDamageTo(v, damage);
        }

        private Vector3 WorldOf(HexCoordinate c)
        {
            if (_grid != null && _grid.TryGetCell(c, out HexCell cell))
                return cell.WorldPosition + Vector3.up * cell.SurfaceHeight;
            Unit at = _units != null ? _units.GetUnitAt(c) : null;
            return at != null ? at.transform.position : Vector3.zero;
        }
    }
}
