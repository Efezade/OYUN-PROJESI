using System.Collections;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// PUSU SAVAŞI BAŞLATICI (2026-10-10) — bölge kuralı oyuncuyu ZORLA savaşa soktuğunda
    /// (av birliği yakaladı, uyuyan dev uyandı). Akış: yürüyüş durdurulur → bildirim → kısa
    /// soluklanma → görev onaysız açılır (normal savaş girişi: AP bedeli, arena, yerleştirme).
    ///
    /// Pusu görevi <see cref="MissionData.IsAmbush"/> taşır: hiçbir düğüme ait değildir, dönüşte
    /// yanındaki düğüm tamamlanmış sayılmaz (bkz <see cref="ChapterNodeManager"/>).
    /// </summary>
    public class AmbushLauncher : MonoBehaviour
    {
        [SerializeField] private GameStateManager _state;
        [SerializeField] private PlayerController _player;
        [SerializeField] private NoticeFeed       _notice;
        [Tooltip("Bildirimden sonra savaş açılmadan önceki soluk (sn) — oyuncu ne olduğunu okusun.")]
        [SerializeField] private float _delay = 1.2f;

        /// <summary>Bir pusu şu an açılıyor mu (aynı anda ikincisi açılmaz).</summary>
        public bool IsBusy { get; private set; }

        /// <returns>false = overworld'de değiliz ya da zaten bir pusu açılıyor.</returns>
        public bool Launch(MissionData mission, string message, Color color)
        {
            if (IsBusy || mission == null || _state == null || _state.State != GameState.Overworld) return false;
            StartCoroutine(Run(mission, message, color));
            return true;
        }

        private IEnumerator Run(MissionData mission, string message, Color color)
        {
            IsBusy = true;
            if (_player != null) _player.RequestStop(message);
            if (_notice != null) _notice.Post(message, color, 4f);

            // Karo ortasında kesilmez: yürüyüş sıradaki karoda durana kadar bekle.
            float guard = 0f;
            while (_player != null && _player.IsMoving && guard < 4f) { guard += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(_delay);

            if (_state.State == GameState.Overworld)
            {
                _state.RequestMission(mission);
                _state.ConfirmMission();
                Debug.Log($"[Pusu] {mission.DisplayName} — {message}");
            }
            IsBusy = false;
        }
    }
}
