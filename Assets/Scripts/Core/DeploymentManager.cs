using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaş öncesi YERLEŞTİRME fazını yönetir: yerleştirme bölgesini vurgular,
    /// seçili kartı hex'e Unit olarak indirir.
    /// GameStateManager.OnStateChanged'i dinler — Deployment'a girince kurar, çıkınca temizler.
    /// (Event-driven; durum makinesine tek yönlü bağlı.)
    ///
    /// KOMUTAN (KAM) ARTIK ELLE YERLEŞTİRİLİR (kullanıcı isteği 2026-09-01). Eskiden savaş
    /// başında otomatik olarak alt-orta hücreye inerdi; oyuncu onu istediği yere koyamıyordu.
    /// Yeni kural:
    ///   • Kam yerleştirme listesinde HAZIR durur ve ÖZ İSTEMEZ — diğer sınıflar önce öz
    ///     tarifiyle ÜRETİLİR, Kam ise partide zaten var (üretilmesi gerekmez).
    ///   • İstenen hücreye, diğer kartlarla aynı yolla (kart seç → mavi hex'e tıkla) iner.
    ///   • KAM OLMADAN SAVAŞ BAŞLAMAZ: kapı <see cref="TryStartBattle"/>'da, yalnız HUD'un
    ///     düğmesini kapatmakla bırakılmadı (kritik kural kodda da açıkça yazılır).
    /// </summary>
    public class DeploymentManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private UnitManager       _unitManager;
        [Tooltip("Yerleştirilecek kartların kaynağı — komutan kartı da buradan bulunur.")]
        [SerializeField] private PartyManager      _party;

        [Header("Yerleştirme Bölgesi")]
        [Tooltip("Savaş haritasının alt kaç satırı yerleştirme bölgesi olsun (R < bu değer).")]
        [SerializeField, Min(1)] private int _deployZoneRows = 2;

        [Header("Görsel")]
        [Tooltip("Opsiyonel — atanmazsa runtime kapsül üretilir.")]
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private Color _playerUnitColor = new(0.30f, 0.60f, 1f);
        [SerializeField] private Color _zoneColor       = new(0.25f, 0.85f, 1f);

        public CharacterCard SelectedCard { get; set; }

        private readonly List<HexCoordinate>    _zone          = new();
        private readonly List<GameObject>       _markers       = new();
        private readonly List<Unit>             _deployed      = new();
        private readonly HashSet<CharacterCard> _deployedCards = new();
        private Transform _container;
        private Material  _zoneMat;
        private Unit      _commanderUnit; // yerleştirilmiş Kam (yoksa null → savaş başlamaz)

        public IReadOnlyList<HexCoordinate> Zone => _zone;
        public int  DeployedCount => _deployed.Count;
        /// <summary>Sahaya inmiş komutan (Kam). Yerleştirilmediyse null.</summary>
        public Unit CommanderUnit => _commanderUnit;
        /// <summary>Kam sahada mı? Savaşın başlaması buna bağlı.</summary>
        public bool IsCommanderDeployed => _commanderUnit != null;
        /// <summary>Savaş başlatılabilir mi? TEK KOŞUL: komutan sahada.</summary>
        public bool CanStartBattle => IsCommanderDeployed;
        public bool IsCardDeployed(CharacterCard c) => c != null && _deployedCards.Contains(c);

        /// <summary>Partideki komutan kartı (HUD listede en üste koyar). Yoksa null.</summary>
        public CharacterCard CommanderCard => FindCommanderCard();

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Deployment: BeginDeployment();    break;
                case GameState.Combat:     ClearMarkers();       break; // birimler savaşta kalır
                default:                   TeardownDeployment(); break; // overworld → birimleri temizle
            }
        }

        // ── Kurulum / temizlik ────────────────────────────────────────────────

        private void BeginDeployment()
        {
            TeardownDeployment(); // önceki kalıntı varsa temizle
            BuildZone();
            ShowMarkers();
            // Kam ARTIK OTOMATİK İNMEZ — oyuncu onu da elle yerleştirir (2026-09-01).
            // Faz boyunca seçili kart olarak komutanla başlıyoruz: zorunlu birim ilk sırada,
            // oyuncu ekrana girer girmez tek tıkla yerini seçebilir.
            SelectedCard = FindCommanderCard();
        }

        // Pedleri kaldırır, yerleştirilen birimleri despawn eder, seçimi sıfırlar.
        private void TeardownDeployment()
        {
            ClearMarkers();
            foreach (var u in _deployed)
                if (u != null) Destroy(u.gameObject);

            _deployed.Clear();
            _deployedCards.Clear();
            _commanderUnit = null;
            SelectedCard   = null;
        }

        private void BuildZone()
        {
            _zone.Clear();
            if (_grid == null || _grid.Cells == null) return;

            foreach (var kv in _grid.Cells)
            {
                HexCell cell = kv.Value;
                if (cell.Coordinate.R < _deployZoneRows && cell.IsWalkable)
                    _zone.Add(cell.Coordinate);
            }
        }

        // ── Yerleştirme ───────────────────────────────────────────────────────

        /// <summary>Seçili kartı bu hex'e yerleştirmeyi dener (MapInputHandler çağırır).</summary>
        public bool TryDeployAt(HexCoordinate coord)
        {
            if (_stateManager == null || _stateManager.State != GameState.Deployment) return false;
            if (SelectedCard == null)              return false;
            if (IsCardDeployed(SelectedCard))      return false;
            if (!_zone.Contains(coord))            return false;
            if (_unitManager != null && _unitManager.GetUnitAt(coord) != null) return false;

            // Deployment artık BEDAVA — öz, birimi overworld'de ÜRETİRKEN harcandı.
            Unit unit = SpawnUnit(coord, SelectedCard);
            _deployed.Add(unit);
            _deployedCards.Add(SelectedCard);
            if (SelectedCard.IsCommander) _commanderUnit = unit;   // savaş kapısı buna bakıyor
            Debug.Log($"[Deployment] {SelectedCard.Data.ClassName} → {coord} yerleştirildi" +
                      (SelectedCard.IsCommander ? " (komutan)." : "."));

            SelectedCard = null; // bir tıkta bir kart
            return true;
        }

        private Unit SpawnUnit(HexCoordinate coord, CharacterCard card)
        {
            EnsureContainer();

            GameObject go;
            if (_unitPrefab != null)
            {
                go = Instantiate(_unitPrefab, _container);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(_container);
                go.transform.localScale = Vector3.one * 0.45f;
                // Her kahraman kendi sınıf rengiyle (UnitColor) gelir → ayırt edilebilir.
                Color tint = card.Data != null ? card.Data.UnitColor : _playerUnitColor;
                TintRenderer(go.GetComponent<Renderer>(), tint);
            }
            go.name = $"Unit_{card.Data.ClassName}_{coord}";

            // Sınıfın modeli varsa kapsülü gerçek modelle değiştir (auto-scale + yön; Kam = animasyonlu barbar).
            // Controller da atanmışsa birim yürürken animasyon oynar (hareketi driver algılar).
            if (card.Data != null && card.Data.UnitModel != null)
            {
                go.AddComponent<CharacterModelBinder>()
                  .Apply(card.Data.UnitModel, card.Data.UnitModelHeight, card.Data.UnitModelEuler,
                         card.Data.UnitModelYOffset, true, card.Data.UnitAnimator);

                if (card.Data.UnitAnimator != null)
                    go.AddComponent<CharacterAnimationDriver>(); // binder'dan SONRA → Animator'ü bulur
            }

            Unit unit = go.GetComponent<Unit>();
            if (unit == null) unit = go.AddComponent<Unit>();

            card.RestoreFull(); // taze birim olarak in (savaş başı tam HP)
            unit.Configure(_grid, _unitManager, UnitTeam.Player);
            unit.Bind(card);
            unit.PlaceAt(coord);
            return unit;
        }

        // ── Komutan (Kam) + geri alma + savaş kapısı ──────────────────────────

        private CharacterCard FindCommanderCard()
        {
            if (_party == null) return null;
            foreach (var c in _party.Party)
                if (c != null && c.IsCommander) return c;
            return null;
        }

        /// <summary>
        /// Yerleştirilmiş bir kartı geri alır (birim yok edilir, kart yeniden yerleştirilebilir).
        /// GEREKLİ ÇÜNKÜ: Kam artık elle iniyor. Yanlış hücreye konan komutanı düzeltmenin tek
        /// yolu "Geri Dön → savaşa yeniden gir" olsaydı, elle yerleştirme bir tuzağa dönerdi.
        /// </summary>
        public bool TryUndeploy(CharacterCard card)
        {
            if (_stateManager == null || _stateManager.State != GameState.Deployment) return false;
            if (card == null || !_deployedCards.Contains(card)) return false;

            for (int i = _deployed.Count - 1; i >= 0; i--)
            {
                Unit u = _deployed[i];
                if (u == null || u.Card != card) continue;

                _deployed.RemoveAt(i);
                if (u == _commanderUnit) _commanderUnit = null;
                Destroy(u.gameObject);
            }

            _deployedCards.Remove(card);
            SelectedCard = card;          // hemen yeniden konabilsin
            return true;
        }

        /// <summary>
        /// SAVAŞ KAPISI — Kam sahada değilse savaş başlamaz (kullanıcı kuralı 2026-09-01).
        /// Kural HUD'un düğmesini kapatmakla BIRAKILMADI: düğme yalnız geri bildirim, karar burada.
        /// </summary>
        public bool TryStartBattle()
        {
            if (_stateManager == null || _stateManager.State != GameState.Deployment) return false;

            if (!IsCommanderDeployed)
            {
                Debug.LogWarning("[Deployment] Komutan (Kam) yerleştirilmeden savaş baslatilamaz.");
                return false;
            }

            _stateManager.StartBattle();
            return true;
        }

        // ── Görsel vurgulama ──────────────────────────────────────────────────

        private void ShowMarkers()
        {
            EnsureContainer();
            if (_zoneMat == null) _zoneMat = MakeColorMaterial(_zoneColor);

            foreach (var coord in _zone)
            {
                if (!_grid.TryGetCell(coord, out HexCell cell)) continue;

                var m = GameObject.CreatePrimitive(PrimitiveType.Cube); // ince yassı zemin pedi
                var col = m.GetComponent<Collider>();
                if (col != null) Destroy(col); // tıklama ışını altındaki karoyu görsün
                m.transform.SetParent(_container);
                m.transform.position   = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.06f);
                m.transform.localScale = new Vector3(1.3f, 0.04f, 1.3f);
                m.GetComponent<Renderer>().sharedMaterial = _zoneMat;
                _markers.Add(m);
            }
        }

        private void ClearMarkers()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("DeploymentVisuals").transform;
                _container.SetParent(transform, false);
            }
        }

        private static void TintRenderer(Renderer rend, Color color)
        {
            if (rend == null) return;
            var mat = MakeColorMaterial(color);
            rend.sharedMaterial = mat;
        }

        private static Material MakeColorMaterial(Color color)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
