using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Parti kartlarını yönetir: üretme (öz tarifi), seviye atlama, kayıp takibi.
    /// Tüm öz harcaması EssenceWallet (çok-tipli) üzerinden geçer.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private EssenceWallet _wallet;

        [Header("Başlangıç Partisi (genelde sadece Kam — diğerleri özle üretilir)")]
        [SerializeField] private List<CharacterClassData> _startingClasses = new();

        [Tooltip("Seviye atlama maliyetinin harcanacağı öz türü (geçici — çok-tipliye sonra genişler).")]
        [SerializeField] private EssenceType _levelUpCostType = EssenceType.Toprak;

        private readonly List<CharacterCard> _party = new();

        public IReadOnlyList<CharacterCard> Party => _party;

        /// <summary>Seviye atlayan kart</summary>
        public event Action<CharacterCard>       OnCardLeveledUp;
        /// <summary>HP 0'a düşen kart</summary>
        public event Action<CharacterCard>       OnCardDied;
        /// <summary>Parti tamamen silindi → Game Over</summary>
        public event Action                      OnPartyWiped;
        /// <summary>Roster değişti (yeni birim üretildi) → UI yenilensin.</summary>
        public event Action                      OnRosterChanged;

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

        /// <summary>Öz tarifini harcayarak yeni bir asker kartı üretir (savaş öncesi yerleştirme ekranı).
        /// Başarıyı döndürür.</summary>
        public bool TryCreate(UnitRecipe recipe)
        {
            if (recipe == null || recipe.UnitClass == null) return false;
            if (_wallet == null || !_wallet.TrySpend(recipe.Cost))
            {
                Debug.Log($"[Party] {recipe.DisplayName} için yeterli öz yok.");
                return false;
            }

            CharacterCard card = AddCard(recipe.UnitClass);
            OnRosterChanged?.Invoke();
            Debug.Log($"[Party] {recipe.DisplayName} üretildi.");
            return card != null;
        }

        // ── Seviye atlama ─────────────────────────────────────────────────────

        /// <summary>Öz yeterliyse kartı bir seviye yükseltir. Başarı durumunu döndürür.</summary>
        public bool TryLevelUp(CharacterCard card)
        {
            if (card == null || !card.CanLevelUp) return false;

            int cost = card.Data.GetEssenceCost(card.Level + 1);
            var typedCost = new[] { new EssenceAmount(_levelUpCostType, cost) };
            if (_wallet == null || !_wallet.TrySpend(typedCost))
            {
                Debug.Log($"[Party] {card.Data.ClassName} Sv{card.Level + 1} için yeterli öz yok " +
                          $"(gerekli: {cost} {_levelUpCostType}).");
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
