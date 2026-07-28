using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HARİTA ekranındaki **8 bölümlük ilerleme yolu**nu <see cref="ChapterProgress"/>'e göre CANLI
    /// gösterir: tamamlandı / şu an / kilitli. **1 bölüm = 1 harita** (Docs/GAME_DESIGN.md §0).
    /// <see cref="ChapterProgress.OnProgressChanged"/> dinlenir (event-driven, CLAUDE.md §2).
    ///
    /// Diziler BÖLÜM sırasındadır: index 0 = Bölüm 1 … 7 = Bölüm 8. Yerleşimi editör aracı kurar
    /// (<c>SceneSetupTool.PopulateMapScreen</c>); bu bileşen yalnız DURUMDAN sorumlu — tek sorumluluk.
    ///
    /// GÖSTERİM-ODAKLI: bölüm pinlerine tıklayıp seyahat etmek YOK. Bölüm haritasını üreten/yükleyen
    /// sistem henüz yazılmadı (TASK-005) — çalışmayan bir düğme koymaktansa salt-okunur bırakıldı.
    ///
    /// (2026-07-28 / TASK-004: burada eskiden "9 harita 3×3 snake dünya" vardı; o varsayım geçersiz
    /// sayıldı. Tasarım SİLİNMEDİ → <c>Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/</c>.)
    /// </summary>
    public class WorldMapView : MonoBehaviour
    {
        [SerializeField] private ChapterProgress _progress;

        [Tooltip("8 düğüm zemini, BÖLÜM sırasında (index 0 = Bölüm 1 … 7 = Bölüm 8).")]
        [SerializeField] private Image[] _nodeBackgrounds;

        [Tooltip("Düğüm altındaki durum yazısı (BİTTİ / ŞU AN / KİLİTLİ), aynı sırada. Boş bırakılabilir.")]
        [SerializeField] private TextMeshProUGUI[] _nodeLabels;

        [Tooltip("Düğümleri bağlayan yol parçaları: index 0 = 1→2 … 6 = 7→8. Boş bırakılabilir.")]
        [SerializeField] private Image[] _connectors;

        [Tooltip("Üstteki başlık — içinde bulunulan bölümün adı + teması.")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Header("Renkler")]
        [SerializeField] private Color _currentColor   = new Color(0.72f, 0.55f, 0.28f, 1f);
        [SerializeField] private Color _completedColor = new Color(0.42f, 0.50f, 0.32f, 1f);
        [SerializeField] private Color _lockedColor    = new Color(0.16f, 0.13f, 0.10f, 0.85f);

        [Header("Durum yazıları")]
        [SerializeField] private string _currentText   = "ŞU AN";
        [SerializeField] private string _completedText = "BİTTİ";
        [SerializeField] private string _lockedText    = "KİLİTLİ";

        private void OnEnable()
        {
            if (_progress != null) _progress.OnProgressChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.OnProgressChanged -= Refresh;
        }

        public void Refresh()
        {
            int current = _progress != null ? _progress.CurrentChapter : 1;

            if (_nodeBackgrounds != null)
                for (int i = 0; i < _nodeBackgrounds.Length; i++)
                {
                    int  chapter = i + 1;
                    bool here    = chapter == current;
                    bool done    = _progress != null && _progress.IsCompleted(chapter);

                    if (_nodeBackgrounds[i] != null)
                        _nodeBackgrounds[i].color = here ? _currentColor : (done ? _completedColor : _lockedColor);

                    if (_nodeLabels != null && i < _nodeLabels.Length && _nodeLabels[i] != null)
                        _nodeLabels[i].text = here ? _currentText : (done ? _completedText : _lockedText);
                }

            // Yol parçası i, (i+1) ile (i+2) arasını bağlar → geçilmiş sayılması için i+1 < current.
            if (_connectors != null)
                for (int i = 0; i < _connectors.Length; i++)
                    if (_connectors[i] != null)
                        _connectors[i].color = (i + 1 < current) ? _completedColor : _lockedColor;

            if (_titleLabel != null)
            {
                ChapterConfigSO cfg = _progress != null ? _progress.Config : null;
                string name  = cfg != null ? cfg.NameOf(current)  : $"Bölüm {current}";
                string theme = cfg != null ? cfg.ThemeOf(current) : "?";
                _titleLabel.text = $"{name} — {theme}";
            }
        }
    }
}
