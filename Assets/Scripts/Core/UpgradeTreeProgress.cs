using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KİTAP'TAKİ BİR GELİŞTİRME AĞACINDA İLERLEME — hangi düğüm açık, kaçıncı seviyede, bir
    /// sonraki adım neye mal olur (2026-10-03'te <see cref="KamSkillProgress"/>'ten ayrıldı).
    ///
    /// Burası AĞACIN TÜRÜNÜ BİLMEZ: büyü de karo da aynı kurala uyar (ön koşul · açık başlar ·
    /// seviye tavanı · öz bedeli). Türe özgü iki şey alt sınıfta kalır:
    ///   • Hangi ağaç aktif (<see cref="Tree"/>) — büyü ağacını bölümün kural seti seçebilir.
    ///   • KİTAP'ta düğümün adı ve ŞU ANKİ hâlinin açıklaması (<see cref="NameOf"/>,
    ///     <see cref="DescribeCurrent"/>) — seviyeli kopyayı yalnız alt sınıf üretebilir.
    ///
    /// SIFIRLANMA: ağaç ölünce sıfırlanır (Efe'nin kuralı) — <see cref="ChapterRunManager"/>
    /// bölüm yeniden başlarken <see cref="ResetProgress"/> çağırır. Harcanan öz geri GELMEZ.
    /// </summary>
    public abstract class UpgradeTreeProgress : MonoBehaviour
    {
        [SerializeField] private EssenceWallet _wallet;

        /// <summary>Bir düğüm açıldı/yükseldi/sıfırlandı (KİTAP sayfası ve davul dinler).</summary>
        public event System.Action OnChanged;

        // id → seviye. Sözlükte OLMAYAN düğüm kilitlidir (seviye 0).
        private readonly Dictionary<string, int> _levels = new();

        /// <summary>Aktif ağaç.</summary>
        public abstract UpgradeTreeSO Tree { get; }

        /// <summary>KİTAP'ta düğümün adı.</summary>
        public abstract string NameOf(string id);

        /// <summary>KİTAP künyesinde düğümün ŞU ANKİ hâli (açıksa seviyeli kopya) + etki alanı.</summary>
        public abstract string DescribeCurrent(string id);

        /// <summary>Konsol günlüğü etiketi ("Yetenek", "Karo").</summary>
        protected abstract string LogLabel { get; }

        protected EssenceWallet Wallet => _wallet;

        protected virtual void Start() => ResetProgress();

        // ── Sorgular ─────────────────────────────────────────────────────────

        public bool IsUnlocked(string id) => LevelOf(id) > 0;

        public int LevelOf(string id)
            => !string.IsNullOrEmpty(id) && _levels.TryGetValue(id, out int lv) ? lv : 0;

        /// <summary>Ön koşulu açık mı? (kök düğümlerde her zaman true)</summary>
        public bool PrerequisiteMet(UpgradeTreeSO.Node node)
            => node != null && (string.IsNullOrEmpty(node.Requires) || IsUnlocked(node.Requires));

        /// <summary>Bu düğüm ŞU AN yükseltilebilir mi (açık + tavana gelmemiş)?</summary>
        public bool CanLevelUp(UpgradeTreeSO.Node node)
            => node != null && IsUnlocked(node.Id) && LevelOf(node.Id) < node.MaxLevel;

        /// <summary>Bir sonraki adımın bedeli: kilitliyse AÇMA, açıksa YÜKSELTME bedeli.
        /// Tavana gelmiş düğümde boş liste döner.</summary>
        public IReadOnlyList<EssenceAmount> NextCost(UpgradeTreeSO.Node node)
        {
            if (node == null) return System.Array.Empty<EssenceAmount>();
            int lv = LevelOf(node.Id);
            if (lv == 0)            return node.UnlockCost ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();
            if (lv < node.MaxLevel) return node.UpgradeCost(lv);
            return System.Array.Empty<EssenceAmount>();
        }

        /// <summary>Bir sonraki adım şu an ödenebilir mi (ön koşul + kese)?</summary>
        public bool CanAffordNext(UpgradeTreeSO.Node node)
        {
            if (node == null || _wallet == null) return false;
            if (!PrerequisiteMet(node)) return false;
            if (LevelOf(node.Id) >= node.MaxLevel) return false;
            return _wallet.CanAfford(NextCost(node));
        }

        // ── Harcama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Düğümü bir adım ilerletir: kilitliyse AÇAR, açıksa SEVİYE ATLATIR. Öz yetmiyorsa ya da
        /// ön koşul kapalıysa hiçbir şey olmaz.
        /// </summary>
        /// <returns>false = ön koşul kapalı, tavana gelinmiş ya da öz yetersiz.</returns>
        public bool TryAdvance(string id)
        {
            UpgradeTreeSO.Node node = Tree != null ? Tree.Find(id) : null;
            if (node == null || _wallet == null) return false;
            if (!PrerequisiteMet(node)) return false;

            int lv = LevelOf(id);
            if (lv >= node.MaxLevel) return false;
            if (!_wallet.TrySpend(NextCost(node))) return false;

            _levels[id] = lv + 1;
            Debug.Log(lv == 0
                ? $"[{LogLabel}] '{id}' ACILDI — artik draft havuzunda."
                : $"[{LogLabel}] '{id}' seviye {lv} -> {lv + 1}.");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Ağacı başlangıç hâline döndürür (ölüm → bölüm yeniden başlar).
        /// Harcanmış öz GERİ GELMEZ — bu bir sıfırlama, geri alma değil.</summary>
        public void ResetProgress()
        {
            _levels.Clear();
            if (Tree != null)
                foreach (var n in Tree.Nodes)
                    if (n != null && n.UnlockedAtStart && Tree.IsKnownId(n.Id)) _levels[n.Id] = 1;
            OnChanged?.Invoke();
        }

        /// <summary>Seviye satırı için yardımcı: "güç +2 → +3" gibi bir parça.</summary>
        protected static string Delta(string label, int from, int to, string unit = "")
            => from == to ? "" : $" · {label} {from}{unit} → {to}{unit}";
    }
}
