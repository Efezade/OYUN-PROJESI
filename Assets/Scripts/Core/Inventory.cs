using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// OYUNCUNUN ENVANTERİ (Efe'nin isteği 2026-10-03): çantadaki eşyalar, pot yığınları ve her
    /// karakterin taktığı eşyalar. Market buraya ekler, ÇANTA buradan okur ve sürükle-bırak ile
    /// yazar, yerleştirme savaşa inen birime <see cref="TraitsFor"/> ile eşya etkilerini verir.
    ///
    /// KARAKTER BAŞINA <see cref="SlotsPerCharacter"/> YUVA = vücut bölgeleri (<see cref="EquipSlot"/>:
    /// kafa · boyun · gövde · sağ kol · sol kol · ayak). Eşya YALNIZ kendi bölgesine takılır. Takılı
    /// eşya çantadan çıkar; yuvadan çıkarılınca çantaya döner; dolu yuvaya bırakılan eşya
    /// oradakiyle YER DEĞİŞTİRİR.
    ///
    /// KALICI: bölüm kaybında roster korunduğu gibi envanter de korunur (eşyaya harcanan öz
    /// zaten "dönüştürülmüş" öz sayılır — ham öz kaybolur, eşya kalmaz diye bir kural yok).
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public const int SlotsPerCharacter = EquipSlots.Count;

        /// <summary>Eşya bu yuvaya takılabilir mi (bölge eşleşmesi)?</summary>
        public static bool Fits(ItemSO item, int slot) => item != null && EquipSlots.IndexOf(item.Slot) == slot;

        private readonly List<ItemSO> _bag = new();
        private readonly Dictionary<PotionSO, int> _potions = new();
        private readonly Dictionary<CharacterCard, ItemSO[]> _equipped = new();

        /// <summary>Envanter değişti (ÇANTA dinler).</summary>
        public event System.Action OnChanged;

        public IReadOnlyList<ItemSO> BagItems => _bag;

        /// <summary>Elde pot olan türler (ÇANTA → POTLAR listesi).</summary>
        public IEnumerable<KeyValuePair<PotionSO, int>> Potions => _potions;

        public int PotionCount(PotionSO p) => p != null && _potions.TryGetValue(p, out int n) ? n : 0;

        // ── Ekleme / harcama ─────────────────────────────────────────────────

        public void AddItem(ItemSO item)
        {
            if (item == null) return;
            _bag.Add(item);
            OnChanged?.Invoke();
        }

        public void AddPotion(PotionSO potion, int count = 1)
        {
            if (potion == null || count <= 0) return;
            _potions[potion] = PotionCount(potion) + count;
            OnChanged?.Invoke();
        }

        /// <summary>Bir pot düşer (içilince). false = elde yok.</summary>
        public bool TryConsumePotion(PotionSO potion)
        {
            int n = PotionCount(potion);
            if (n <= 0) return false;
            if (n == 1) _potions.Remove(potion); else _potions[potion] = n - 1;
            OnChanged?.Invoke();
            return true;
        }

        // ── Yuvalar ──────────────────────────────────────────────────────────

        public ItemSO EquippedAt(CharacterCard card, int slot)
            => card != null && _equipped.TryGetValue(card, out ItemSO[] s) && slot >= 0 && slot < s.Length ? s[slot] : null;

        /// <summary>
        /// Çantadaki eşyayı karakterin yuvasına takar. Yuva doluysa oradaki eşya ÇANTAYA döner.
        /// Eşya başka bir karakterin yuvasındaysa önce oradan alınır (karakterden karaktere sürükleme).
        /// </summary>
        public bool Equip(ItemSO item, CharacterCard card, int slot, CharacterCard fromCard = null, int fromSlot = -1)
        {
            if (item == null || card == null || slot < 0 || slot >= SlotsPerCharacter) return false;
            if (!Fits(item, slot)) return false;                           // yanlış bölge (kafaya kılıç olmaz)

            // Kaynaktan çıkar: başka bir yuva ya da çanta.
            if (fromCard != null && fromSlot >= 0)
            {
                if (EquippedAt(fromCard, fromSlot) != item) return false;
                if (fromCard == card && fromSlot == slot) return false;    // aynı yere bırakıldı
                _equipped[fromCard][fromSlot] = null;
            }
            else if (!_bag.Remove(item)) return false;

            ItemSO[] slots = SlotsOf(card);
            ItemSO previous = slots[slot];
            slots[slot] = item;

            // Yuvadaki eski eşya: karakterden karaktere sürüklemede kaynak yuvaya geçer (takas),
            // çantadan sürüklemede çantaya döner.
            if (previous != null)
            {
                if (fromCard != null && fromSlot >= 0) SlotsOf(fromCard)[fromSlot] = previous;
                else _bag.Add(previous);
            }

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Yuvadaki eşyayı çantaya geri koyar.</summary>
        public bool Unequip(CharacterCard card, int slot)
        {
            ItemSO item = EquippedAt(card, slot);
            if (item == null) return false;
            _equipped[card][slot] = null;
            _bag.Add(item);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Karakterin taktığı eşyaların toplam etkisi (savaşa inişte birime verilir).</summary>
        public EvolutionTraits TraitsFor(CharacterCard card)
        {
            var t = new EvolutionTraits();
            if (card == null || !_equipped.TryGetValue(card, out ItemSO[] slots)) return t;
            foreach (ItemSO item in slots) if (item != null) item.AddTo(ref t);
            return t;
        }

        private ItemSO[] SlotsOf(CharacterCard card)
        {
            if (!_equipped.TryGetValue(card, out ItemSO[] s))
                _equipped[card] = s = new ItemSO[SlotsPerCharacter];
            return s;
        }
    }
}
