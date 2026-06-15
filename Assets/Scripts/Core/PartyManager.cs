using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Parti kartlarını yönetir: oluşturma, seviye atlama, kayıp takibi.
    /// Tüm öz harcaması bu sınıf üzerinden geçer (EssenceManager'ı doğrudan çağırır).
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private EssenceManager _essenceManager;

        [Header("Başlangıç Partisi")]
        [SerializeField] private List<CharacterClassData> _startingClasses = new();

        private readonly List<CharacterCard> _party = new();

        public IReadOnlyList<CharacterCard> Party => _party;

        /// <summary>Seviye atlayan kart</summary>
        public event Action<CharacterCard>       OnCardLeveledUp;
        /// <summary>HP 0'a düşen kart</summary>
        public event Action<CharacterCard>       OnCardDied;
        /// <summary>Parti tamamen silindi → Game Over</summary>
        public event Action                      OnPartyWiped;

        private void Awake()
        {
            foreach (var data in _startingClasses)
                AddCard(data);
        }

        // ── Kart oluşturma ────────────────────────────────────────────────────

        public CharacterCard AddCard(CharacterClassData data)
        {
            if (data == null) return null;
            var card = new CharacterCard(data);
            _party.Add(card);
            return card;
        }

        // ── Seviye atlama ─────────────────────────────────────────────────────

        /// <summary>Öz yeterliyse kartı bir seviye yükseltir. Başarı durumunu döndürür.</summary>
        public bool TryLevelUp(CharacterCard card)
        {
            if (card == null || !card.CanLevelUp) return false;

            int cost = card.Data.GetEssenceCost(card.Level + 1);
            if (_essenceManager == null || !_essenceManager.TrySpend(cost))
            {
                Debug.Log($"[Party] {card.Data.ClassName} Sv{card.Level + 1} için yeterli öz yok. " +
                          $"Gerekli: {cost}  Mevcut: {_essenceManager?.CurrentEssence ?? 0}");
                return false;
            }

            card.LevelUp();
            OnCardLeveledUp?.Invoke(card);
            Debug.Log($"[Party] {card} — seviye atladı! Öz harcandı: {cost}");
            return true;
        }

        // ── Hasar API (dışarıdan çağrılır — gelecekte CombatSystem kullanacak) ──

        public void ApplyDamage(CharacterCard card, int amount)
        {
            if (card == null || !card.IsAlive) return;
            card.TakeDamage(amount);

            if (!card.IsAlive)
            {
                OnCardDied?.Invoke(card);
                Debug.Log($"[Party] {card.Data.ClassName} hayatını kaybetti!");

                if (!HasSurvivor()) OnPartyWiped?.Invoke();
            }
        }

        public bool HasSurvivor() { foreach (var c in _party) if (c.IsAlive) return true; return false; }

        /// <summary>Belirtilen sınıf adına sahip ilk kartı döndürür.</summary>
        public CharacterCard FindByClass(string className)
        {
            foreach (var c in _party)
                if (c.Data.ClassName == className) return c;
            return null;
        }
    }
}
