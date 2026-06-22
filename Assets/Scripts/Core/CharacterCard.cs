using System;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bir karakterin anlık oyun durumu.
    /// ScriptableObject (CharacterClassData) veri kaynağı; bu sınıf runtime değişimi tutar.
    /// PartyManager tarafından oluşturulur ve yönetilir.
    /// </summary>
    public class CharacterCard
    {
        public CharacterClassData Data  { get; }
        public int                Level { get; private set; } = 1;

        public int CurrentHP { get; private set; }
        public int MaxHP     => Data.GetMaxHP(Level);
        public int Attack    => Data.GetAttack(Level);
        public int Defense   => Data.GetDefense(Level);
        public int MoveRange   => Data.MoveRange;
        public int Speed       => Data.Speed;
        public int AttackRange => Data.AttackRange;

        public bool IsAlive    => CurrentHP > 0;
        public bool CanLevelUp => Level < CharacterClassData.MaxLevel;

        public event Action<int, int> OnHPChanged;   // current, max
        public event Action<int>      OnLevelChanged; // new level

        public CharacterCard(CharacterClassData data, int level = 1)
        {
            Data      = data;
            Level     = Math.Max(1, Math.Min(level, CharacterClassData.MaxLevel));
            CurrentHP = MaxHP;
        }

        // ── Hasar / İyileşme ─────────────────────────────────────────────────

        public void TakeDamage(int rawAmount)
        {
            int reduced = Math.Max(0, rawAmount - Defense);
            CurrentHP   = Math.Max(0, CurrentHP - reduced);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + Math.Max(0, amount));
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        // ── Seviye atlama (sadece PartyManager çağırır) ───────────────────────

        internal void LevelUp()
        {
            if (!CanLevelUp) return;
            int prevMaxHP = MaxHP;
            Level++;
            int hpGain = MaxHP - prevMaxHP;
            CurrentHP = Math.Min(MaxHP, CurrentHP + hpGain); // HP farkını ekle
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            OnLevelChanged?.Invoke(Level);
        }

        public override string ToString() =>
            $"[{Data.ClassName} Sv{Level}] HP:{CurrentHP}/{MaxHP} ATK:{Attack} DEF:{Defense}";
    }
}
