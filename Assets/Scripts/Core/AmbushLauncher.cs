using System.Collections;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.UI;

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

        [Header("Av birliğiyle yüzleşme")]
        [SerializeField] private CameraZoomSettings _zoom;
        [SerializeField, Min(0.1f)] private float _approachSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float _encounterDistance = 0.85f;
        [SerializeField, Min(0.1f)] private float _encounterHold = 1f;
        [SerializeField, Range(0.5f, 1f)] private float _encounterZoom = 0.75f;
        [SerializeField, Min(0.01f)] private float _fadeDuration = 0.35f;
        [SerializeField] private Color _transitionColor = new(0.06f, 0.025f, 0.09f, 1f);

        private float _fade;
        private bool _holdsMovementLock;
        private System.Action<bool> _completion;

        private void Awake()
        {
            if (_zoom == null) _zoom = FindFirstObjectByType<CameraZoomSettings>();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Finish(false);
        }

        /// <summary>Bir pusu şu an açılıyor mu (aynı anda ikincisi açılmaz).</summary>
        public bool IsBusy { get; private set; }

        /// <returns>false = overworld'de değiliz ya da zaten bir pusu açılıyor.</returns>
        public bool Launch(MissionData mission, string message, Color color)
            => Begin(mission, message, color, null, null, null);

        public bool LaunchEncounter(MissionData mission, string message, Color color,
            GameObject attacker, CharacterAnimationDriver animation, System.Action<bool> completed)
        {
            if (attacker == null || _player == null) return false;
            return Begin(mission, message, color, attacker, animation, completed);
        }

        private bool Begin(MissionData mission, string message, Color color, GameObject attacker,
            CharacterAnimationDriver animation, System.Action<bool> completed)
        {
            if (!isActiveAndEnabled || IsBusy || mission == null || _state == null || _state.State != GameState.Overworld) return false;
            IsBusy = true;
            _completion = completed;
            if (_player != null)
            {
                _holdsMovementLock = true;
                _player.SetMovementLocked(true);
            }
            StartCoroutine(Run(mission, message, color, attacker, animation));
            return true;
        }

        private IEnumerator Run(MissionData mission, string message, Color color,
            GameObject attacker, CharacterAnimationDriver animation)
        {
            bool enteredBattle = false;
            bool encounter = attacker != null;
            try
            {
                if (_player != null) _player.RequestStop(message);
                if (_notice != null) _notice.Post(message, color, 4f);

                // Kam mevcut adımını tamamlar; animasyon sırasında yeni rota başlatamaz.
                float guard = 0f;
                while (_player != null && _player.IsMoving && guard < 4f)
                { guard += Time.deltaTime; yield return null; }
                if (_player != null && _player.IsMoving) yield break;
                if (_state.State != GameState.Overworld) yield break;

                if (encounter)
                {
                    if (attacker == null || _player == null) yield break;
                    if (_zoom != null) _zoom.SetCinematicZoom(_encounterZoom);
                    Vector3 direction = attacker.transform.position - _player.transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.0001f) direction = -_player.transform.forward;
                    Vector3 target = _player.transform.position + direction.normalized * _encounterDistance;
                    target.y = attacker.transform.position.y;
                    while (attacker != null && (attacker.transform.position - target).sqrMagnitude > 0.0001f)
                    {
                        if (_state.State != GameState.Overworld) yield break;
                        attacker.transform.position = Vector3.MoveTowards(attacker.transform.position, target,
                            Mathf.Max(0.1f, _approachSpeed) * Time.deltaTime);
                        yield return null;
                    }
                    if (attacker == null) yield break;
                    // Yürüme sürücüsü duruşa geçsin; gerçek goblin Attack klibi yerinde oynar.
                    yield return new WaitForSeconds(0.08f);
                    if (animation != null)
                    {
                        animation.FaceTowards(_player.transform.position);
                        animation.PlayAttack();
                    }
                    yield return new WaitForSeconds(_encounterHold);
                    if (attacker == null || _state.State != GameState.Overworld) yield break;
                    yield return FadeTo(1f);
                }
                else yield return new WaitForSeconds(_delay);

                if (_state.State != GameState.Overworld) yield break;
                if (_zoom != null && encounter) _zoom.SetCinematicZoom(1f);
                _state.RequestMission(mission);
                _state.ConfirmMission();
                enteredBattle = _state.State == GameState.Deployment;
                Debug.Log($"[Pusu] {mission.DisplayName} — {message}");
                if (encounter) yield return FadeTo(0f);
            }
            finally { Finish(enteredBattle); }
        }

        private IEnumerator FadeTo(float target)
        {
            float from = _fade;
            float duration = Mathf.Max(0.01f, _fadeDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                _fade = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _fade = target;
        }

        private void Finish(bool enteredBattle)
        {
            if (!IsBusy && !_holdsMovementLock) return;
            if (_zoom != null) _zoom.SetCinematicZoom(1f);
            if (_holdsMovementLock && _player != null) _player.SetMovementLocked(false);
            _holdsMovementLock = false;
            _fade = 0f;
            IsBusy = false;
            var completed = _completion;
            _completion = null;
            completed?.Invoke(enteredBattle);
        }

        private void OnGUI()
        {
            if (_fade <= 0f) return;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            Color color = _transitionColor;
            color.a *= _fade;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }
}
