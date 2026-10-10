using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.Data
{
    /// <summary>Bölümün anlatım tonu (YOL_HARITASI.md madde 11). Metin havuzu henüz bağlı değil —
    /// alan şimdiden duruyor ki her map'in kimliği tek asset'te toplansın.</summary>
    public enum ChapterTone
    {
        Ciddi,
        Sakaci,
        DorduncuDuvar,
        Romantik
    }

    /// <summary>
    /// BİR BÖLÜMÜN KURAL SETİ — Faz 2 omurgası (YOL_HARITASI.md madde 7, "MAP = KURAL SETİ").
    ///
    /// Neden var: artık birden fazla sistem map'e göre değişmek istiyor (Kam'ın ana mekaniği,
    /// zorunlu görev zinciri, öz türleri, yetenek ağacı). Bu asset olmadan her sistem kendi içinde
    /// <c>if (bolum == n)</c> yazardı. Burada her bölüm TEK bir asset'le tarif edilir; sistemler
    /// <see cref="TacticalRPG.Core.ChapterProgress.CurrentRules"/>'tan okur.
    ///
    /// KURAL — boş alan "eski davranış" demektir: her tüketici kendi Inspector yedeğine düşer.
    /// Böylece kural seti yarım doldurulmuş bir bölüm oyunu kırmaz.
    ///
    /// SAF VERİ: runtime durumu (mevcut mana, açık görevler) burada TUTULMAZ (CLAUDE.md §2).
    /// </summary>
    [CreateAssetMenu(fileName = "BolumKurallari", menuName = "TacticalRPG/Chapter Rules")]
    public class ChapterRulesSO : ScriptableObject
    {
        [Header("Kam")]
        [Tooltip("Bu bölümde Kam'ın ANA mekaniği (bugün: mana). Boş = KamMechanicHost'un yedeği.")]
        [SerializeField] private KamMechanicSO _kamMechanic;

        [Tooltip("Bu bölümde kullanılan yetenek ağacı. Map'e özgü dallar burada ayrı bir ağaç " +
                 "asset'iyle gelir. Boş = KamSkillProgress'teki varsayılan ağaç.\n\n" +
                 "DİKKAT: KİTAP sayfası ağacı kurulumda ÇİZER — yeni düğümlü bir ağaç atanırsa " +
                 "sayfa için 'Kitap - Yetenek Agaci Sayfasini Kur' yeniden koşturulmalı.")]
        [SerializeField] private KamSkillTreeSO _skillTree;

        [Header("Zorunlu görev zinciri")]
        [Tooltip("Bu bölümün zincir ayarı (başlangıç sayısı, açılış günleri, ödül eğrisi, görev " +
                 "tipleri, ekonomi eşikleri). Boş = sahnedeki yedek ayar.")]
        [SerializeField] private MandatoryQuestConfigSO _questChain;

        [Header("Hikaye zincirleri")]
        [Tooltip("Savaş alanlarının 2-5 adımlık zincirlere bölünme ayarı. Boş = StoryChainManager'ın yedeği.")]
        [SerializeField] private StoryChainConfigSO _storyChains;

        [Header("Öz")]
        [Tooltip("Bölümün HAM özleri. Bölüm kaybedilince yalnız bunlar sıfırlanır. Boş = " +
                 "ChapterRunManager'daki yedek liste.")]
        [SerializeField] private EssenceType[] _chapterEssences = { EssenceType.Tas, EssenceType.Doga };

        [Tooltip("Zorunlu görev / düğüm ödülünün hangi özle ödendiği.")]
        [SerializeField] private EssenceType _rewardEssence = EssenceType.Doga;

        [Header("Bölgeler (2026-10-10)")]
        [Tooltip("Haritanın bölge seti (Minecraft biyomu gibi bölgeler, yeri her koşuda değişir). " +
                 "Boş = bölgesiz eski iklim üretimi.")]
        [SerializeField] private RegionSetSO _regions;

        [Tooltip("Kara Aşı çürümesi ayarı. Boş = bu bölümde çürüme YOK.")]
        [SerializeField] private CorruptionConfigSO _corruption;

        [Header("Anlatım")]
        [SerializeField] private ChapterTone _tone = ChapterTone.Ciddi;

        [TextArea(2, 5)]
        [Tooltip("Tasarım notu — oyunda görünmez. Bu bölümün kuralı neden böyle?")]
        [SerializeField] private string _designNote;

        public KamMechanicSO          KamMechanic     => _kamMechanic;
        public KamSkillTreeSO         SkillTree       => _skillTree;
        public MandatoryQuestConfigSO QuestChain      => _questChain;
        public StoryChainConfigSO     StoryChains     => _storyChains;
        public EssenceType            RewardEssence   => _rewardEssence;
        public ChapterTone            Tone            => _tone;
        public RegionSetSO            Regions         => _regions;
        public CorruptionConfigSO     Corruption      => _corruption;

        /// <summary>Boşsa null döner → tüketici kendi yedeğine düşer.</summary>
        public EssenceType[] ChapterEssences
            => _chapterEssences != null && _chapterEssences.Length > 0 ? _chapterEssences : null;
    }
}
