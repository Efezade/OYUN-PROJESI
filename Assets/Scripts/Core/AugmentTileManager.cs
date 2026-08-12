using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN KOYDUĞU KAROLARIN BEYNİ — kartta yazan şeyi tahtada GERÇEKTEN yapan yer.
    ///
    /// Eskiden (2026-08-12 öncesi) davul karosu konunca yalnız GÖRSEL değişiyordu: karo boyanıyor,
    /// yürünürlüğü güncelleniyor, orada bitiyordu. "Sersemletir", "2 can yeniler", "patlar" gibi
    /// vaatlerin hiçbirini çözümleyen kod yoktu — kullanıcı haklı olarak "karolar çalışmıyor"
    /// dedi. Bu bileşen o boşluğu doldurur.
    ///
    /// SORUMLULUK SINIRI:
    ///   • BURASI: hangi karo kime, ne zaman, ne yapar (kural).
    ///   • <see cref="AugmentFeedback"/>: bunun görünen hâli (halka, yazı, çakış).
    ///   • <see cref="CombatDrumManager"/>: ritim + draft + "nereye konabilir" kuralı.
    ///   • <see cref="Unit"/>/<see cref="TurnManager"/>: statı ve turu TAŞIR, karo bilmez.
    /// Tek yönlü akış (CLAUDE.md §2): karo sistemi savaşı çağırır, savaş karo sistemini bilmez.
    ///
    /// ZAMANLAMA — her tetikleyicinin tek bir kancası var:
    ///   Aura      → <see cref="RefreshAuras"/> (tur değişimi + her adım): girenin statı artar,
    ///               çıkanınki eski hâline döner; inisiyatif değişirse SIRA yeniden dizilir.
    ///   TurnStart → <c>TurnManager.OnUnitTurnBegan</c> (can/mana/aksiyon)
    ///   OnEnter   → <c>Unit.OnEnteredCell</c> (tuzak/diken/buz — alana GİRİŞTE bir kez)
    ///   OnDamaged → <c>Unit.OnDamaged</c> (üstündeki birim vurulunca fıçı patlar)
    ///   Fuse      → <c>TurnManager.OnRoundStarted</c> (sayaç biter, bomba patlar)
    ///   Terrain   → karo boyanınca palet zaten yürünmezliği veriyor; ek çözümleme yok.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class AugmentTileManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private UnitManager       _units;
        [SerializeField] private TurnManager       _turns;
        [SerializeField] private GameStateManager  _state;
        [Tooltip("Opsiyonel — halka/yazı geri bildirimi. Boşsa mekanik yine çalışır, sessiz olur.")]
        [SerializeField] private AugmentFeedback   _fx;
        [Tooltip("Opsiyonel — Davul Taşı'nın mana vermesi için.")]
        [SerializeField] private KamManaManager    _mana;

        [Header("Geri bildirim renkleri")]
        [SerializeField] private Color _buffColor   = new(0.30f, 0.90f, 0.78f);  // ruh teali (tema)
        [SerializeField] private Color _debuffColor = new(0.95f, 0.55f, 0.25f);
        [SerializeField] private Color _healColor   = new(0.45f, 0.95f, 0.50f);
        [SerializeField] private Color _damageColor = new(1f,    0.35f, 0.28f);
        [SerializeField] private Color _stunColor   = new(0.55f, 0.85f, 1f);

        /// <summary>Tahtaya konmuş TEK bir davul karosu (çok karolu kartlarda tek kayıt, çok karo).</summary>
        private sealed class Placed
        {
            public AugmentCatalog.Entry Entry;
            public HexCoordinate        Origin;          // etkinin MERKEZİ (yarıçap buradan ölçülür)
            public readonly List<HexCoordinate> Tiles = new();   // kapladığı karolar (görsel/arazi)
            public int        FuseLeft;                  // Fuse: kaç tur kaldı
            public GameObject AuraRing;                  // kalıcı alan halkası (varsa)
            /// <summary>Patlaması sıraya alındı/gerçekleşti — zincirleme patlamada aynı karo
            /// iki kez patlamasın.</summary>
            public bool       Dead;
            /// <summary>Şu an alanın İÇİNDE olan birimler — "girişte bir kez" kuralı bununla tutulur;
            /// alan içinde yürümeye devam eden birim her adımda yeniden tuzağa basmaz.</summary>
            public readonly HashSet<Unit> Inside = new();
        }

        private readonly List<Placed> _placed = new();
        private readonly HashSet<Unit> _hooked = new();
        private readonly List<Unit>    _scratch = new();

        /// <summary>Tahtada duran aktif davul karosu sayısı (HUD/log).</summary>
        public int ActiveCount => _placed.Count;

        // ── Bağlanma ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_turns != null)
            {
                _turns.OnTurnChanged    += HandleTurnChanged;
                _turns.OnUnitTurnBegan  += HandleUnitTurnBegan;
                _turns.OnRoundStarted   += HandleRoundStarted;
                _turns.OnCombatEnded    += HandleCombatEnded;
            }
            if (_state != null) _state.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_turns != null)
            {
                _turns.OnTurnChanged    -= HandleTurnChanged;
                _turns.OnUnitTurnBegan  -= HandleUnitTurnBegan;
                _turns.OnRoundStarted   -= HandleRoundStarted;
                _turns.OnCombatEnded    -= HandleCombatEnded;
            }
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
            UnhookAll();
        }

        private void HandleStateChanged(GameState state)
        {
            // Savaştan çıkıldı ya da yeni savaşa girildi → tahta sıfır. Arena her savaşta yeniden
            // üretildiği için eski kayıtlar taşınırsa hayalet etki alanları kalırdı.
            if (state != GameState.Combat) ClearAll();
        }

        private void HandleCombatEnded(CombatResult result) => ClearAll();

        /// <summary>Tüm karo kayıtlarını ve birimlerdeki bonusları temizler.</summary>
        public void ClearAll()
        {
            foreach (var p in _placed)
                if (p.AuraRing != null) Destroy(p.AuraRing);
            _placed.Clear();

            foreach (var u in _hooked)
                if (u != null) u.SetTileBonus(default);
            UnhookAll();
        }

        private void UnhookAll()
        {
            foreach (var u in _hooked)
            {
                if (u == null) continue;
                u.OnEnteredCell -= HandleEnteredCell;
                u.OnDamaged     -= HandleUnitDamaged;
            }
            _hooked.Clear();
        }

        /// <summary>Yeni doğmuş birimleri olaylara bağlar. UnitManager'ın "birim eklendi" olayı
        /// yok; her tazelemede fark alınır (birim sayısı tek haneli, maliyeti yok sayılır).</summary>
        private void HookUnits()
        {
            if (_units == null) return;
            foreach (var u in _units.Units)
            {
                if (u == null || _hooked.Contains(u)) continue;
                u.OnEnteredCell += HandleEnteredCell;
                u.OnDamaged     += HandleUnitDamaged;
                _hooked.Add(u);
            }
        }

        // ── Yerleştirme (CombatDrumManager çağırır) ──────────────────────────

        /// <summary>
        /// Seçilen kartı tahtaya koyar: karoları boyar, kaydı açar, yerleşme animasyonunu oynatır.
        /// <paramref name="coords"/>[0] etkinin MERKEZİDİR (çok karolu kartlarda yalnız merkez
        /// aura yayar — aksi halde 3 karoluk Kutsal Zemin üç kat can yenilerdi).
        /// </summary>
        public void Place(AugmentCatalog.Entry entry, IReadOnlyList<HexCoordinate> coords)
        {
            if (entry == null || coords == null || coords.Count == 0 || _grid == null) return;

            var p = new Placed { Entry = entry, Origin = coords[0], FuseLeft = entry.FuseRounds };

            for (int i = 0; i < coords.Count; i++)
            {
                HexCoordinate c = coords[i];
                p.Tiles.Add(c);
                PaintTile(c, entry.VisualId);
                PlayPlacementAnim(c, entry, i * 0.12f);   // duvar karoları sırayla örülsün
            }

            // Alanda ZATEN duran birimler tuzağa basmış sayılmaz ("üstüne GELEN" diyor kart).
            if (_units != null)
                foreach (var u in _units.Units)
                    if (u != null && u.IsAlive && InArea(p, u.Coordinate)) p.Inside.Add(u);

            _placed.Add(p);

            if (_fx != null)
            {
                Vector3 center = WorldOf(p.Origin);
                Color   col    = ColorFor(entry);
                _fx.Burst(center, RadiusWorld(entry.Radius), col);
                if (entry.Radius > 0 && !entry.IsTerrain)
                    p.AuraRing = _fx.CreateAuraRing(center, RadiusWorld(entry.Radius), col);
            }

            HookUnits();
            RefreshAuras();
            Announce($"{entry.Name} kondu — {entry.Description}");
        }

        private void PaintTile(HexCoordinate coord, string tileId)
        {
            if (_grid.TileMap == null) return;
            _grid.TileMap.SetTileId(coord, tileId);
            _grid.RegenerateCellVisual(coord);   // yürünürlük palete göre burada senkronlanır
        }

        private void PlayPlacementAnim(HexCoordinate coord, AugmentCatalog.Entry entry, float delay)
        {
            if (!_grid.TryGetCell(coord, out HexCell cell) || cell.Visual == null) return;

            // Karo prefabında bileşen yoksa (üreteç henüz koşmamış) burada eklenir → animasyon
            // her hâlükârda oynar, sadece parlayacak "Accent" parçası olmaz.
            var vis = cell.Visual.GetComponent<AugmentTileVisual>()
                   ?? cell.Visual.AddComponent<AugmentTileVisual>();
            vis.SetPulsing(!entry.IsTerrain);    // duvar/moloz nabız atmaz
            vis.PlayPlacement(delay);
        }

        // ── Aura (sürekli stat farkı) ────────────────────────────────────────

        private void HandleTurnChanged() => RefreshAuras();

        /// <summary>
        /// Her birimin ÜSTÜNDE DURDUĞU karolardan gelen toplam stat farkını yeniden hesaplar.
        /// Toplama yöntemi bilinçli: bonus birimde birikmez, HER SEFERİNDE sıfırdan kurulur —
        /// böylece karodan çıkan birim etkiyi kesin olarak kaybeder (birikmeli bir sistemde
        /// "çıkarmayı unutma" hatası kaçınılmazdır).
        /// </summary>
        public void RefreshAuras()
        {
            if (_units == null) return;
            HookUnits();

            bool initiativeChanged = false;

            foreach (var u in _units.Units)
            {
                if (u == null || !u.IsAlive) continue;

                Unit.TileBonus before = u.Bonus;
                Unit.TileBonus b = default;

                foreach (var p in _placed)
                {
                    if (p.Entry.Trigger != AugmentTrigger.Aura) continue;
                    if (!Affects(p.Entry, u) || !InArea(p, u.Coordinate)) continue;

                    switch (p.Entry.Effect)
                    {
                        case AugmentEffect.Damage:     b.Attack     += p.Entry.Magnitude; break;
                        case AugmentEffect.Defense:    b.Defense    += p.Entry.Magnitude; break;
                        case AugmentEffect.Move:       b.Move       += p.Entry.Magnitude; break;
                        case AugmentEffect.Initiative: b.Initiative += p.Entry.Magnitude; break;
                        case AugmentEffect.Range:      b.Range      += p.Entry.Magnitude; break;
                    }
                }

                if (!before.Equals(b))
                {
                    if (before.Initiative != b.Initiative) initiativeChanged = true;
                    u.SetTileBonus(b);
                    ShowBonusDelta(u, before, b);
                }
            }

            // Alanı terk edenler "girişte bir kez" sayacından düşer → geri girince yeniden tetiklenir.
            RefreshInsideSets();

            // Ata Taşı / Ağırlık Taşı sözünü ancak sıra barı değişirse tutmuş olur.
            if (initiativeChanged && _turns != null) _turns.ResortUpcoming();
        }

        private void RefreshInsideSets()
        {
            foreach (var p in _placed)
            {
                if (p.Entry.Trigger != AugmentTrigger.OnEnter) continue;
                _scratch.Clear();
                foreach (var u in p.Inside)
                    if (u == null || !u.IsAlive || !InArea(p, u.Coordinate)) _scratch.Add(u);
                foreach (var u in _scratch) p.Inside.Remove(u);
            }
        }

        // ── Tur başı etkileri (can / mana / aksiyon) ─────────────────────────

        private void HandleUnitTurnBegan(Unit unit)
        {
            if (unit == null || !unit.IsAlive) return;

            foreach (var p in _placed)
            {
                if (p.Entry.Trigger != AugmentTrigger.TurnStart) continue;
                if (!Affects(p.Entry, unit) || !InArea(p, unit.Coordinate)) continue;

                switch (p.Entry.Effect)
                {
                    case AugmentEffect.Regen:
                        unit.Heal(p.Entry.Magnitude);
                        Feedback(p, unit, $"+{p.Entry.Magnitude} CAN", _healColor);
                        break;

                    case AugmentEffect.Mana:
                        // Mana yalnız Kam'ındır — kart da öyle diyor.
                        if (!unit.IsCommander || _mana == null) continue;
                        _mana.RestoreMana(p.Entry.Magnitude);
                        Feedback(p, unit, $"+{p.Entry.Magnitude} MANA", _buffColor);
                        break;

                    case AugmentEffect.ExtraAction:
                        if (_turns == null) continue;
                        _turns.GrantExtraAction(unit, p.Entry.Magnitude);
                        Feedback(p, unit, $"+{p.Entry.Magnitude} AKSIYON", _buffColor);
                        break;
                }
            }
        }

        // ── Girişte tetiklenenler (tuzak / diken / buz) ──────────────────────

        private void HandleEnteredCell(Unit unit, HexCoordinate coord)
        {
            if (unit == null || !unit.IsAlive) return;

            // ÖNCE eşleşen karoları topla, SONRA uygula. Sırası kritik: hasar birimi öldürebilir,
            // ölüm/patlama _placed listesini değiştirir — liste üstünde gezerken uygulasaydık
            // "koleksiyon değişti" istisnası atardı (sessiz savaş kilitlenmesi).
            _enterScratch.Clear();
            foreach (var p in _placed)
            {
                if (p.Entry.Trigger != AugmentTrigger.OnEnter || p.Dead) continue;
                if (!Affects(p.Entry, unit)) continue;
                if (!InArea(p, coord)) continue;
                if (!p.Inside.Add(unit)) continue;          // zaten içerideydi → tekrar tetiklenmez
                _enterScratch.Add(p);
            }

            foreach (var p in _enterScratch)
            {
                if (!unit.IsAlive) break;

                switch (p.Entry.Effect)
                {
                    case AugmentEffect.EntryDamage:
                        unit.TakeDamage(p.Entry.Magnitude);
                        Feedback(p, unit, $"-{p.Entry.Magnitude} CAN", _damageColor);
                        Announce($"{unit.DisplayName} {p.Entry.Name} uzerinde {p.Entry.Magnitude} hasar aldi.");
                        break;

                    case AugmentEffect.Stun:
                        unit.ApplyStun(Mathf.Max(1, p.Entry.Magnitude));
                        Feedback(p, unit, "SERSEMLEDI", _stunColor);
                        Announce($"{unit.DisplayName} {p.Entry.Name} uzerinde SERSEMLEDI — siradaki turunu kaybeder.");
                        break;
                }

                if (p.Entry.OneShot) Consume(p);   // buz kabuğu: ilk gireni dondurur, kırılır
            }

            if (_enterScratch.Count > 0) RefreshAuras();
        }

        private readonly List<Placed>  _enterScratch = new();
        private readonly List<Placed>  _fuseScratch  = new();
        private readonly Queue<Placed> _blastQueue   = new();
        private bool _resolvingBlasts;

        // ── Hasarla tetiklenen (ateş fıçısı) ─────────────────────────────────

        private void HandleUnitDamaged(Unit unit, int amount)
        {
            if (unit == null) return;

            // Önce bul, sonra patlat: patlama _placed'i değiştirir, üstünde gezerken olmaz.
            Placed barrel = null;
            foreach (var p in _placed)
            {
                if (p.Entry.Trigger != AugmentTrigger.OnDamaged || p.Dead) continue;
                // Fıçı, ÜSTÜNDEKİ birim vurulunca patlar — yanındaki değil.
                if (unit.Coordinate != p.Origin) continue;
                barrel = p;
                break;   // bir karoda tek fıçı olur
            }
            if (barrel != null) QueueBlast(barrel);
        }

        // ── Fitil (ruh bombası) ──────────────────────────────────────────────

        private void HandleRoundStarted(int round)
        {
            _fuseScratch.Clear();
            foreach (var p in _placed)
            {
                if (p.Entry.Trigger != AugmentTrigger.Fuse || p.Dead) continue;
                p.FuseLeft--;
                if (p.FuseLeft <= 0) { _fuseScratch.Add(p); continue; }

                // Son tur uyarısı: patlamanın haber verilmesi onu taktik yapar, sürpriz değil.
                Announce($"{p.Entry.Name} 1 tur sonra patliyor!");
                if (_fx != null) _fx.Burst(WorldOf(p.Origin), RadiusWorld(p.Entry.Radius) * 0.6f, _debuffColor);
            }
            foreach (var p in _fuseScratch) QueueBlast(p);
        }

        // ── Patlama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Patlamayı SIRAYA alır ve sırayı boşaltır. Neden kuyruk: patlama hasar verir, hasar
        /// başka bir fıçıyı tetikleyebilir (zincir). Doğrudan özyineleme yapsaydık aynı geçici
        /// listeler iç içe iki patlama tarafından ezilirdi — zincir patlamada karo iki kez
        /// patlar ya da hiç patlamazdı.
        /// </summary>
        private void QueueBlast(Placed p)
        {
            if (p == null || p.Dead) return;
            p.Dead = true;                       // artık ikinci kez sıraya girmez
            _blastQueue.Enqueue(p);

            if (_resolvingBlasts) return;        // dıştaki döngü devralır
            _resolvingBlasts = true;
            while (_blastQueue.Count > 0) Explode(_blastQueue.Dequeue());
            _resolvingBlasts = false;
            RefreshAuras();
        }

        private void Explode(Placed p)
        {
            int    dmg    = p.Entry.Magnitude;
            float  radius = RadiusWorld(p.Entry.Radius);
            Vector3 center = WorldOf(p.Origin);

            if (_fx != null) _fx.Burst(center, radius, _damageColor);
            Announce($"{p.Entry.Name} PATLADI — {AugmentCatalog.HexCount(p.Entry.Radius)} hexe {dmg} hasar.");

            if (_units != null)
            {
                // Kopya al: hasar ölüme, ölüm de UnitManager listesinden düşmeye yol açar
                // (koleksiyon değişirken üstünde gezilemez).
                var hit = new List<Unit>();
                foreach (var u in _units.Units)
                    if (u != null && u.IsAlive && u.Coordinate.DistanceTo(p.Origin) <= p.Entry.Radius)
                        hit.Add(u);

                foreach (var u in hit)
                {
                    if (u == null || !u.IsAlive) continue;
                    u.TakeDamage(dmg);
                    if (_fx != null) _fx.FloatingText(u.transform.position, $"-{dmg} CAN", _damageColor);
                }
            }

            Consume(p);
        }

        /// <summary>Karo tükendi: zemine döner, kaydı silinir.</summary>
        private void Consume(Placed p)
        {
            p.Dead = true;
            foreach (var c in p.Tiles) PaintTile(c, TileCatalog.Spent);
            if (p.AuraRing != null) Destroy(p.AuraRing);
            _placed.Remove(p);
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        /// <summary>Kart bu birimi etkiliyor mu? (Karoları Kam koyar → "yandaş" = oyuncu takımı.)</summary>
        private static bool Affects(AugmentCatalog.Entry e, Unit u) => e.Target switch
        {
            AugmentTarget.Allies   => u.Team == UnitTeam.Player,
            AugmentTarget.Enemies  => u.Team == UnitTeam.Enemy,
            _                      => true,
        };

        /// <summary>Koordinat bu karonun etki alanında mı? Arazi kartlarında alan = kapladığı
        /// karolar; diğerlerinde merkeze olan hex mesafesi.</summary>
        private static bool InArea(Placed p, HexCoordinate coord)
        {
            if (p.Entry.IsTerrain) return p.Tiles.Contains(coord);
            return coord.DistanceTo(p.Origin) <= p.Entry.Radius;
        }

        private Vector3 WorldOf(HexCoordinate coord)
        {
            if (_grid != null && _grid.TryGetCell(coord, out HexCell cell))
                return cell.WorldPosition + Vector3.up * cell.SurfaceHeight;
            return Vector3.zero;
        }

        /// <summary>Hex yarıçapının dünya yarıçapı karşılığı (komşu merkezler arası √3 × hexSize).</summary>
        private float RadiusWorld(int hexRadius)
        {
            float size = _grid != null ? _grid.HexSize : 1f;
            return (hexRadius + 0.5f) * Mathf.Sqrt(3f) * size;
        }

        private Color ColorFor(AugmentCatalog.Entry e) => e.Group switch
        {
            AugmentGroup.Kut       => _buffColor,
            AugmentGroup.Kargis    => _debuffColor,
            AugmentGroup.Patlayici => _damageColor,
            _                      => _buffColor,
        };

        /// <summary>Karoyu çaktır + birimin üstüne yazı yaz (ikisi birlikte "bu karo bunu yaptı"
        /// bağını kurar; yalnız yazı olsaydı hangi karodan geldiği belirsiz kalırdı).</summary>
        private void Feedback(Placed p, Unit unit, string text, Color color)
        {
            if (_fx == null) return;
            _fx.FloatingText(unit.transform.position, text, color);
            _fx.Burst(WorldOf(p.Origin), RadiusWorld(p.Entry.Radius) * 0.75f, color);
            FlashTile(p);
        }

        private void FlashTile(Placed p)
        {
            foreach (var c in p.Tiles)
            {
                if (!_grid.TryGetCell(c, out HexCell cell) || cell.Visual == null) continue;
                var vis = cell.Visual.GetComponent<AugmentTileVisual>();
                if (vis != null) vis.Flash(Color.white);
            }
        }

        /// <summary>Aura değişimini kısa bir yazı olarak gösterir ("+2 SAV", "-2 HAR").</summary>
        private void ShowBonusDelta(Unit u, in Unit.TileBonus before, in Unit.TileBonus after)
        {
            if (_fx == null) return;

            string text = "";
            Append(ref text, after.Attack     - before.Attack,     "HASAR");
            Append(ref text, after.Defense    - before.Defense,    "SAV");
            Append(ref text, after.Move       - before.Move,       "HAR");
            Append(ref text, after.Initiative - before.Initiative, "HIZ");
            Append(ref text, after.Range      - before.Range,      "MENZIL");
            if (text.Length == 0) return;

            bool positive = after.Attack + after.Defense + after.Move + after.Initiative + after.Range
                          >= before.Attack + before.Defense + before.Move + before.Initiative + before.Range;
            _fx.FloatingText(u.transform.position, text.Trim(), positive ? _buffColor : _debuffColor);
        }

        private static void Append(ref string text, int delta, string label)
        {
            if (delta == 0) return;
            text += $"{(delta > 0 ? "+" : "")}{delta} {label}  ";
        }

        private void Announce(string text)
        {
            if (_turns != null) _turns.Announce(text);
            else Debug.Log($"[Karo] {text}");
        }
    }
}
