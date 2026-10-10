using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KARA AŞI ÇÜRÜMESİ (2026-10-10) — map çapında yayılan baskı mekaniği. Kuralın sayıları:
    /// <see cref="CorruptionConfigSO"/>; hikâyedeki karşılığı da orada.
    ///
    /// AKIŞ:
    ///   • Harita üretilince kaynaklar (<see cref="ChapterMapGenerator.CorruptionSources"/>) çevresiyle
    ///     birlikte çürük başlar.
    ///   • Her GÜN BAŞINDA her etkin kaynak yeni karolara yayılır (kaynağa yakın + gürültülü cephe)
    ///     ve çürük karolar olasılıkla bir kademe derinleşir (1 damar · 2 çürük · 3 kararmış).
    ///   • ZORUNLU görev bitince görevin bölgesindeki ARINABİLİR kaynak arınır: yayılma durur,
    ///     çürüğü her gün geri çekilir. Kalıcı kaynaklar (Zar yırtığı, Kalp) arınmaz.
    ///   • Çöküş (<see cref="MapCollapseManager"/>) karo seçerken <see cref="CollapseWeight"/>'e
    ///     bakar: kararmış karo önce düşer → "karo silinmesi bir sisteme bağlı" (Efe, 2026-09-02).
    ///
    ///   • SAVAŞ: çürük karodan (ya da yanından) girilen savaşta düşmanlar Kara Öz ile güçlenir
    ///     (<see cref="CombatEnemyLevelBonus"/>; <see cref="EnemySpawner"/> okur).
    ///
    /// DURUM KOORDİNATTA tutulur, grid hücresinde değil: savaşa girince grid arena olur. GÖRSEL bu
    /// sınıfın işi değil — <see cref="CorruptionVisuals"/> olayları dinleyip çizer.
    /// Çürüme kapalıysa (kural setinde ve yedekte ayar yoksa) hiçbir şey yapmaz.
    /// </summary>
    public class CorruptionManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager      _grid;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private ActionPointManager  _ap;
        [SerializeField] private GameStateManager    _state;
        [SerializeField] private ChapterNodeManager  _nodes;
        [Tooltip("Bölümün kural seti çürüme ayarını seçer.")]
        [SerializeField] private ChapterProgress     _progress;
        [Tooltip("YEDEK ayar — kural seti çürüme seçmiyorsa. İkisi de boşsa çürüme KAPALI.")]
        [SerializeField] private CorruptionConfigSO  _fallbackConfig;
        [Tooltip("Savaşın girildiği karoyu okumak için (Kara Öz güçlendirmesi).")]
        [SerializeField] private PlayerController    _player;
        [Tooltip("Savaş açılırken 'Kara Öz: düşmanlar +N seviye' uyarısı.")]
        [SerializeField] private NoticeFeed          _notice;

        private sealed class Source
        {
            public HexCoordinate Coord;
            public int  Region;
            public bool Permanent;
            public bool Purified;
        }

        private readonly Dictionary<HexCoordinate, (int level, int owner)> _cells = new();
        private readonly List<Source> _sources = new();
        private int _lastDay;

        /// <summary>Çürüme durumu TOPTAN değişti (yeni harita). Görsel her şeyi yeniden çizer.</summary>
        public event Action OnCorruptionChanged;

        /// <summary>Bu karoların kademesi değişti (yayılma / derinleşme / geri çekilme).</summary>
        public event Action<IReadOnlyCollection<HexCoordinate>> OnCellsChanged;

        /// <summary>Bir aşı kaynağı ARINDI (bölge indisi). UI kutlaması için.</summary>
        public event Action<int> OnSourcePurified;

        public CorruptionConfigSO Config
            => _progress != null && _progress.CurrentRules != null && _progress.CurrentRules.Corruption != null
               ? _progress.CurrentRules.Corruption : _fallbackConfig;

        /// <summary>Bu haritada çürüme işliyor mu?</summary>
        public bool IsActive => Config != null && _sources.Count > 0;

        public int CorruptedCount => _cells.Count;
        public int SourceCount    => _sources.Count;
        public int PurifiedCount
        {
            get { int n = 0; foreach (var s in _sources) if (s.Purified) n++; return n; }
        }
        public int PurifiableCount
        {
            get { int n = 0; foreach (var s in _sources) if (!s.Permanent) n++; return n; }
        }

        /// <summary>Karonun çürüme kademesi (0 = temiz … 3 = kararmış).</summary>
        public int LevelAt(HexCoordinate c) => _cells.TryGetValue(c, out var v) ? v.level : 0;

        /// <summary>Çöküş seçiminde bu karonun ağırlığı (temiz = 1).</summary>
        public float CollapseWeight(HexCoordinate c)
        {
            CorruptionConfigSO cfg = Config;
            if (cfg == null) return 1f;
            return 1f + LevelAt(c) * cfg.CollapseWeightPerLevel;
        }

        /// <summary>Bu bölgenin kaynağı arındı mı? (Kaynağı yoksa false.)</summary>
        public bool IsRegionPurified(int region)
        {
            foreach (var s in _sources) if (s.Region == region) return s.Purified;
            return false;
        }

        /// <summary>Tüm çürük karolar (görsel ilk çizimde gezer).</summary>
        public IEnumerable<HexCoordinate> CorruptedCells => _cells.Keys;

        /// <summary>Şu anki (ya da en son) savaşın girildiği karonun çürüme kademesi.</summary>
        public int CombatCorruptionLevel { get; private set; }

        /// <summary>Şu anki savaşta düşmanlara eklenen seviye (0 = temiz karodan girildi).</summary>
        public int CombatEnemyLevelBonus
            => Config != null ? Config.EnemyLevelBonus(CombatCorruptionLevel) : 0;

        // ── Bağlantı ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_map   != null) _map.OnMapGenerated      += HandleMapGenerated;
            if (_ap    != null) _ap.OnTimeAdvanced       += HandleTimeAdvanced;
            if (_state != null) _state.OnStateChanged    += HandleStateChanged;
            if (_nodes != null) _nodes.OnNodeCompleted   += HandleNodeCompleted;
        }

        private void OnDisable()
        {
            if (_map   != null) _map.OnMapGenerated      -= HandleMapGenerated;
            if (_ap    != null) _ap.OnTimeAdvanced       -= HandleTimeAdvanced;
            if (_state != null) _state.OnStateChanged    -= HandleStateChanged;
            if (_nodes != null) _nodes.OnNodeCompleted   -= HandleNodeCompleted;
        }

        /// <summary>Savaş onay ekranı açılırken (grid hâlâ overworld) savaşın girildiği yerin
        /// kademesi okunur: oyuncunun karosu + yanındaki savaş karoları, en yüksek olanı.
        /// Overworld'e dönünce sıfırlanır.</summary>
        private void HandleStateChanged(GameState s)
        {
            if (s == GameState.Overworld) { CombatCorruptionLevel = 0; return; }
            if (s != GameState.ConfirmMission || !IsActive || _player == null) return;

            HexCoordinate at = _player.CurrentCoord;
            int lvl = LevelAt(at);
            for (int d = 0; d < 6; d++)
            {
                HexCoordinate n = at.GetNeighbor(d);
                if (_grid != null && _grid.TryGetCell(n, out HexCell cell) && cell.CanEnterCombat)
                    lvl = Mathf.Max(lvl, LevelAt(n));
            }
            CombatCorruptionLevel = lvl;
            if (lvl > 0 && CombatEnemyLevelBonus > 0)
            {
                Debug.Log($"[Curume] Savas kademe-{lvl} curuk karodan — dusmanlara +{CombatEnemyLevelBonus} seviye.");
                if (_notice != null)
                    _notice.Post($"KARA ÖZ — çürümüş topraktan girdin: düşmanlar +{CombatEnemyLevelBonus} seviye",
                                 Config.EnemyAuraColor, 5f);
            }
        }

        // ── Yeni harita ──────────────────────────────────────────────────────

        private void HandleMapGenerated()
        {
            ResetState();
            CorruptionConfigSO cfg = Config;
            if (cfg == null || _map == null || !_map.HasRegions) { OnCorruptionChanged?.Invoke(); return; }

            foreach (var src in _map.CorruptionSources)
            {
                _sources.Add(new Source { Coord = src.Coord, Region = src.Region, Permanent = src.Permanent });
                int owner = _sources.Count - 1;

                // Kaynak kararmış, çevresi halka halka hafifleyerek başlar.
                foreach (var c in Ring(src.Coord, cfg.InitialRadius))
                {
                    if (!IsLand(c)) continue;
                    int d = c.DistanceTo(src.Coord);
                    int lvl = Mathf.Max(1, CorruptionConfigSO.MAX_LEVEL - d);
                    if (!_cells.TryGetValue(c, out var cur) || cur.level < lvl) _cells[c] = (lvl, owner);
                }
            }

            Debug.Log($"[Curume] {_sources.Count} kaynak ({PurifiableCount} arinabilir) | baslangic {_cells.Count} karo.");
            if (_notice != null)
                _notice.Post("Bayterek'in kökleri çürüyor: Kara Aşı her gün yayılır. Zorunlu görevler aşı noktalarını arındırır.",
                             cfg.EnemyAuraColor, 6f);
            OnCorruptionChanged?.Invoke();
        }

        private void ResetState()
        {
            _cells.Clear();
            _sources.Clear();
            _lastDay = 0;
            CombatCorruptionLevel = 0;
        }

        // ── Gün döngüsü ──────────────────────────────────────────────────────

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (slot != 0 || day <= _lastDay) return;      // yalnız gün sınırında, bir kez
            _lastDay = day;
            if (!IsActive) return;
            StepDay(day);
        }

        private void StepDay(int day)
        {
            CorruptionConfigSO cfg = Config;
            // Deterministik: aynı harita + aynı gün → aynı yayılma (teşhis edilebilir).
            var rnd = new System.Random(unchecked(_map.CurrentSeed * 7919 + _map.CurrentLayoutSeed * 31 + day * 131));
            var changed = new HashSet<HexCoordinate>();

            for (int i = 0; i < _sources.Count; i++)
            {
                Source src = _sources[i];
                if (src.Purified) { Recede(i, cfg.RecedePerDay, changed); continue; }
                Spread(i, cfg.SpreadFor(day, src.Permanent), rnd, changed);
                Deepen(i, cfg.DeepenChance, rnd, changed);
            }

            if (changed.Count > 0) OnCellsChanged?.Invoke(changed);
            AnnounceDay(day);
        }

        /// <summary>Gün başı bildirimi: çürümenin VAR olduğunu ve nereye yaklaştığını oyuncu bilsin
        /// (Efe 2026-10-10: "morluk vs yok" — mekanik görünmeden işliyordu).</summary>
        private void AnnounceDay(int day)
        {
            if (_notice == null || Config == null) return;
            int near = 0;
            if (_player != null)
                foreach (var c in _cells.Keys) if (c.DistanceTo(_player.CurrentCoord) <= 5) near++;
            int active = 0;
            foreach (var s in _sources) if (!s.Purified) active++;
            string msg = near > 0
                ? $"Kara Aşı yaklaşıyor — çevrende {near} karo çürümüş (mor damar)."
                : $"Kara Aşı yayıldı — {_cells.Count} karo çürük, {active} kaynak etkin. (HARİTA'da mor alanlar)";
            _notice.Post(msg, Config.EnemyAuraColor, 4.5f);
        }

        private static readonly List<(HexCoordinate c, double key)> Frontier = new();

        /// <summary>Kaynağın çürüğüne komşu temiz karolardan, kaynağa yakın + gürültülü
        /// <paramref name="count"/> tanesini çürütür (kademe 1).</summary>
        private void Spread(int owner, int count, System.Random rnd, HashSet<HexCoordinate> changed)
        {
            if (count <= 0) return;
            Source src = _sources[owner];

            Frontier.Clear();
            var seen = new HashSet<HexCoordinate>();
            foreach (var kv in _cells)
            {
                if (kv.Value.owner != owner) continue;
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinate n = kv.Key.GetNeighbor(d);
                    if (_cells.ContainsKey(n) || !seen.Add(n) || !IsLand(n)) continue;
                    // Yakınlık + gürültü: cephe yuvarlak değil, dil dil ilerler.
                    Frontier.Add((n, n.DistanceTo(src.Coord) + rnd.NextDouble() * 2.5));
                }
            }
            if (Frontier.Count == 0) return;
            Frontier.Sort((a, b) => a.key.CompareTo(b.key));

            for (int i = 0; i < count && i < Frontier.Count; i++)
            {
                _cells[Frontier[i].c] = (1, owner);
                changed.Add(Frontier[i].c);
            }
        }

        private static readonly List<HexCoordinate> Tmp = new();

        private void Deepen(int owner, float chance, System.Random rnd, HashSet<HexCoordinate> changed)
        {
            Tmp.Clear();
            foreach (var kv in _cells)
                if (kv.Value.owner == owner && kv.Value.level < CorruptionConfigSO.MAX_LEVEL) Tmp.Add(kv.Key);
            foreach (var c in Tmp)
            {
                if (rnd.NextDouble() >= chance) continue;
                var v = _cells[c];
                _cells[c] = (v.level + 1, owner);
                changed.Add(c);
            }
        }

        private void Recede(int owner, int amount, HashSet<HexCoordinate> changed)
        {
            Tmp.Clear();
            foreach (var kv in _cells) if (kv.Value.owner == owner) Tmp.Add(kv.Key);
            foreach (var c in Tmp)
            {
                var v = _cells[c];
                int lvl = v.level - amount;
                if (lvl <= 0) _cells.Remove(c); else _cells[c] = (lvl, owner);
                changed.Add(c);
            }
        }

        // ── Arınma ───────────────────────────────────────────────────────────

        private void HandleNodeCompleted(ChapterNodeManager.MapNode node)
        {
            if (node == null || node.Type != MapNodeType.Mandatory || !IsActive) return;

            int idx = FindSourceToPurify(node.Coord);
            if (idx < 0) return;

            Source src = _sources[idx];
            src.Purified = true;

            // Anında bir kademe geri çekil — oyuncu görevin etkisini HEMEN görsün.
            var changed = new HashSet<HexCoordinate>();
            Recede(idx, Config.RecedePerDay, changed);
            if (changed.Count > 0) OnCellsChanged?.Invoke(changed);

            string regionName = _map != null && src.Region >= 0 && src.Region < _map.Regions.Count
                ? _map.Regions[src.Region].DisplayName : $"bolge {src.Region}";
            Debug.Log($"[Curume] {regionName} ARINDI ({node.Coord}) — {PurifiedCount}/{PurifiableCount} asi noktasi temiz.");
            OnSourcePurified?.Invoke(src.Region);
        }

        /// <summary>Görevin bölgesindeki arınabilir kaynak; bölgede yoksa yarıçap içindeki en yakını.</summary>
        private int FindSourceToPurify(HexCoordinate at)
        {
            int region = _map != null ? _map.RegionIndexAt(at) : -1;
            for (int i = 0; i < _sources.Count; i++)
                if (!_sources[i].Permanent && !_sources[i].Purified && _sources[i].Region == region) return i;

            int best = -1, bestD = Config.PurifyRadius + 1;
            for (int i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].Permanent || _sources[i].Purified) continue;
                int d = at.DistanceTo(_sources[i].Coord);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        /// <summary>Kara mı? (Void ve sınır dekoru — deniz/sis — çürümez.) Terrain verisinden
        /// okunur, grid'den değil: savaştayken de doğru cevap verir.</summary>
        private bool IsLand(HexCoordinate c)
        {
            if (_map == null) return false;
            string id = _map.TerrainIdAt(c);
            if (TileCatalog.IsVoid(id)) return false;
            TileCatalog.Entry e = TileCatalog.Get(id);
            return e == null || e.Family != TileFamily.Fringe;     // düğüm karoları (palette) kara sayılır
        }

        private static IEnumerable<HexCoordinate> Ring(HexCoordinate center, int radius)
        {
            for (int dq = -radius; dq <= radius; dq++)
                for (int dr = Mathf.Max(-radius, -dq - radius); dr <= Mathf.Min(radius, -dq + radius); dr++)
                    yield return new HexCoordinate(center.Q + dq, center.R + dr);
        }
    }
}
