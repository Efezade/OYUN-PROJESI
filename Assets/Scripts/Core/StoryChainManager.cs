using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Bir hikaye zinciri: sıralı savaş alanları (adım 1 → N).</summary>
    public sealed class StoryChain
    {
        public readonly List<ChapterNodeManager.MapNode> Steps = new();

        public int Length => Steps.Count;

        public int DoneCount
        {
            get { int n = 0; foreach (var s in Steps) if (s.Completed) n++; return n; }
        }
    }

    /// <summary>
    /// HİKAYE ZİNCİRLERİ'ni kurar ve tutar (Efe'nin isteği 2026-10-03).
    ///
    /// İKİ TÜR ZİNCİR:
    ///   • SAVAŞ ALANI ZİNCİRLERİ — zindan + karşılaşma düğümleri harita üretilince 2-5 adımlık
    ///     zincirlere bölünür. Kural: hiçbir alan tek başına kalmaz.
    ///   • ZORUNLU GÖREV ZİNCİRİ — zorunlu görevler kademe sırasıyla (1 → 2 → 3 …) kendi
    ///     zincirlerini kurar; gökten yeni görev düştükçe zincir uzar (Efe'nin seçimi).
    ///
    /// SIRA KİLİDİ YOK (Efe'nin seçimi 2026-10-03): zincir şimdilik veri + minimap çizgisi. Hikaye
    /// metinleri gelince adım sırası anlam kazanacak; o zaman kilit buraya eklenir.
    ///
    /// Bu sınıf yalnız GRUPLAR; çizmek <c>MinimapChainOverlay</c>'in işi. 3B haritada zincir
    /// görünmez (Efe'nin kuralı).
    /// </summary>
    [DefaultExecutionOrder(-60)]   // ChapterNodeManager(-80) düğümleri kurduktan SONRA
    public class StoryChainManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ChapterNodeManager  _nodes;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private PlayerController    _player;
        [Tooltip("Bölümün kural seti kendi zincir ayarını seçebilir.")]
        [SerializeField] private ChapterProgress     _progress;

        [Header("Yedek")]
        [Tooltip("Kural seti zincir ayarı seçmiyorsa bu kullanılır; o da boşsa 2-5 adım, 7 hex.")]
        [SerializeField] private StoryChainConfigSO _config;

        private readonly List<StoryChain> _chains = new();
        private readonly List<ChapterNodeManager.MapNode> _mandatory = new();
        private readonly Dictionary<ChapterNodeManager.MapNode, StoryChain> _chainOf = new();
        private readonly HashSet<ChapterNodeManager.MapNode> _builtFrom = new();

        /// <summary>Savaş alanı zincirleri.</summary>
        public IReadOnlyList<StoryChain> Chains => _chains;

        /// <summary>Zorunlu görevler, kademe sırasıyla (bitmişler dahil).</summary>
        public IReadOnlyList<ChapterNodeManager.MapNode> MandatoryChain => _mandatory;

        /// <summary>Zincirler yeniden kuruldu ya da bir adımı değişti — minimap dinler.</summary>
        public event System.Action OnChainsChanged;

        private StoryChainConfigSO Config
            => _progress != null && _progress.CurrentRules != null && _progress.CurrentRules.StoryChains != null
               ? _progress.CurrentRules.StoryChains : _config;

        private void OnEnable()
        {
            if (_nodes != null) _nodes.OnNodesChanged += Refresh;
        }

        private void OnDisable()
        {
            if (_nodes != null) _nodes.OnNodesChanged -= Refresh;
        }

        private void Start() => Refresh();

        /// <summary>Düğümün savaş alanı zinciri ve adım indeksi (0-tabanlı). Zorunlu görev için
        /// <see cref="MandatoryChain"/> kullanılır.</summary>
        public bool TryGetChain(ChapterNodeManager.MapNode node, out StoryChain chain, out int index)
        {
            index = -1;
            if (node == null || !_chainOf.TryGetValue(node, out chain)) { chain = null; return false; }
            index = chain.Steps.IndexOf(node);
            return true;
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_nodes == null) return;

            var pool = new List<ChapterNodeManager.MapNode>();
            _mandatory.Clear();
            foreach (var n in _nodes.Nodes)
            {
                if (n.Type == MapNodeType.Zindan || n.Type == MapNodeType.Encounter) pool.Add(n);
                else if (n.Type == MapNodeType.Mandatory) _mandatory.Add(n);
            }
            _mandatory.Sort((a, b) => a.Tier.CompareTo(b.Tier));

            // Savaş alanı zincirleri yalnız DÜĞÜM KÜMESİ değişince yeniden kurulur (yeni harita).
            // Görev bitince zincir aynı kalır — yalnız minimap tazelenir.
            if (!SameSet(pool)) BuildCombatChains(pool);

            OnChainsChanged?.Invoke();
        }

        private bool SameSet(List<ChapterNodeManager.MapNode> pool)
        {
            if (pool.Count != _builtFrom.Count) return false;
            foreach (var n in pool) if (!_builtFrom.Contains(n)) return false;
            return true;
        }

        /// <summary>
        /// Savaş alanlarını zincirlere böler. AÇGÖZLÜ ama tekil bırakmayan:
        ///   1. Başlangıç = en az YAKIN komşusu olan alan (kıyıdaki/kenardaki alanlar önce
        ///      alınır, yoksa sona tek başına kalırlardı).
        ///   2. Uzunluk 2-5 arasından zarla; kalan tek bir alan artmayacak şekilde ayarlanır.
        ///   3. Zincir kuyruğa EN YAKIN alanı ekleyerek büyür (bağ mesafesi sınırı içinde).
        ///   4. Yine de tek kalan olursa en yakın zincirin UCUNA eklenir — tekil görev kuralı
        ///      mesafe sınırından önce gelir.
        /// Aynı seed → aynı zincirler. Adım 1, oyuncunun başladığı yere yakın uçtur.
        /// </summary>
        private void BuildCombatChains(List<ChapterNodeManager.MapNode> pool)
        {
            _chains.Clear();
            _chainOf.Clear();
            _builtFrom.Clear();
            foreach (var n in pool) _builtFrom.Add(n);
            if (pool.Count == 0) return;

            StoryChainConfigSO cfg = Config;
            int minLen  = cfg != null ? cfg.MinLength       : 2;
            int maxLen  = cfg != null ? cfg.MaxLength       : 5;
            int maxLink = cfg != null ? cfg.MaxLinkDistance : 7;

            var rnd  = new PythonRandom((_map != null ? _map.CurrentSeed : 0) + 5000);
            var left = new List<ChapterNodeManager.MapNode>(pool);
            rnd.Shuffle(left);

            while (left.Count > 0)
            {
                var start = FewestNeighbours(left, maxLink);
                int target = Mathf.Min(rnd.RandInt(minLen, maxLen), left.Count);
                // Arkada TEK bir alan kalmasın: ya onu da al ya bir eksik al.
                if (left.Count - target == 1) target = target + 1 <= maxLen ? target + 1 : target - 1;

                var chain = new StoryChain();
                chain.Steps.Add(start);
                left.Remove(start);

                while (chain.Length < target)
                {
                    var next = Nearest(left, chain.Steps[chain.Length - 1].Coord, maxLink);
                    if (next == null) break;
                    chain.Steps.Add(next);
                    left.Remove(next);
                }
                _chains.Add(chain);
            }

            MergeSingles(maxLen, maxLink);
            OrientTowardsStart();

            foreach (var c in _chains)
                foreach (var s in c.Steps) _chainOf[s] = c;

            Debug.Log($"[Zincir] {pool.Count} savas alani -> {_chains.Count} hikaye zinciri " +
                      $"({string.Join(", ", _chains.ConvertAll(c => c.Length.ToString()))} adim).");
        }

        /// <summary>
        /// Tek adımlı zinciri başka bir zincirin UCUNA ekler. Önce DOLMAMIŞ (uzunluğu tavanın
        /// altında) zincirlere, bağ mesafesinin iki katına kadar bakılır; bulunamazsa en yakın
        /// zincire eklenir — tavanı aşmak tek görev bırakmaktan iyidir.
        /// </summary>
        private void MergeSingles(int maxLen, int maxLink)
        {
            for (int i = _chains.Count - 1; i >= 0; i--)
            {
                if (_chains[i].Length != 1 || _chains.Count < 2) continue;
                var lone = _chains[i].Steps[0];

                if (!TryNearestEnd(lone, _chains[i], maxLen, maxLink * 2, out StoryChain best, out bool atHead))
                    TryNearestEnd(lone, _chains[i], int.MaxValue, int.MaxValue, out best, out atHead);

                if (best == null) continue;
                if (atHead) best.Steps.Insert(0, lone); else best.Steps.Add(lone);
                _chains.RemoveAt(i);
            }
        }

        private bool TryNearestEnd(ChapterNodeManager.MapNode lone, StoryChain self, int lengthBelow,
                                   int maxDistance, out StoryChain best, out bool atHead)
        {
            best = null; atHead = false;
            int bestD = int.MaxValue;
            foreach (var c in _chains)
            {
                if (c == self || c.Length >= lengthBelow) continue;
                int dHead = lone.Coord.DistanceTo(c.Steps[0].Coord);
                int dTail = lone.Coord.DistanceTo(c.Steps[c.Length - 1].Coord);
                if (dHead <= maxDistance && dHead < bestD) { bestD = dHead; best = c; atHead = true; }
                if (dTail <= maxDistance && dTail < bestD) { bestD = dTail; best = c; atHead = false; }
            }
            return best != null;
        }

        /// <summary>Zincirin 1. adımı oyuncunun başladığı yere YAKIN uç olsun — hikaye oyuncuya
        /// doğru değil, oyuncudan dışarı doğru akar.</summary>
        private void OrientTowardsStart()
        {
            if (_player == null) return;
            HexCoordinate from = _player.CurrentCoord;
            foreach (var c in _chains)
                if (from.DistanceTo(c.Steps[c.Length - 1].Coord) < from.DistanceTo(c.Steps[0].Coord))
                    c.Steps.Reverse();
        }

        private static ChapterNodeManager.MapNode FewestNeighbours(List<ChapterNodeManager.MapNode> left, int maxLink)
        {
            ChapterNodeManager.MapNode best = left[0];
            int bestCount = int.MaxValue;
            foreach (var a in left)
            {
                int count = 0;
                foreach (var b in left)
                    if (a != b && a.Coord.DistanceTo(b.Coord) <= maxLink) count++;
                if (count < bestCount) { bestCount = count; best = a; }
            }
            return best;
        }

        private static ChapterNodeManager.MapNode Nearest(List<ChapterNodeManager.MapNode> left,
                                                          HexCoordinate from, int maxLink)
        {
            ChapterNodeManager.MapNode best = null;
            int bestD = int.MaxValue;
            foreach (var n in left)
            {
                int d = from.DistanceTo(n.Coord);
                if (d > maxLink || d >= bestD) continue;
                bestD = d; best = n;
            }
            return best;
        }
    }
}
