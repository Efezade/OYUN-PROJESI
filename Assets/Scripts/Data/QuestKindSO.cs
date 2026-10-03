using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// ZORUNLU GÖREVİN TİPİ (Faz 1, YOL_HARITASI.md madde 5.2): bir zorunlu görev savaş, bulmaca
    /// ya da yepyeni bir mekanik olabilir. Akış her tipte aynı:
    /// <b>girildi → tipin ekranı/kuralı işler → başarı/başarısızlık döner → zincir devam eder.</b>
    ///
    /// Neden SO: yeni bir tip eklemek <c>ChapterNodeManager</c>'ı ya da <c>MissionManager</c>'ı
    /// yamamak olmasın (CLAUDE.md spagetti yasağı). Tip = bir asset + onu işleyen bir
    /// <see cref="TacticalRPG.Core.QuestKindRunner"/>. Düğüm yöneticisi yalnız "bu tipi kim işler"
    /// diye sorar.
    ///
    /// SAVAŞ özel bir tiptir: savaş akışı oyunun çekirdeğinde zaten var (durum makinesi + karo
    /// üstü "Savaşa Gir" istemi), bu yüzden runner istemez — <see cref="IsCombat"/> true döner ve
    /// düğüm yöneticisi yerleşik yolu kullanır.
    /// </summary>
    public abstract class QuestKindSO : ScriptableObject
    {
        [SerializeField] private string _displayName = "Görev";

        [TextArea(2, 4)]
        [Tooltip("Düğüm panelinde oyuncuya gösterilen kısa açıklama.")]
        [SerializeField] private string _description;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;

        /// <summary>true = yerleşik savaş akışı. false = bir <c>QuestKindRunner</c> işler.</summary>
        public abstract bool IsCombat { get; }
    }
}
