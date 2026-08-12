using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    public enum UnitTeam { Player, Enemy }

    /// <summary>
    /// Hex üstünde duran bir savaş birimi (oyuncu veya düşman).
    /// Bir CharacterCard'a BAĞLIYSA HP/stat tek kaynaktan (karttan) gelir; bağlı değilse
    /// kendi _maxHP'sini kullanır (basit düşman kuklası için geri uyumlu).
    /// Faz B (deployment) oyuncu birimini Bind(card) + PlaceAt(coord) ile spawn eder.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [Header("Kimlik")]
        [SerializeField] private string   _displayName = "Birim";
        [SerializeField] private UnitTeam _team        = UnitTeam.Enemy;

        [Header("Konum")]
        [SerializeField] private HexCoordinate _coord;
        [Tooltip("Placeholder KAPSÜL için dikey ofset (kapsülün merkezi yüzeyin bu kadar üstünde).")]
        [SerializeField] private float         _heightOffset = 0.8f;
        [Tooltip("Gerçek MODEL takılıysa bunun yerine bu kullanılır. TileHeight (0.3) = modelin ayağı " +
                 "tam karo yüzeyine oturur; kapsül ofseti (0.8) modeli havada bırakırdı.")]
        [SerializeField] private float         _modelHeightOffset = HexMetrics.TileHeight;
        [SerializeField] private float         _moveSpeed    = 8f;

        [Header("Can (yalnızca KARTSIZ birimlerde kullanılır)")]
        [SerializeField, Min(1)] private int _maxHP = 10;

        [Header("Savaş statları (yalnızca KARTSIZ birimlerde)")]
        [SerializeField, Min(0)] private int _attack      = 3;
        [SerializeField, Min(0)] private int _defense     = 0;
        [SerializeField, Min(1)] private int _speed       = 5;
        [SerializeField, Min(1)] private int _moveRange   = 3;
        [SerializeField, Min(1)] private int _attackRange = 1;

        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager _gridManager;
        [SerializeField] private UnitManager    _unitManager;

        [Header("Hasar görseli")]
        [Tooltip("Vurulduğunda bir anlığına bu renge çakar, sonra kendi rengine döner.")]
        [SerializeField] private Color _hitFlashColor = new(1f, 0.25f, 0.20f);
        [SerializeField, Min(0.05f)] private float _hitFlashDuration = 0.5f;

        private CharacterCard _card;        // bağlıysa stat kaynağı (oyuncu/kartlı birim)
        private int           _currentHP;   // yalnızca kartsız birimde geçerli
        private bool          _diedNotified;
        private string        _runtimeName; // savaş başı atanan benzersiz isim (örn. "Goblin 2")

        // Hasar flaşı (MaterialPropertyBlock → paylaşılan materyali bozmaz; URP _BaseColor).
        // Model takılıysa (CharacterModelBinder) kapsül GİZLİ olduğu için flaş MODELİN
        // renderer'larına uygulanır — yoksa görünmez bir mesh'i boyardık.
        private Renderer[]            _renderers;
        private Color[]               _baseColors;
        private bool                  _renderersResolved;
        private MaterialPropertyBlock _mpb;
        private Coroutine             _flashCo;
        private static readonly int   BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int   ColorId     = Shader.PropertyToID("_Color");

        // ── Kimlik / konum ────────────────────────────────────────────────────
        // Öncelik: runtime atanan isim > kart sınıf adı > Inspector _displayName.
        public string        DisplayName =>
            !string.IsNullOrEmpty(_runtimeName) ? _runtimeName
            : (_card != null ? _card.Data.ClassName : _displayName);
        public bool          HasInstanceName => !string.IsNullOrEmpty(_runtimeName);
        public UnitTeam      Team        => _team;
        public HexCoordinate Coordinate  => _coord;
        public CharacterCard Card        => _card;
        public bool          IsCommander => _card != null && _card.IsCommander;

        // ── Can / kalkan ──────────────────────────────────────────────────────
        public int  MaxHP     => _card != null ? _card.MaxHP     : _maxHP;
        public int  CurrentHP => _card != null ? _card.CurrentHP : _currentHP;
        public int  Shield    { get; private set; }
        public bool IsAlive   => CurrentHP > 0;

        // ── Savaş statları (kart varsa karttan; yoksa kartsız fallback alanlarından) ──
        public int Attack      => _card != null ? _card.Attack      : _attack;
        public int Defense     => _card != null ? _card.Defense     : _defense;
        public int Level       => _card != null ? _card.Level       : 1;
        public int MoveRange   => _card != null ? _card.MoveRange   : _moveRange;
        public int Speed       => _card != null ? _card.Speed       : _speed;
        public int AttackRange => _card != null ? _card.AttackRange : _attackRange;

        public event Action<Unit> OnStatsChanged; // HP veya kalkan değişti
        public event Action<Unit> OnDied;
        /// <summary>Bu birim bir hedefe saldırdı (animasyon sürücüsü dinler; argüman = hedef).</summary>
        public event Action<Unit> OnAttackPerformed;

        private CharacterModelBinder _binder;

        private void Awake()
        {
            if (_card == null) _currentHP = _maxHP;
            _mpb    = new MaterialPropertyBlock();
            _binder = GetComponent<CharacterModelBinder>();
        }

        /// <summary>
        /// Flaşın boyayacağı renderer'ları ve özgün renklerini bulur. İLK HASARDA çözülür (Awake'te
        /// DEĞİL): model runtime'da takılabildiği için (CharacterModelBinder.Apply) erken çözersek
        /// gizlenmiş placeholder kapsülünü yakalarız ve flaş görünmez.
        /// </summary>
        private void ResolveRenderers()
        {
            _renderersResolved = true;

            Renderer[] found = _binder != null && _binder.ModelRenderer != null
                ? _binder.ModelRenderers         // gerçek model (skinned mesh + parçaları)
                : GetComponentsInChildren<Renderer>(true);

            var list = new List<Renderer>();
            foreach (var r in found)
                if (r != null && r.enabled) list.Add(r);   // gizlenmiş kapsülü atla

            _renderers  = list.ToArray();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = ReadBaseColor(_renderers[i]);
        }

        private static Color ReadBaseColor(Renderer r)
        {
            Material m = r != null ? r.sharedMaterial : null;
            if (m == null) return Color.white;
            return m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                 : m.HasProperty(ColorId)     ? m.GetColor(ColorId)
                 : m.color;
        }

        private void OnEnable()
        {
            if (_unitManager != null) _unitManager.Register(this);
        }

        private void OnDisable()
        {
            if (_unitManager != null) _unitManager.Unregister(this);
        }

        private void Start() => SnapToCell();

        private void OnDestroy()
        {
            if (_card != null) _card.OnHPChanged -= HandleCardHPChanged;
        }

        // ── Kart bağlama (Faz B deployment) ───────────────────────────────────

        /// <summary>
        /// Birimi bir CharacterCard'a bağlar — HP/stat artık karttan gelir (tek kaynak).
        /// Düşman da kartlı olabilir; takım bağımsızdır, çağıran ayarlar.
        /// </summary>
        public void Bind(CharacterCard card)
        {
            if (card == null) return;
            if (_card != null) _card.OnHPChanged -= HandleCardHPChanged;

            _card         = card;
            _diedNotified = false;
            _card.OnHPChanged += HandleCardHPChanged;
            OnStatsChanged?.Invoke(this);
        }

        /// <summary>
        /// Savaş başında benzersiz görünen isim atar (örn. "Goblin 2"). CombatNameplateHUD çağırır.
        /// DisplayName artık bunu döndürür → tur paneli/mesajlar da aynı ismi gösterir.
        /// </summary>
        public void SetInstanceName(string instanceName) => _runtimeName = instanceName;

        /// <summary>Birimi bir koordinata yerleştirir ve görsel olarak oraya oturtur.</summary>
        public void PlaceAt(HexCoordinate coord)
        {
            _coord = coord;
            SnapToCell();
        }

        /// <summary>
        /// Runtime spawn için bağımlılıkları + takımı ayarlar (DeploymentManager kullanır).
        /// UnitManager'a (yeniden) kaydeder.
        /// </summary>
        public void Configure(HexGridManager grid, UnitManager unitManager, UnitTeam team)
        {
            _gridManager = grid;
            _team        = team;
            if (_unitManager != null && _unitManager != unitManager) _unitManager.Unregister(this);
            _unitManager = unitManager;
            if (_unitManager != null) _unitManager.Register(this);
        }

        private void SnapToCell()
        {
            if (_gridManager != null && _gridManager.TryGetCell(_coord, out HexCell cell))
                transform.position = SurfacePosition(cell);
        }

        // Hücrenin yürüme yüzeyine oturmuş dünya pozisyonu (köprü/engebe SurfaceHeight ile).
        // Model takılıysa ayağı yüzeye oturtan ofset kullanılır; kapsülde merkez ofseti.
        private Vector3 SurfacePosition(HexCell cell) =>
            cell.WorldPosition + Vector3.up * (VerticalOffset + cell.SurfaceHeight - HexMetrics.TileHeight);

        // Model runtime'da takılabildiği için her seferinde sorulur (GetComponent Awake'te önbelleklenir).
        private float VerticalOffset =>
            _binder != null && _binder.ModelRenderer != null ? _modelHeightOffset : _heightOffset;

        // ── Hareket (TurnManager savaşta çağırır) ─────────────────────────────

        public bool IsMoving { get; private set; }

        /// <summary>Birimi verilen hücre yolu boyunca yürütür (path[0] = mevcut konum).</summary>
        public void MoveAlongPath(List<HexCell> path, Action onComplete = null)
        {
            if (path == null || path.Count < 2) { onComplete?.Invoke(); return; }
            StartCoroutine(MoveRoutine(path, onComplete));
        }

        private IEnumerator MoveRoutine(List<HexCell> path, Action onComplete)
        {
            IsMoving = true;
            for (int i = 1; i < path.Count; i++)
            {
                HexCell cell   = path[i];
                Vector3 target = SurfacePosition(cell);
                while ((transform.position - target).sqrMagnitude > 0.0001f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, _moveSpeed * Time.deltaTime);
                    yield return null;
                }
                _coord = cell.Coordinate;
            }
            IsMoving = false;
            onComplete?.Invoke();
        }

        private void HandleCardHPChanged(int current, int max)
        {
            OnStatsChanged?.Invoke(this);
            if (current <= 0 && !_diedNotified)
            {
                _diedNotified = true;
                OnDied?.Invoke(this);
            }
        }

        // ── Saldırı (TurnManager çağırır) ─────────────────────────────────────

        /// <summary>
        /// Bu birim hedefe vurur: önce <see cref="OnAttackPerformed"/> yayılır (animasyon sürücüsü
        /// saldırı klibini oynatır + gövdeyi hedefe çevirir), sonra hasar uygulanır. Menzil/tur
        /// kuralları çağıranın (TurnManager) sorumluluğundadır — burada yalnız etki + olay var.
        /// </summary>
        public void PerformAttack(Unit target)
        {
            if (target == null || !IsAlive) return;
            OnAttackPerformed?.Invoke(target);
            target.TakeDamage(Attack);
        }

        // ── Etki API'si (AbilityCaster çağırır) ───────────────────────────────

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            int beforeHP = CurrentHP;

            int remaining = amount;
            if (Shield > 0)
            {
                int absorbed = Mathf.Min(Shield, remaining);
                Shield    -= absorbed;
                remaining -= absorbed;
            }

            if (remaining <= 0) { OnStatsChanged?.Invoke(this); return; }

            if (_card != null)
            {
                // Kart kendi Defense'ini uygular ve OnHPChanged ile event akışını tetikler.
                _card.TakeDamage(remaining);
            }
            else
            {
                // Kartsız birim (savaş testi/placeholder): savunma BURADA da uygulanmalı.
                // Eskiden bu dal Defense'i tamamen atlıyordu → aynı birim kartlıyken dayanıklı,
                // kartsızken kağıttan oluyordu ve denge ölçümleri iki farklı sonuç veriyordu.
                _currentHP = Mathf.Max(0, _currentHP - CombatMath.Damage(remaining, Defense));
                OnStatsChanged?.Invoke(this);
                if (!IsAlive && !_diedNotified)
                {
                    _diedNotified = true;
                    OnDied?.Invoke(this);
                }
            }

            if (CurrentHP < beforeHP) PlayHitFlash(); // gerçekten can gittiyse kırmızı çak
        }

        // ── Hasar flaşı ───────────────────────────────────────────────────────

        private void PlayHitFlash()
        {
            if (!_renderersResolved) ResolveRenderers();
            if (_renderers.Length == 0 || !gameObject.activeInHierarchy) return;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            float t = 0f;
            while (t < _hitFlashDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(1f - t / _hitFlashDuration); // 1 (tam kırmızı) → 0 (kendi rengi)
                ApplyTint(k);
                yield return null;
            }
            ApplyTint(0f);
            _flashCo = null;
        }

        // k=1 → tam flaş rengi, k=0 → her renderer kendi özgün rengine döner.
        private void ApplyTint(float k)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                Color c = Color.Lerp(_baseColors[i], _hitFlashColor, k);
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            if (_card != null) { _card.Heal(amount); return; }

            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            OnStatsChanged?.Invoke(this);
        }

        public void AddShield(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            Shield += amount;
            OnStatsChanged?.Invoke(this);
        }
    }
}
