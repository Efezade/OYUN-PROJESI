using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// KAM'IN YETENEK AĞACI (2026-09-04, Efe'nin isteği): büyüler ÖZ harcanarak açılır ve
    /// yükseltilir. Ağacın kendisi KİTAP ekranında çizilir, ilerleme
    /// <see cref="TacticalRPG.Core.KamSkillProgress"/>'te tutulur.
    ///
    /// AĞAÇ ↔ DAVUL DRAFTI (Efe'nin kararı 2026-09-04): **ağaç HAVUZU ve SEVİYEYİ belirler,
    /// draft yine rastgele seçer.** Yani açılan büyü "artık draftta çıkabilir" olur, yükseltilen
    /// büyü daha büyük yarıçapla/hasarla çıkar. Böylece davulun "şimdi mi patlatayım, tahtayı mı
    /// kurayım" gerilimi ölmez; ağaç sürprizi değil, HAVUZUN KALİTESİNİ değiştirir.
    ///
    /// Neden düğüm etkisi "yeni bir büyü" değil de SEVİYE ÇARPANI: her seviye için ayrı katalog
    /// girdisi yazmak 5 büyü × 3 seviye = 15 girdi ve 15 ayrı denge sayısı demekti. Seviye,
    /// katalog girdisinin ÜSTÜNE binen bir değiştirici — <see cref="KamSkillCatalog"/> tek
    /// doğruluk kaynağı olarak kalır.
    ///
    /// SAYILAR TASLAK: denge/ekonomi hesapları durdurulmuş durumda (CLAUDE.md §9). Buradaki
    /// maliyetler ve seviye kazanımları yer tutucudur, `Docs/GAME_DESIGN.md`'ye henüz girmedi.
    ///
    /// 2026-10-03: düğüm şekli ve doğrulama ortak tabana taşındı (<see cref="UpgradeTreeSO"/>);
    /// KİTAP'taki KAROLAR ağacı (<see cref="AugmentTreeSO"/>) aynı tabanı kullanır.
    /// </summary>
    [CreateAssetMenu(fileName = "KamSkillTree", menuName = "TacticalRPG/Config/KamSkillTree")]
    public class KamSkillTreeSO : UpgradeTreeSO
    {
        public override bool IsKnownId(string id) => KamSkillCatalog.Get(id) != null;

        protected override string TreeLabel => "YetenekAgaci";
    }
}
