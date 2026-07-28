using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// "Hangi bölümdeyiz, hangileri bitti" — bölüm ilerlemesinin TEK kaynağı.
    /// **1 bölüm = 1 harita**, toplam 8 bölüm (Docs/GAME_DESIGN.md §0, 2026-07-27 · TASK-004).
    ///
    /// TEK SORUMLULUK: ilerleme durumu + <see cref="OnProgressChanged"/> yayını. Bölümün haritasını
    /// ÜRETMEK/YÜKLEMEK bunun işi DEĞİLDİR (prosedürel terrain + seed havuzu → TASK-005).
    ///
    /// Kalıcılık YOK — ilerleme şimdilik oturum içi tutulur; kayıt/yükleme sistemi geldiğinde
    /// <c>_completed</c> + <see cref="CurrentChapter"/> serileştirilir.
    /// </summary>
    public class ChapterProgress : MonoBehaviour
    {
        [SerializeField] private ChapterConfigSO _config;

        [Tooltip("Oyunun başlayacağı bölüm (normalde 1). Test için ileri bir bölümden başlatılabilir.")]
        [SerializeField, Min(1)] private int _startChapter = 1;

        private readonly HashSet<int> _completed = new();

        public ChapterConfigSO Config => _config;

        /// <summary>Oyuncunun şu an içinde olduğu bölüm (1-tabanlı).</summary>
        public int CurrentChapter { get; private set; } = 1;

        /// <summary>Toplam bölüm sayısı — config yoksa tasarım varsayılanı 8.</summary>
        public int ChapterCount => (_config != null && _config.Count > 0) ? _config.Count : 8;

        /// <summary>Bölüm değişti ya da biri tamamlandı. UI dinler (event-driven, CLAUDE.md §2).</summary>
        public event System.Action OnProgressChanged;

        private void Awake() => CurrentChapter = Mathf.Clamp(_startChapter, 1, ChapterCount);

        public bool IsCompleted(int chapter) => _completed.Contains(chapter);

        /// <summary>Bu bölüm oyuncuya açık mı? Tamamlananlar + içinde bulunulan açıktır.</summary>
        public bool IsUnlocked(int chapter) => chapter == CurrentChapter || IsCompleted(chapter);

        /// <summary>Bölümü elle değiştir (retry/atlama). Aralık dışı değerler kırpılır.</summary>
        public void SetCurrentChapter(int chapter)
        {
            chapter = Mathf.Clamp(chapter, 1, ChapterCount);
            if (chapter == CurrentChapter) return;
            CurrentChapter = chapter;
            OnProgressChanged?.Invoke();
        }

        /// <summary>İçinde bulunulan bölüm tamamlandı → işaretle, varsa bir sonrakine geç.</summary>
        public void CompleteCurrentChapter()
        {
            if (!_completed.Add(CurrentChapter)) return;   // zaten tamamlanmış
            if (CurrentChapter < ChapterCount) CurrentChapter++;
            OnProgressChanged?.Invoke();
        }

        /// <summary>Bölüm kaybedildi → TASK-007'ye göre SADECE o bölüm baştan başlar (run sıfırlanmaz).
        /// Şimdilik yalnız olayı yayar; haritayı yeni seed'le yeniden üretmek TASK-005/007'nin işi.</summary>
        public void RestartCurrentChapter() => OnProgressChanged?.Invoke();
    }
}
