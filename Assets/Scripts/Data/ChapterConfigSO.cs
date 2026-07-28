using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// 8 bölümün tanım listesi. **1 bölüm = 1 harita** (Docs/GAME_DESIGN.md §0, 2026-07-27).
    /// Her bölümün kendi temalı elementi vardır (§3).
    ///
    /// SAF VERİ — ilerleme durumu (hangisi bitti/açık) burada TUTULMAZ; o runtime'da
    /// <c>ChapterProgress</c>'tedir (CLAUDE.md §2: "Runtime'da SO verisi değiştirilmez").
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterConfig", menuName = "TacticalRPG/Chapter Config")]
    public class ChapterConfigSO : ScriptableObject
    {
        [System.Serializable]
        public class ChapterEntry
        {
            public string displayName = "Bölüm";

            [Tooltip("Bölümün temalı elementi (GAME_DESIGN.md §3). Henüz kararlaşmadıysa \"?\" bırak.")]
            public string theme = "?";

            [Tooltip("Teması/adı henüz TASARLANMADI mı? İşaretliyse HARİTA ekranında taslak sayılır — " +
                     "uydurulmuş içerik gerçek karar sanılmasın diye.")]
            public bool isPlaceholder = false;
        }

        [Tooltip("Sıra = bölüm numarası (index 0 = Bölüm 1). GAME_DESIGN.md §3'e göre 8 bölüm.")]
        public List<ChapterEntry> chapters = new();

        public int Count => chapters != null ? chapters.Count : 0;

        /// <summary>1-TABANLI bölüm girişi (Get(1) = Bölüm 1). Aralık dışıysa null.</summary>
        public ChapterEntry Get(int chapter)
            => (chapters != null && chapter >= 1 && chapter <= chapters.Count) ? chapters[chapter - 1] : null;

        public string NameOf(int chapter)
        {
            ChapterEntry e = Get(chapter);
            return e != null && !string.IsNullOrEmpty(e.displayName) ? e.displayName : $"Bölüm {chapter}";
        }

        public string ThemeOf(int chapter)
        {
            ChapterEntry e = Get(chapter);
            return e != null && !string.IsNullOrEmpty(e.theme) ? e.theme : "?";
        }
    }
}
