using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// DAVUL TEMPOSU — savaşın ritmi. Belirli turlarda Kam davulu vurur, oyuna 3 KARO KARTI sunulur
    /// (TFT augment seçimi gibi), Kam birini seçer ve **kendi çevresine** yerleştirir.
    ///
    /// TASARIMIN ÇEKİRDEĞİ (kullanıcı kuralı 2026-08-12): karo, haritaya uzaktan bakılıp istenen
    /// yere KONULAMAZ — yalnız **Kam'ın <see cref="PlacementRadius"/> karo çevresine** konur.
    /// Sebebi: aksi halde Kam arkada güvenle durup tahtayı yönetirdi. Bu kural Kam'ı ileri çıkmaya,
    /// yani RİSK ALMAYA zorlar; komutanın konumu her vuruşta yeniden bir karar haline gelir.
    ///
    /// Yarıçap neden 3 (boss 4):
    ///   • Düşman tehdit menzili = hareket (3-4) + saldırı (1) = 4-5 karo.
    ///   • Temas hattı deploy bölgesinden ~5-6 karo uzakta.
    ///   • R=3 ile temas hattına karo koymak için Kam ~3 karo yaklaşmak zorunda → düşman tehdit
    ///     menzilinin İÇİNE girer. R=5-6 olsaydı güvenli mesafeden koyardı, risk sıfırlanırdı.
    ///   • R / Kam hareketi = 3/3 = 1.0 → bir tur repositioning, bir karo yeni erişim. Temiz tempo.
    ///
    /// Bu bileşen yalnız RİTİM + DRAFT + YERLEŞTİRME KURALINI yönetir; kartların etkilerini
    /// çözümlemek (hasar, sıra, sersemletme) savaş sistemlerinin işi.
    /// </summary>
    public class CombatDrumManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private TurnManager        _turns;
        [SerializeField] private UnitManager        _units;
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private CombatMapGenerator _arena;

        [Header("Ritim")]
        [Tooltip("İlk davul vuruşunun turu. 2 önerilir: tur 3'ten başlarsa 4-5 turluk encounter'da " +
                 "mekanik ya hiç görünmez ya bir kez görünür, sistem gibi hissettirmez.")]
        [SerializeField, Min(1)] private int _firstBeatRound = 2;

        [Tooltip("Kaç turda bir davul çalar. 2 → tur 2,4,6,8,10 (encounter 2 vuruş, zindan 3, boss 5).")]
        [SerializeField, Min(1)] private int _beatInterval = 2;

        [Header("Yerleştirme")]
        [Tooltip("Kam'ın kaç karo çevresine yerleştirilebilir. 3 önerilir — düşman tehdit menzili " +
                 "4-5 olduğu için Kam'ı riske sokar. Büyütürsen Kam güvenli mesafeden oynar.")]
        [SerializeField, Min(1)] private int _placementRadius = 3;

        [Tooltip("Boss arenası daha büyük olduğu için yarıçap 1 artar.")]
        [SerializeField, Min(0)] private int _bossRadiusBonus = 1;

        [Header("Draft")]
        [Tooltip("Vuruş başına sunulan kart sayısı.")]
        [SerializeField, Min(2)] private int _choiceCount = 3;

        [Tooltip("Bir kart yuvasının SINIFSAL kartla değişme ihtimali (sahada o sınıf varsa).")]
        [SerializeField, Range(0f, 1f)] private float _classCardChance = 0.15f;

        // ── Durum ────────────────────────────────────────────────────────────
        private readonly List<AugmentCatalog.Entry> _choices   = new();
        private readonly List<HexCoordinate>        _validCells = new();
        private readonly List<string>               _usedIds    = new();   // aynı savaşta tekrar etmesin

        /// <summary>Şu an seçim bekleniyor mu? (UI bu bayrağa bakar, savaş girdisi kilitlenir.)</summary>
        public bool IsChoosing { get; private set; }

        /// <summary>Kart seçildi, yerleştirme bekleniyor mu?</summary>
        public bool IsPlacing { get; private set; }

        public IReadOnlyList<AugmentCatalog.Entry> Choices  => _choices;
        public AugmentCatalog.Entry                Selected { get; private set; }

        /// <summary>Seçilen karonun konabileceği karolar (Kam'ın çevresi, dolu/uygunsuz olanlar hariç).</summary>
        public IReadOnlyList<HexCoordinate> ValidCells => _validCells;

        /// <summary>Bu arenada geçerli yerleştirme yarıçapı.</summary>
        public int PlacementRadius =>
            _placementRadius + (_arena != null && _arena.Arena != null && IsBossArena ? _bossRadiusBonus : 0);

        private bool IsBossArena => _arena != null && _arena.Arena != null && _arena.Arena.Playable >= 100;

        public event System.Action OnChoicesOffered;   // 3 kart hazır → UI paneli aç
        public event System.Action OnPlacementBegan;   // kart seçildi → haritada hedef seç
        public event System.Action<HexCoordinate, AugmentCatalog.Entry> OnAugmentPlaced;

        private void OnEnable()
        {
            if (_turns != null) _turns.OnRoundStarted += HandleRoundStarted;
        }

        private void OnDisable()
        {
            if (_turns != null) _turns.OnRoundStarted -= HandleRoundStarted;
        }

        // ── Ritim ────────────────────────────────────────────────────────────

        private void HandleRoundStarted(int round)
        {
            if (round < _firstBeatRound) return;
            if ((round - _firstBeatRound) % _beatInterval != 0) return;
            if (KamUnit() == null) return;                  // Kam yoksa davul çalmaz

            OfferChoices();
        }

        /// <summary>Yeni savaş başlarken çağrılır (aynı kart havuzu tekrar açılsın).</summary>
        public void ResetForNewBattle()
        {
            _usedIds.Clear();
            _choices.Clear();
            _validCells.Clear();
            Selected   = null;
            IsChoosing = false;
            IsPlacing  = false;
        }

        // ── Draft ────────────────────────────────────────────────────────────

        private void OfferChoices()
        {
            _choices.Clear();

            // Normal dağılım: 1 Kut + 1 Kargış + 1 (Nötr ya da Patlayıcı).
            // Üçü de artı olsaydı seçim "bedava güç" olurdu, karar kalmazdı.
            AddFromGroup(AugmentGroup.Kut);
            AddFromGroup(AugmentGroup.Kargis);
            AddFromGroup(Random.value < 0.5f ? AugmentGroup.Notr : AugmentGroup.Patlayici);

            // Nadir sınıfsal kart: sahada O SINIF varsa bir yuvayı devralır.
            if (_choices.Count > 0 && Random.value < _classCardChance)
            {
                var classCard = PickClassCard();
                if (classCard != null) _choices[Random.Range(0, _choices.Count)] = classCard;
            }

            while (_choices.Count > _choiceCount) _choices.RemoveAt(_choices.Count - 1);

            IsChoosing = true;
            IsPlacing  = false;
            Selected   = null;
            Debug.Log($"[Davul] Tur {_turns.Round} — vuruş! {_choices.Count} kart sunuldu.");
            OnChoicesOffered?.Invoke();
        }

        private void AddFromGroup(AugmentGroup group)
        {
            var pool = new List<AugmentCatalog.Entry>();
            foreach (var e in AugmentCatalog.InGroup(group))
            {
                if (_usedIds.Contains(e.Id)) continue;                   // aynı savaşta tekrar yok
                if (e.NeedsRangedSystem)     continue;                   // menzil/LoS gelmeden çalışmaz
                if (e.RequiresClass != null) continue;                   // sınıfsallar ayrı yoldan
                pool.Add(e);
            }
            if (pool.Count == 0) return;
            _choices.Add(pool[Random.Range(0, pool.Count)]);
        }

        private AugmentCatalog.Entry PickClassCard()
        {
            var onField = new HashSet<string>();
            if (_units != null)
                foreach (var u in _units.Units)
                {
                    // Sinif adi karttan gelir (Unit'in kendi DisplayName'i takma ad olabilir).
                    if (u == null || !u.IsAlive || u.Team != UnitTeam.Player) continue;
                    if (u.Card != null && u.Card.Data != null) onField.Add(u.Card.Data.ClassName);
                }

            var pool = new List<AugmentCatalog.Entry>();
            foreach (var e in AugmentCatalog.InGroup(AugmentGroup.Sinifsal))
            {
                if (_usedIds.Contains(e.Id) || e.NeedsRangedSystem) continue;
                if (e.RequiresClass != null && !onField.Contains(e.RequiresClass)) continue;
                pool.Add(e);
            }
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        // ── Seçim → yerleştirme ──────────────────────────────────────────────

        /// <summary>Kartı seç ve yerleştirme moduna geç (UI çağırır).</summary>
        public bool Choose(int index)
        {
            if (!IsChoosing || index < 0 || index >= _choices.Count) return false;

            Selected   = _choices[index];
            IsChoosing = false;
            IsPlacing  = true;
            BuildValidCells();

            if (_validCells.Count == 0)
            {
                // Kam'ın çevresinde uygun karo yoksa (köşeye sıkışmış) seçim boşa gitmesin.
                Debug.LogWarning("[Davul] Kam'in cevresinde uygun karo yok — kart iade edildi.");
                IsPlacing  = false;
                IsChoosing = true;
                Selected   = null;
                return false;
            }

            Debug.Log($"[Davul] '{Selected.Name}' secildi — {_validCells.Count} gecerli karo " +
                      $"(Kam'in {PlacementRadius} karo cevresi).");
            OnPlacementBegan?.Invoke();
            return true;
        }

        /// <summary>Kam'ın çevresindeki UYGUN karolar. "Uygun" = arenada var, üstünde birim yok,
        /// zaten bir davul karosu değil, ve Kam'ın kendi karosu değil.</summary>
        private void BuildValidCells()
        {
            _validCells.Clear();
            Unit kam = KamUnit();
            if (kam == null || _grid == null) return;

            int r = PlacementRadius;
            for (int dq = -r; dq <= r; dq++)
                for (int dr = Mathf.Max(-r, -dq - r); dr <= Mathf.Min(r, -dq + r); dr++)
                {
                    var c = new HexCoordinate(kam.Coordinate.Q + dq, kam.Coordinate.R + dr);
                    if (c == kam.Coordinate)                   continue;   // Kam kendi ayağının altına koymaz
                    if (!_grid.TryGetCell(c, out HexCell cell)) continue;   // arena dışı
                    if (_units != null && _units.GetUnitAt(c) != null) continue;  // birim var
                    if (!cell.IsWalkable)                      continue;   // duvar/siper üstüne konmaz
                    _validCells.Add(c);
                }
        }

        /// <summary>Seçilen karoyu bu koordinata yerleştirir (harita girdisi çağırır).</summary>
        public bool PlaceAt(HexCoordinate coord)
        {
            if (!IsPlacing || Selected == null) return false;
            if (!_validCells.Contains(coord))
            {
                Debug.Log("[Davul] Bu karo Kam'in menzili disinda ya da dolu.");
                return false;
            }

            ApplyTile(coord, Selected);

            // Çok karolu kartlar (Duvar 3, Kutsal Zemin 3): komşulara doğru yayılır.
            if (Selected.TileCount > 1)
            {
                int placed = 1;
                for (int d = 0; d < 6 && placed < Selected.TileCount; d++)
                {
                    HexCoordinate n = coord.GetNeighbor(d);
                    if (!_validCells.Contains(n)) continue;
                    ApplyTile(n, Selected);
                    placed++;
                }
            }

            _usedIds.Add(Selected.Id);
            var placedEntry = Selected;

            Selected  = null;
            IsPlacing = false;
            _validCells.Clear();
            _choices.Clear();

            OnAugmentPlaced?.Invoke(coord, placedEntry);
            return true;
        }

        private void ApplyTile(HexCoordinate coord, AugmentCatalog.Entry entry)
        {
            if (_grid == null || _grid.TileMap == null) return;
            _grid.TileMap.SetTileId(coord, entry.VisualId);
            _grid.RegenerateCellVisual(coord);   // IsWalkable de palete göre senkronlanır
            Debug.Log($"[Davul] '{entry.Name}' {coord} karosuna kondu. {AugmentCatalog.AreaLabel(entry)}");
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────

        private Unit KamUnit()
        {
            if (_units == null) return null;
            foreach (var u in _units.Units)
                if (u != null && u.IsAlive && u.IsCommander) return u;
            return null;
        }

        /// <summary>Kam'ın konumu (UI menzil halkasını buraya çizer). Kam yoksa false.</summary>
        public bool TryGetKamCoord(out HexCoordinate coord)
        {
            Unit kam = KamUnit();
            coord = kam != null ? kam.Coordinate : default;
            return kam != null;
        }
    }
}
