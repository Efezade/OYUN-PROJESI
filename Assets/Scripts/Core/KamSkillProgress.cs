using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN YETENEK AĞACINDAKİ İLERLEME (2026-09-04): hangi büyü açık, kaçıncı seviyede.
    /// Ağacın şekli <see cref="KamSkillTreeSO"/>'da, çizimi KİTAP ekranında
    /// (<see cref="TacticalRPG.UI.KamSkillTreeView"/>), harcama burada.
    ///
    /// İKİ TÜKETİCİ, TEK KAYNAK:
    ///   • KİTAP ekranı — açma/yükseltme (öz harcar).
    ///   • <see cref="CombatDrumManager"/> — draft havuzunu buradan sorar ve kartı SEVİYELİ
    ///     kopyayla sunar. Böylece ağaç, draftın sürprizini değil KALİTESİNİ değiştirir
    ///     (Efe'nin kararı 2026-09-04).
    ///
    /// SIFIRLANMA: Efe'nin kuralı — **ağaç ölünce sıfırlanır**. Bölüm kaybedilip yeniden
    /// başlatılınca <see cref="ChapterRunManager.RestartChapter"/> buradaki ilerlemeyi siler;
    /// kalıcı avantaj (roguelike meta ekonomi) AYRI bir katman olacak, burası onu bilmez.
    ///
    /// KATALOG EZİLMEZ: seviye, <see cref="KamSkillCatalog"/>'un STATİK girdisine yazılmaz —
    /// statik veri savaş arası taşınır ve bir daha geri alınamazdı. Seviyeli kart her seferinde
    /// <see cref="Scaled"/> ile KOPYA olarak üretilir.
    /// </summary>
    public class KamSkillProgress : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private KamSkillTreeSO _tree;
        [SerializeField] private EssenceWallet  _wallet;

        /// <summary>Bir düğüm açıldı/yükseldi (KİTAP ekranı ve HUD dinler).</summary>
        public event System.Action OnChanged;

        // id → seviye. Sözlükte OLMAYAN düğüm kilitlidir (seviye 0).
        private readonly Dictionary<string, int> _levels = new();

        public KamSkillTreeSO Tree => _tree;

        private void Start() => ResetProgress();

        // ── Sorgular ─────────────────────────────────────────────────────────

        public bool IsUnlocked(string skillId) => LevelOf(skillId) > 0;

        public int LevelOf(string skillId)
            => !string.IsNullOrEmpty(skillId) && _levels.TryGetValue(skillId, out int lv) ? lv : 0;

        /// <summary>Ön koşulu açık mı? (kök düğümlerde her zaman true)</summary>
        public bool PrerequisiteMet(KamSkillTreeSO.Node node)
            => node != null && (string.IsNullOrEmpty(node.Requires) || IsUnlocked(node.Requires));

        /// <summary>Bu düğüm ŞU AN yükseltilebilir mi (açık + tavana gelmemiş)?</summary>
        public bool CanLevelUp(KamSkillTreeSO.Node node)
            => node != null && IsUnlocked(node.SkillId) && LevelOf(node.SkillId) < node.MaxLevel;

        /// <summary>Bir sonraki adımın bedeli: kilitliyse AÇMA, açıksa YÜKSELTME bedeli.
        /// Tavana gelmiş düğümde boş liste döner.</summary>
        public IReadOnlyList<EssenceAmount> NextCost(KamSkillTreeSO.Node node)
        {
            if (node == null) return System.Array.Empty<EssenceAmount>();
            int lv = LevelOf(node.SkillId);
            if (lv == 0)            return node.UnlockCost ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();
            if (lv < node.MaxLevel) return node.UpgradeCost(lv);
            return System.Array.Empty<EssenceAmount>();
        }

        /// <summary>Bir sonraki adım şu an ödenebilir mi (ön koşul + kese)?</summary>
        public bool CanAffordNext(KamSkillTreeSO.Node node)
        {
            if (node == null || _wallet == null) return false;
            if (!PrerequisiteMet(node)) return false;
            if (LevelOf(node.SkillId) >= node.MaxLevel) return false;
            return _wallet.CanAfford(NextCost(node));
        }

        // ── Harcama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Düğümü bir adım ilerletir: kilitliyse AÇAR, açıksa SEVİYE ATLATIR. Öz yetmiyorsa ya da
        /// ön koşul kapalıysa hiçbir şey olmaz.
        /// </summary>
        /// <returns>false = ön koşul kapalı, tavana gelinmiş ya da öz yetersiz.</returns>
        public bool TryAdvance(string skillId)
        {
            KamSkillTreeSO.Node node = _tree != null ? _tree.Find(skillId) : null;
            if (node == null || _wallet == null) return false;
            if (!PrerequisiteMet(node)) return false;

            int lv = LevelOf(skillId);
            if (lv >= node.MaxLevel) return false;
            if (!_wallet.TrySpend(NextCost(node))) return false;

            _levels[skillId] = lv + 1;
            Debug.Log(lv == 0
                ? $"[Yetenek] '{node.SkillId}' ACILDI — artik draft havuzunda."
                : $"[Yetenek] '{node.SkillId}' seviye {lv} -> {lv + 1}.");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Ağacı başlangıç hâline döndürür (ölüm → bölüm yeniden başlar).
        /// Harcanmış öz GERİ GELMEZ — bu bir sıfırlama, geri alma değil.</summary>
        public void ResetProgress()
        {
            _levels.Clear();
            if (_tree != null)
                foreach (var n in _tree.Nodes)
                    if (n != null && n.UnlockedAtStart && n.Catalog != null) _levels[n.SkillId] = 1;
            OnChanged?.Invoke();
        }

        // ── Davul draftına bakan yüz ─────────────────────────────────────────

        /// <summary>
        /// Draft havuzu: AÇIK büyülerin SEVİYELİ kopyaları. Ağaç atanmamışsa (eski sahne) boş
        /// döner ve davul eski davranışına — katalogdaki her büyüye — geri düşer.
        /// </summary>
        public void FillUnlockedPool(List<KamSkillCatalog.Entry> into)
        {
            if (into == null) return;
            into.Clear();
            if (_tree == null) return;

            foreach (var n in _tree.Nodes)
            {
                if (n == null || !IsUnlocked(n.SkillId)) continue;
                KamSkillCatalog.Entry e = n.Catalog;
                if (e != null) into.Add(Scaled(e, n, LevelOf(n.SkillId)));
            }
        }

        /// <summary>Katalog girdisinin bu seviyedeki KOPYASI. Seviye 1 = katalog değerleri.</summary>
        public KamSkillCatalog.Entry Scaled(string skillId)
        {
            KamSkillCatalog.Entry e = KamSkillCatalog.Get(skillId);
            KamSkillTreeSO.Node   n = _tree != null ? _tree.Find(skillId) : null;
            return e == null ? null : Scaled(e, n, Mathf.Max(1, LevelOf(skillId)));
        }

        private static KamSkillCatalog.Entry Scaled(KamSkillCatalog.Entry e,
                                                    KamSkillTreeSO.Node n, int level)
        {
            int steps = Mathf.Max(0, level - 1);
            if (n == null || steps == 0) return e;      // seviye 1 → katalog girdisi aynen

            int magnitude = e.Magnitude    + n.MagnitudePerLevel * steps;
            int radius    = e.Radius       + n.RadiusPerLevel    * steps;
            int push      = e.PushDistance + n.PushPerLevel      * steps;
            int stun      = e.StunTurns    + n.StunPerLevel      * steps;

            // Açıklama metni ELLE yazılmış ve içinde sayılar geçiyor (kartta yazan = davranışın
            // sözleşmesi). Metni yeniden üretmek yerine seviye satırı EKLENİR: eski cümle
            // katalogdaki taban değeri anlatmaya devam eder, fark açıkça altında durur.
            string extra = $"\n\nSEVİYE {level}";
            if (n.MagnitudePerLevel != 0) extra += $" · güç {e.Magnitude} → {magnitude}";
            if (n.RadiusPerLevel    != 0) extra += $" · çap {e.Radius * 2 + 1} → {radius * 2 + 1} karo";
            if (n.PushPerLevel      != 0) extra += $" · itme {e.PushDistance} → {push} karo";
            if (n.StunPerLevel      != 0) extra += $" · sersemletme {e.StunTurns} → {stun} tur";

            return new KamSkillCatalog.Entry
            {
                Id           = e.Id,
                Name         = e.Name,
                Description  = e.Description + extra,
                Effect       = e.Effect,
                Radius       = radius,
                Magnitude    = magnitude,
                PushDistance = push,
                StunTurns    = stun,
                R = e.R, G = e.G, B = e.B
            };
        }
    }
}
