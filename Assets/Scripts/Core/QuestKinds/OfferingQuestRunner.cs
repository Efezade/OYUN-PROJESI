using System;
using System.Text;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>ADAK görevini işler: özü cüzdandan düşer, görev anında biter.</summary>
    public class OfferingQuestRunner : QuestKindRunner
    {
        [SerializeField] private EssenceWallet _wallet;

        public override bool Handles(QuestKindSO kind) => kind is OfferingQuestKindSO;

        public override bool CanBegin(QuestKindSO kind, out string reason)
        {
            reason = "";
            if (kind is not OfferingQuestKindSO offering) { reason = "Bu görev tipi tanınmıyor."; return false; }
            if (_wallet == null) { reason = "Öz deposu bağlı değil."; return false; }
            if (_wallet.CanAfford(offering.Offering)) return true;

            reason = $"Adak için gerekli: {Describe(offering)}";
            return false;
        }

        public override void Begin(QuestKindSO kind, HexCoordinate coord, Action<bool> onFinished)
        {
            bool ok = kind is OfferingQuestKindSO offering && _wallet != null && _wallet.TrySpend(offering.Offering);
            Debug.Log(ok ? $"[Gorev] Adak sunuldu ({coord}) — gorev tamam."
                         : $"[Gorev] Adak sunulamadi ({coord}) — oz yetersiz.");
            onFinished?.Invoke(ok);
        }

        private static string Describe(OfferingQuestKindSO offering)
        {
            var sb = new StringBuilder();
            foreach (var c in offering.Offering)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c.amount).Append(' ').Append(c.type);
            }
            return sb.ToString();
        }
    }
}
