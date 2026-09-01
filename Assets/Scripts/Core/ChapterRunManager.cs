using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bölümün KAYIP ve YENİDEN BAŞLAMA kuralı (TASK-007 · GAME_DESIGN.md §3).
    ///
    /// **Kayıp kapsamı — kritik kural:** kaybedilen şey SADECE O BÖLÜM/HARİTA'dır, TÜM RUN DEĞİL.
    ///   • KORUNUR: kalıcı roster (üretilmiş birimler + seviyeleri) — <see cref="PartyManager"/>'a
    ///     hiç dokunulmaz. Harcanan öz zaten kalıcı birime dönüştüğü için ayrı bir "banka" gerekmiyor.
    ///   • KAYBOLUR: o bölümde toplanmış ama HARCANMAMIŞ ham öz (taş + doğa) + keşif ilerlemesi
    ///     (yeni harita yeni seed'le kurulduğu için sis/çöküş/düğümler sıfırdan).
    ///
    /// Üç kayıp yolu da AYNI kurala tabidir (görev metni: "Kam ölümü DAHİL hepsi aynı kural"):
    ///   1. Gün <see cref="CollapseConfig.HardCutDay"/> bitti → süre doldu.
    ///   2. Savaşta Kam düştü (<see cref="CombatResult.PlayerLost"/>).
    ///   3. (elle) <see cref="LoseChapter"/> çağrısı.
    ///
    /// Yeniden başlarken harita 10-seed havuzundan **farklı bir seed** ile üretilir
    /// (<see cref="ChapterMapGenerator.GenerateNew"/> son oynananı elemeye çalışır).
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class ChapterRunManager : MonoBehaviour
    {
        [SerializeField] private ActionPointManager  _ap;
        [SerializeField] private CollapseConfig      _collapseConfig;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private EssenceWallet       _wallet;
        [SerializeField] private TurnManager         _turns;
        [SerializeField] private GameStateManager    _state;
        [Tooltip("Yeniden başlarken çöküş sayacı/işaretleri sıfırlansın diye.")]
        [SerializeField] private MapCollapseManager  _collapse;

        [Tooltip("Bölüm kaybedilince sıfırlanan ÖZ türleri — bölümün ham özü. Kalıcı birime dönüşmüş " +
                 "öz zaten roster'da, etkilenmez.")]
        [SerializeField] private EssenceType[] _chapterEssences = { EssenceType.Tas, EssenceType.Doga };

        /// <summary>Bölüm kaybedildi mi? (true iken harita ilerlenemez — sert kesim)</summary>
        public bool ChapterLost { get; private set; }

        /// <summary>Neden kaybedildi (banner metni).</summary>
        public string LossReason { get; private set; } = "";

        /// <summary>Son oynanabilir gün (sert kesim). Config yoksa 14.</summary>
        public int HardCutDay => _collapseConfig != null ? _collapseConfig.HardCutDay : 14;

        /// <summary>Son biten savaş kazanıldı mı? (oyun sonu ekranının metni için)</summary>
        public bool LastCombatWon { get; private set; }

        /// <summary>Bölüm kaybedildi / yeniden başladı — UI dinler.</summary>
        public event System.Action OnChapterStateChanged;

        private void OnEnable()
        {
            if (_ap    != null) _ap.OnTimeAdvanced    += HandleTimeAdvanced;
            if (_turns != null) _turns.OnCombatEnded  += HandleCombatEnded;
        }

        private void OnDisable()
        {
            if (_ap    != null) _ap.OnTimeAdvanced    -= HandleTimeAdvanced;
            if (_turns != null) _turns.OnCombatEnded  -= HandleCombatEnded;
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            // Sert kesim: son oynanabilir gün GEÇİLDİ → bölüm kaybedildi.
            if (!ChapterLost && day > HardCutDay)
                LoseChapter($"Süre doldu — gün {HardCutDay} sonunda harita çöktü.");
        }

        private void HandleCombatEnded(CombatResult result)
        {
            // Son savaşın sonucu saklanır: oyun sonu ekranı "boss yenildi mi" diye soruyor
            // (boss düğümü sonucu bilmiyor, kazanan da kaybeden de aynı yoldan dönüyor).
            LastCombatWon = result == CombatResult.PlayerWon;

            // Kam düştü → aynı kural: SADECE bu bölüm baştan (tüm run değil).
            if (!ChapterLost && result == CombatResult.PlayerLost)
                LoseChapter("Kam düştü — bölüm kaybedildi.");
        }

        /// <summary>Bölümü kaybet: ilerlemeyi dondur, banner göster. Harita YENİDEN BAŞLATILANA kadar
        /// (bkz <see cref="RestartChapter"/>) oyuncu ilerleyemez.</summary>
        public void LoseChapter(string reason)
        {
            ChapterLost = true;
            LossReason  = reason;
            if (_ap != null) _ap.SetFrozen(true);   // ilerleme donar (sert kesim)
            Debug.Log($"[Bolum] KAYIP — {reason}");
            OnChapterStateChanged?.Invoke();
        }

        /// <summary>Bölümü FARKLI bir seed ile baştan başlat. Roster korunur, ham öz kaybolur.</summary>
        public void RestartChapter()
        {
            // 1) HAM öz kaybolur (harcanmamış taş/doğa). Roster'a DOKUNULMAZ.
            if (_wallet != null) _wallet.ClearTypes(_chapterEssences);

            // 2) Zaman motoru 1. güne sarılır (tüm run değil, bu bölümün sayacı).
            if (_ap != null) { _ap.SetFrozen(false); _ap.ResetTime(); }

            // 3) Çöküş durumu sıfırlanır (silinmiş/işaretli karolar yeni haritaya taşınmasın).
            if (_collapse != null) _collapse.ResetCollapse();

            // 4) Harita havuzdan FARKLI bir seed ile yeniden üretilir → düğümler/sis sıfırlanır
            //    (ChapterNodeManager ve MapCollapseManager OnMapGenerated/OnGridRegenerated dinliyor).
            if (_map != null) _map.GenerateNew();

            ChapterLost = false;
            LossReason  = "";
            Debug.Log("[Bolum] Yeniden baslatildi — yeni seed, roster korundu, ham oz sifirlandi.");
            OnChapterStateChanged?.Invoke();
        }
    }
}
