using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Overworld MAĞAZA (store) karolarını yönetir: satın alınabilir katalog + oyuncu yakınlık sorgusu +
    /// mağaza karolarının üstüne altın işaret koyar. HexGridManager'a dokunmaz (grid sade kalır; karo
    /// <see cref="HexCell.IsStore"/> bayrağını palet <c>isStore</c>'dan alır). MissionManager deseninin
    /// mağaza karşılığı. Öz harcama <c>EssenceWallet</c>, etki uygulama <c>PlayerBuffs</c> — burası ikisini
    /// de bilmez; sadece "yakında mı + neyi satıyoruz" sorusunu yanıtlar (tek yönlü bağımlılık).
    ///
    /// RASTGELE STOK + REROLL (Efe'nin isteği 2026-10-03): market her açılışta değil, her GÜN
    /// eşya ve pot havuzundan nadirliğe göre ağırlıklı rastgele bir stok dizer (<see cref="Stock"/>).
    /// Oyuncu belirli bir öz ödeyerek stoğu baştan dizdirebilir (<see cref="TryReroll"/>) — yeni
    /// dizilişte mümkün olduğunca bir öncekinde olmayanlar gelir. Alınan ürün "SATILDI" olur;
    /// eşya/pot doğrudan ENVANTERE gider (etkisi anında değil, çantadan kullanılır).
    /// </summary>
    public class StoreManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private GameStateManager _stateManager;

        [Header("Katalog")]
        [Tooltip("Mağazada satılan öğeler (kalıcı item + geçici pot). Sıra = ekranda görünüm sırası.")]
        [SerializeField] private List<ShopItemSO> _catalog = new();

        [Tooltip("Oyuncu mağazaya kaç hex yakın olmalı (0 = üstünde, 1 = bitişik).")]
        [SerializeField, Min(0)] private int _openRange = 1;

        [Header("Market stoğu (2026-10-03)")]
        [Tooltip("Markette çıkabilecek eşyalar (kurulum doldurur).")]
        [SerializeField] private List<ItemSO>   _itemPool   = new();
        [Tooltip("Markette çıkabilecek potlar (kurulum doldurur).")]
        [SerializeField] private List<PotionSO> _potionPool = new();
        [SerializeField, Min(0)] private int _itemSlots   = 4;
        [SerializeField, Min(0)] private int _potionSlots = 4;
        [Tooltip("Nadirlik ağırlıkları: Sıradan · Nadir · Destansı.")]
        [SerializeField] private int[] _rarityWeights = { 60, 30, 10 };
        [Tooltip("Stoğu baştan dizdirmenin bedeli (REROLL).")]
        [SerializeField] private EssenceAmount[] _rerollCost =
        {
            new EssenceAmount(EssenceType.Tas, 3), new EssenceAmount(EssenceType.Doga, 3)
        };
        [Tooltip("Yeni gün başlayınca stok ücretsiz yenilensin mi?")]
        [SerializeField] private bool _restockEachDay = true;
        [SerializeField] private ActionPointManager _ap;

        [Header("İşaret Görseli")]
        [SerializeField] private Color _markerColor  = new(1f, 0.82f, 0.2f);
        [SerializeField] private float _markerHeight = 1.6f;
        [SerializeField] private float _markerScale  = 0.3f;

        private readonly List<GameObject> _markers = new();

        /// <summary>Stoktaki tek ürün: eşya YA DA pot.</summary>
        public sealed class StockEntry
        {
            public ItemSO   Item;
            public PotionSO Potion;
            public bool     Sold;

            public bool       IsItem => Item != null;
            public string     Name   => IsItem ? Item.DisplayName : Potion.DisplayName;
            public ItemRarity Rarity => IsItem ? Item.Rarity : Potion.Rarity;
            public IReadOnlyList<EssenceAmount> Price => IsItem ? Item.Price : Potion.Price;
        }

        private readonly List<StockEntry> _stock = new();
        private int _stockDay = -1;

        /// <summary>Şu anki stok (eşyalar önce, potlar sonra).</summary>
        public IReadOnlyList<StockEntry> Stock => _stock;
        public IReadOnlyList<EssenceAmount> RerollCost => _rerollCost;

        /// <summary>Stok değişti (alım / reroll / yeni gün) — market paneli dinler.</summary>
        public event System.Action OnStockChanged;

        public IReadOnlyList<ShopItemSO> Catalog => _catalog;
        public int OpenRange => _openRange;

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
            if (_ap           != null) _ap.OnTimeAdvanced           += HandleTimeAdvanced;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
            if (_ap           != null) _ap.OnTimeAdvanced           -= HandleTimeAdvanced;
        }

        private void Start()
        {
            RebuildMarkersDeferred();
            Restock(avoidCurrent: false);
            _stockDay = _ap != null ? _ap.CurrentDay : 1;
        }

        // ── Stok ─────────────────────────────────────────────────────────────

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (!_restockEachDay || day == _stockDay) return;
            _stockDay = day;
            Restock(avoidCurrent: true);
            Debug.Log($"[Market] Gun {day} — stok yenilendi.");
        }

        /// <summary>Öz ödeyip stoğu baştan dizdirir. false = öz yetmiyor.</summary>
        public bool TryReroll(EssenceWallet wallet)
        {
            if (wallet != null && !wallet.TrySpend(_rerollCost)) return false;
            Restock(avoidCurrent: true);
            Debug.Log("[Market] REROLL — stok yeniden dizildi.");
            return true;
        }

        /// <summary>Stoktaki ürünü alır: öz düşer, ürün envantere gider, yer "SATILDI" olur.</summary>
        public bool TryBuy(int index, EssenceWallet wallet, Inventory inventory)
        {
            if (index < 0 || index >= _stock.Count || inventory == null) return false;
            StockEntry e = _stock[index];
            if (e.Sold) return false;
            if (wallet != null && !wallet.TrySpend(e.Price)) return false;

            if (e.IsItem) inventory.AddItem(e.Item); else inventory.AddPotion(e.Potion);
            e.Sold = true;
            OnStockChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Stoğu havuzdan dizer: her yuva için önce NADİRLİK zarı (ağırlıklı), sonra o nadirlikten
        /// rastgele bir ürün. Aynı ürün iki kez çıkmaz. <paramref name="avoidCurrent"/> açıksa şu
        /// anki stokta olanlar mümkün olduğunca dışarıda tutulur — reroll "aynı rafı" getirmesin.
        /// </summary>
        private void Restock(bool avoidCurrent)
        {
            var oldItems   = new HashSet<ItemSO>();
            var oldPotions = new HashSet<PotionSO>();
            if (avoidCurrent)
                foreach (var e in _stock) { if (e.Item != null) oldItems.Add(e.Item); if (e.Potion != null) oldPotions.Add(e.Potion); }

            _stock.Clear();
            foreach (ItemSO i in Pick(_itemPool, _itemSlots, oldItems, x => x.Rarity))
                _stock.Add(new StockEntry { Item = i });
            foreach (PotionSO p in Pick(_potionPool, _potionSlots, oldPotions, x => x.Rarity))
                _stock.Add(new StockEntry { Potion = p });

            OnStockChanged?.Invoke();
        }

        private List<T> Pick<T>(List<T> pool, int count, HashSet<T> avoid, System.Func<T, ItemRarity> rarityOf)
            where T : Object
        {
            var result = new List<T>();
            if (pool == null || pool.Count == 0) return result;

            var candidates = new List<T>();
            foreach (var x in pool) if (x != null && !avoid.Contains(x)) candidates.Add(x);
            // Havuz küçükse "öncekinden kaçın" kuralı gevşer — yuva boş kalmasın.
            if (candidates.Count < count)
                foreach (var x in pool) if (x != null && avoid.Contains(x)) candidates.Add(x);

            for (int n = 0; n < count && candidates.Count > 0; n++)
            {
                ItemRarity want = RollRarity();
                var ofRarity = candidates.FindAll(x => rarityOf(x) == want);
                var from = ofRarity.Count > 0 ? ofRarity : candidates;   // o nadirlik tükendiyse herhangi biri
                T chosen = from[Random.Range(0, from.Count)];
                result.Add(chosen);
                candidates.Remove(chosen);
            }
            return result;
        }

        private ItemRarity RollRarity()
        {
            int total = 0;
            for (int i = 0; i < 3; i++) total += i < _rarityWeights.Length ? Mathf.Max(0, _rarityWeights[i]) : 0;
            if (total <= 0) return ItemRarity.Siradan;
            int roll = Random.Range(0, total);
            for (int i = 0; i < 3; i++)
            {
                int w = i < _rarityWeights.Length ? Mathf.Max(0, _rarityWeights[i]) : 0;
                if (roll < w) return (ItemRarity)i;
                roll -= w;
            }
            return ItemRarity.Siradan;
        }

        // ── Market DÜĞÜMLERİ (TASK-006) ─────────────────────────────────────
        // Kullanıcı kararı (2026-07-28): market düğümü ile boyalı "magaza" karosu AYNI dükkânı açar.
        // ChapterNodeManager düğüm karolarını buraya bildirir; boyama yolu aynen çalışmaya devam eder.
        private readonly List<HexCoordinate> _nodeStoreCoords = new();
        private bool _nodeStoresOpen = true;   // gündüz açık / gece kapalı

        /// <summary>Market düğümlerinin karolarını bildir (ChapterNodeManager çağırır).</summary>
        public void SetNodeStores(IEnumerable<HexCoordinate> coords, bool open)
        {
            _nodeStoreCoords.Clear();
            if (coords != null) _nodeStoreCoords.AddRange(coords);
            _nodeStoresOpen = open;
        }

        /// <summary>Gündüz/gece geçişinde market düğümlerinin açık/kapalı durumunu güncelle.</summary>
        public void SetNodeStoresOpen(bool open) => _nodeStoresOpen = open;

        /// <summary>Oyuncu bir mağaza karosunun ya da AÇIK bir market düğümünün
        /// <see cref="_openRange"/> menzilinde mi?</summary>
        public bool IsPlayerNearStore(HexCoordinate from)
        {
            if (_nodeStoresOpen)
                foreach (var c in _nodeStoreCoords)
                    if (from.DistanceTo(c) <= _openRange) return true;

            if (_grid == null || _grid.Cells == null) return false;
            foreach (var cell in _grid.Cells.Values)
                if (cell.IsStore && from.DistanceTo(cell.Coordinate) <= _openRange)
                    return true;
            return false;
        }

        // Karolar ada yüklendikten SONRA oluştuğu için işaret kurulumunu bir kare ertele.
        private void RebuildMarkersDeferred()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null; // grid karoları kursun
            RebuildMarkers();
        }

        private void RebuildMarkers()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();

            if (_grid == null || _grid.Cells == null) return;
            float hexSize = _grid.HexSize;
            var block = new MaterialPropertyBlock();

            foreach (var cell in _grid.Cells.Values)
            {
                if (!cell.IsStore) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"StoreMarker_{cell.Coordinate}";
                go.transform.SetParent(transform);
                go.transform.position   = cell.Coordinate.ToWorldPosition(hexSize) + Vector3.up * _markerHeight;
                go.transform.localScale = Vector3.one * _markerScale;

                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // tıklama zemine geçsin

                var mr = go.GetComponent<MeshRenderer>();
                mr.GetPropertyBlock(block);
                block.SetColor("_BaseColor", _markerColor);
                block.SetColor("_Color",     _markerColor);
                mr.SetPropertyBlock(block);

                _markers.Add(go);
            }

            HandleStateChanged(_stateManager != null ? _stateManager.State : GameState.Overworld);
        }

        private void HandleStateChanged(GameState state)
        {
            bool show = state == GameState.Overworld;
            foreach (var go in _markers)
                if (go != null) go.SetActive(show);
        }
    }
}
