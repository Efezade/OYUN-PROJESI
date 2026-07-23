using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Karakterin gövde animasyonunu HAREKETİNDEN türetir: Transform'un yatay yer
    /// değiştirmesini her kare ölçer (hafif polling), Animator'daki "IsMoving"
    /// parametresini sürer ve gövdeyi yürüme yönüne döndürür.
    /// PlayerController / Unit koduna DOKUNMAZ (bağımsız gözlemci) — bu sayede aynı
    /// bileşen hem overworld oyuncusunda hem savaş birimlerinde çalışır ve hareket
    /// kodu ileride değişse bile animasyon bozulmaz.
    /// SAVAŞTA: aynı GameObject'te <see cref="Unit"/> varsa onun OLAYLARINA abone olur —
    /// saldırınca "Attack", ölünce "Death" trigger'ı (klip/parametre yoksa sessizce atlanır).
    /// Işınlanmalar (harita geçişi, PlaceAt) yürüme sayılmaz (_teleportDistance eşiği).
    /// Whitebox: eşikler/dönüş hızı Inspector'dan ayarlanır, koda gömülü değer yok.
    /// </summary>
    public class CharacterAnimationDriver : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Boş bırakılırsa çocuklardan otomatik bulunur (model runtime'da takıldığında).")]
        [SerializeField] private Animator _animator;

        [Header("Hareket Algısı")]
        [Tooltip("Bu yatay hızın (m/sn) üstü 'yürüyor' sayılır.")]
        [SerializeField, Min(0.01f)] private float _moveSpeedThreshold = 0.5f;
        [Tooltip("Tek karede bundan uzun yer değiştirme = ışınlanma (harita geçişi vb.) → yürüme sayılmaz.")]
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;
        [Tooltip("Hareket bittikten sonra yürüme pozundan çıkmadan önce beklenecek süre (sn) — adım aralarında titremesin. Kısa: durunca ayaklar hemen dursun.")]
        [SerializeField, Min(0f)] private float _stopDelay = 0.04f;

        [Header("Yön")]
        [Tooltip("Karakter yürürken gövdesi hareket yönüne dönsün mü.")]
        [SerializeField] private bool _faceMovement = true;
        [SerializeField, Min(1f)] private float _turnSpeed = 720f;
        [Tooltip("Modelin ileri ekseni +Z değilse düzeltme açısı (derece).")]
        [SerializeField] private float _yawOffset = 0f;

        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        private static readonly int AttackParam   = Animator.StringToHash("Attack");
        private static readonly int DeathParam    = Animator.StringToHash("Death");

        private Vector3 _lastPos;
        private bool    _hasLastPos;
        private float   _lastMoveTime = -999f;
        private bool    _isMoving;     // Animator'a en son yazılan değer (yalnız değişince yazılır)
        private bool    _dead;         // ölüm klibi başladı → hareket/yön sürüşü durur
        private Unit    _unit;         // aynı GO'da savaş birimi varsa (overworld oyuncusunda YOK)
        private bool    _hasIsMoving, _hasAttack, _hasDeath;   // controller'da var mı (bir kez okunur)

        private void OnEnable()
        {
            ResolveAnimator();
            _hasLastPos = false;
        }

        // Savaş birimine bağlan. Start'ta yapılır çünkü spawner sürücüyü Unit'ten ÖNCE ekliyor
        // (OnEnable'da Unit henüz yok). Unit yoksa (overworld oyuncusu) sessizce atlanır.
        private void Start()
        {
            _unit = GetComponent<Unit>();
            if (_unit == null) return;
            _unit.OnAttackPerformed += HandleAttackPerformed;
            _unit.OnDied            += HandleDied;
        }

        private void OnDestroy()
        {
            if (_unit == null) return;
            _unit.OnAttackPerformed -= HandleAttackPerformed;
            _unit.OnDied            -= HandleDied;
        }

        private void HandleAttackPerformed(Unit target)
        {
            if (target != null) FaceTowards(target.transform.position);
            PlayAttack();
        }

        private void HandleDied(Unit _) => PlayDeath();

        /// <summary>Controller'da gerçek bir ölüm klibi/trigger'ı var mı (yoksa ölüm anında bekleme gereksiz).</summary>
        public bool HasDeathAnimation => _hasDeath;

        /// <summary>Saldırı klibini oynatır (controller'da "Attack" trigger'ı varsa).</summary>
        public void PlayAttack()
        {
            if (_dead || _animator == null || !_hasAttack) return;
            _animator.SetTrigger(AttackParam);
        }

        /// <summary>Ölüm klibini oynatır ve sürücüyü durdurur (controller'da "Death" trigger'ı varsa).</summary>
        public void PlayDeath()
        {
            if (_dead) return;
            _dead = true;
            if (_animator == null) return;

            if (_hasIsMoving)
            {
                _isMoving = false;
                _animator.SetBool(IsMovingParam, false);
            }
            if (_hasDeath) _animator.SetTrigger(DeathParam);
        }

        /// <summary>Gövdeyi anında hedefe çevirir (saldırırken sırtı dönük kalmasın).</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            if (!_faceMovement) return;
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + _yawOffset, 0f);
        }

        /// <summary>
        /// Animator referansını (yeniden) çözer. Model runtime'da sonradan takılırsa
        /// (CharacterModelBinder.Apply) bu bileşen ondan SONRA eklenmeli — OnEnable bulur.
        /// </summary>
        public void ResolveAnimator()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            // Konum her zaman koddan gelir (PlayerController/Unit); animasyon yerinde oynar.
            if (_animator != null) _animator.applyRootMotion = false;
            CacheParameters();
        }

        // Hangi parametreler var? (Klip yoksa importer parametreyi de kurmaz.) Animator.parameters
        // her çağrıda dizi ayırdığı için BİR KEZ okunur — saldırı/ölüm anında tekrar sorulmaz.
        private void CacheParameters()
        {
            _hasIsMoving = _hasAttack = _hasDeath = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if      (p.nameHash == IsMovingParam) _hasIsMoving = true;
                else if (p.nameHash == AttackParam)   _hasAttack   = true;
                else if (p.nameHash == DeathParam)    _hasDeath    = true;
            }
        }

        private void Update()
        {
            if (_animator == null || _dead) return;   // öldüyse yürüme/yön sürüşü yok

            Vector3 pos = transform.position;
            if (!_hasLastPos) { _lastPos = pos; _hasLastPos = true; return; }

            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            delta.y  = 0f; // yalnız yatay hareket (engebe inişi/çıkışı yürüme sayılır zaten)

            float dist = delta.magnitude;
            if (dist > _teleportDistance) return; // ışınlanma — bu kareyi yok say

            float dt = Time.deltaTime;
            bool movingNow = dt > 0f && dist / dt > _moveSpeedThreshold;

            if (movingNow)
            {
                _lastMoveTime = Time.time;

                if (_faceMovement && dist > 0.0001f)
                {
                    float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + _yawOffset;
                    Quaternion target = Quaternion.Euler(0f, yaw, 0f);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * dt);
                }
            }

            bool moving = movingNow || Time.time - _lastMoveTime < _stopDelay;
            if (!_hasIsMoving) return;
            if (moving != _isMoving)
            {
                _isMoving = moving;
                _animator.SetBool(IsMovingParam, moving);
            }
        }
    }
}
