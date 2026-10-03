using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// ADAK görevi — savaşsız ilk tip, soyutlamanın uçtan uca çalıştığını gösteren en küçük örnek:
    /// görev karosunda belirtilen özü adarsan görev biter. Ekran istemez, düğüm panelinden çözülür.
    ///
    /// Efe'nin listesinde "puzzle" ve "yeni mekanik" var ama ikisinin de tasarımı yok; bu tip
    /// onların YERİNE değil, yolun açık olduğunu kanıtlamak için. Hiçbir bölümün havuzunda
    /// varsayılan olarak YOK — zincir ayarındaki görev tipi listesine sürüklenince devreye girer.
    /// </summary>
    [CreateAssetMenu(fileName = "GorevTipi_Adak", menuName = "TacticalRPG/Gorev Tipi/Adak")]
    public class OfferingQuestKindSO : QuestKindSO
    {
        [Tooltip("Görevi bitirmek için adanacak öz.")]
        [SerializeField] private EssenceAmount[] _offering =
        {
            new EssenceAmount(EssenceType.Tas,  15),
            new EssenceAmount(EssenceType.Doga, 15)
        };

        public IReadOnlyList<EssenceAmount> Offering
            => _offering ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();

        public override bool IsCombat => false;
    }
}
