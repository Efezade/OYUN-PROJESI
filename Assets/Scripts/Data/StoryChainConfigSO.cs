using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// HİKAYE ZİNCİRLERİ ayarı (Efe'nin isteği 2026-10-03): haritadaki savaş alanları (zindan +
    /// karşılaşma) TEK BAŞINA görev değildir; birbirine yakın olanlar 2-5 adımlık zincirler kurar.
    /// Hikaye metni ÇOK İLERİDE gelecek (yandan çıkan karakter konuşmaları); şimdilik zincir yalnız
    /// veri + minimapte hafif bir bağ çizgisi. Sıra kilidi YOK — her alana yine serbestçe girilir.
    ///
    /// Bölümün kural seti (<see cref="ChapterRulesSO"/>) kendi ayarını seçebilir; seçmezse
    /// <c>StoryChainManager</c>'ın yedeği kullanılır.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryChainConfig", menuName = "TacticalRPG/Config/StoryChainConfig")]
    public class StoryChainConfigSO : ScriptableObject
    {
        [Tooltip("Bir zincirin en az adım sayısı. 2'nin altına inemez: 'hiçbir savaş alanı tek " +
                 "görev gibi olmayacak' (Efe).")]
        [SerializeField, Min(2)] private int _minLength = 2;

        [Tooltip("Bir zincirin en çok adım sayısı. Her zincirin uzunluğu bu aralıktan zarla seçilir.")]
        [SerializeField, Min(2)] private int _maxLength = 5;

        [Tooltip("Zincirin iki ardışık adımı arasındaki en büyük HEX mesafesi. Zincir 'yakın savaş " +
                 "alanlarını' bağlar; uzaktakini zorla bağlamaz. Tek başına kalan alan yine de en " +
                 "yakın zincirin ucuna eklenir (tekil görev kuralı mesafeden önce gelir).")]
        [SerializeField, Min(1)] private int _maxLinkDistance = 7;

        public int MinLength       => Mathf.Max(2, _minLength);
        public int MaxLength       => Mathf.Max(MinLength, _maxLength);
        public int MaxLinkDistance => Mathf.Max(1, _maxLinkDistance);
    }
}
