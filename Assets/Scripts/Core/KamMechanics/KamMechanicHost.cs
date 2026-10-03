using System;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kam'ın AKTİF MEKANİĞİNİN sahnedeki tek kapısı (eski adı <c>KamManaManager</c> — dosya GUID'i
    /// korundu, sahnedeki bileşen ve ona bakan referanslar olduğu gibi bu sınıfa geçti).
    ///
    /// İŞİ: içinde bulunulan bölümün kural setinden (<see cref="ChapterRulesSO.KamMechanic"/>)
    /// mekaniği seçmek, taze bir runtime örneği kurmak ve zaman olaylarını ona iletmek. Büyü
    /// kasterleri ve HUD'lar YALNIZ bu sınıfla konuşur; hangi mekaniğin açık olduğunu bilmezler.
    ///
    /// Mekanik ne zaman yeniden kurulur: harita üretilince (bölüm başı + ölüp yeniden başlama) ve
    /// bölüm değişince. İkisi de "yeni kural seti" anlamına gelir; eski durum taşınmaz.
    /// </summary>
    public class KamMechanicHost : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager  _apManager;
        [Tooltip("Hangi bölümdeyiz → hangi kural seti. Atanmazsa her zaman yedek mekanik kullanılır.")]
        [SerializeField] private ChapterProgress     _progress;
        [Tooltip("Yeni harita = bölüm (yeniden) başladı → mekanik taze kurulur.")]
        [SerializeField] private ChapterMapGenerator _map;

        [Header("Yedek")]
        [Tooltip("Bölümün kural seti yoksa ya da mekanik seçmiyorsa kullanılan mekanik. " +
                 "Bu da boşsa varsayılan mana (10, dilim başı +2) kurulur.")]
        [SerializeField] private KamMechanicSO _fallbackMechanic;

        private IKamMechanic _active;
        private KamMechanicSO _activeSource;

        /// <summary>Şu an açık mekanik (Awake'ten sonra hiçbir zaman null değil).</summary>
        public IKamMechanic Active => _active;

        /// <summary>Aktif mekaniği üreten asset (teşhis / kurulum doğrulaması).</summary>
        public KamMechanicSO ActiveSource => _activeSource;

        public string ResourceName => _active.ResourceName;
        public int    Current      => _active.Current;
        public int    Max          => _active.Max;

        /// <summary>(mevcut, tavan) — aktif mekanik değişse de aynı olay yayınlanır.</summary>
        public event Action<int, int> OnResourceChanged;

        /// <summary>Bölümün kuralıyla başka bir mekaniğe geçildi (HUD etiketini tazelesin).</summary>
        public event Action OnMechanicChanged;

        private void Awake() => Rebuild();

        private void OnEnable()
        {
            if (_apManager != null) _apManager.OnTimeAdvanced   += HandleTimeAdvanced;
            if (_progress  != null) _progress.OnProgressChanged += Rebuild;
            if (_map       != null) _map.OnMapGenerated         += Rebuild;
        }

        private void OnDisable()
        {
            if (_apManager != null) _apManager.OnTimeAdvanced   -= HandleTimeAdvanced;
            if (_progress  != null) _progress.OnProgressChanged -= Rebuild;
            if (_map       != null) _map.OnMapGenerated         -= Rebuild;
            if (_active    != null) _active.OnResourceChanged   -= Relay;
        }

        private void Start()
        {
            if (_apManager == null)
                Debug.LogError("[KamMekanik] _apManager NULL — zaman ilerleyince kaynak yenilenmez.");
            Relay(_active.Current, _active.Max);
        }

        // ── Mekanik seçimi ───────────────────────────────────────────────────

        /// <summary>Kural setine göre mekaniği TAZE kurar (eski durum taşınmaz).</summary>
        private void Rebuild()
        {
            KamMechanicSO source = _progress != null && _progress.CurrentRules != null
                                   ? _progress.CurrentRules.KamMechanic : null;
            if (source == null) source = _fallbackMechanic;
            if (source == null)
            {
                // Kurulum koşmamış eski sahne: oyun kırılmasın, eski mana davranışı sürsün.
                source = ScriptableObject.CreateInstance<ManaMechanicSO>();
                source.name = "Mana (varsayilan)";
                Debug.LogWarning("[KamMekanik] Kural setinde de yedekte de mekanik yok — " +
                                 "varsayilan mana kuruldu. 'Bolum - Kural Seti Omurgasini Kur' kostur.");
            }

            bool changed = source != _activeSource;
            if (_active != null) _active.OnResourceChanged -= Relay;

            _activeSource = source;
            _active       = source.CreateRuntime();
            _active.OnResourceChanged += Relay;

            if (changed) Debug.Log($"[KamMekanik] Aktif mekanik: {source.name} ({_active.ResourceName}).");
            if (changed) OnMechanicChanged?.Invoke();
            Relay(_active.Current, _active.Max);
        }

        private void Relay(int current, int max) => OnResourceChanged?.Invoke(current, max);

        private void HandleTimeAdvanced(int day, int slot, string slotName)
            => _active.OnTimeSlotAdvanced(day, slot);

        // ── Bedel (büyü kasterleri buradan öder) ─────────────────────────────

        public bool CanPay(int cost)      => _active.CanPay(cost);
        public bool TryPay(int cost)      => _active.TryPay(cost);
        public void Restore(int amount)   => _active.Restore(amount);
        public int  ModifyPower(int power) => _active.ModifyPower(power);
    }
}
