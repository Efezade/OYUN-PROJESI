using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// DEĞİŞTİRİLEBİLİR KAROLAR AĞACI (Efe'nin isteği 2026-10-03): savaşta davul çalınca solda
    /// sunulan karo kartları, KİTAP'ın KAROLAR sekmesinde öz harcanarak açılır ve yükseltilir.
    ///
    /// Büyü ağacıyla AYNI kural (<see cref="KamSkillTreeSO"/>): ağaç HAVUZU ve SEVİYEYİ belirler,
    /// davul yine rastgele seçer. Açılmamış karo draftta çıkmaz; yükseltilmiş karo daha güçlü
    /// (büyüklük / yarıçap / karo sayısı) çıkar. Mana bedeli seviyeyle DEĞİŞMEZ — bütçe kuralı
    /// (savaş başı 2-3 kart) yükseltmeyle delinmesin.
    ///
    /// Dallar karo GRUPLARIDIR (Kut · Kargış · Nötr · Patlayıcı · Sınıfsal): davul her gruptan
    /// bir kart sunduğu için her dalın en az bir düğümü AÇIK başlar.
    /// </summary>
    [CreateAssetMenu(fileName = "AugmentTree", menuName = "TacticalRPG/Config/AugmentTree")]
    public class AugmentTreeSO : UpgradeTreeSO
    {
        public override bool IsKnownId(string id) => AugmentCatalog.Get(id) != null;

        protected override string TreeLabel => "KaroAgaci";
    }
}
