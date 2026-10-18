using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticalRPG.Prototypes.CombatLab
{
    public sealed class CombatLabController : MonoBehaviour
    {
        [SerializeField] private CombatLabConfig _config;
        [SerializeField] private CombatLabView _view;
        [SerializeField] private CombatLabHud _hud;
        private CombatLabSimulation _simulation;
        private bool _recorded;
        private bool _aimPull;
        private bool _capture;
        private string _capturePath;
        private float _pointerRefresh;
        private Vector2 _pointer;
        private LabResults _results = new LabResults();
        public CombatLabSimulation Simulation => _simulation;
        public string ResultsPath => Path.Combine(Application.persistentDataPath, "CombatLab", "comparison.json");
        public string LastTactical => DescribeLast("Tactical");
        public string LastRealtime => DescribeLast("Realtime");
        public bool AimingPull => _aimPull;
        [Serializable] private sealed class LabResult
        { public string mode; public string utc; public bool won; public float seconds; public float enemyWait; public int commands; public int chains; public int kills; public int damage; public int replayRating; }
        [Serializable] private sealed class LabResults { public List<LabResult> attempts = new List<LabResult>(); }

        private void Start()
        {
            try { if (File.Exists(ResultsPath)) _results = JsonUtility.FromJson<LabResults>(File.ReadAllText(ResultsPath)) ?? new LabResults(); }
            catch (Exception e) { Debug.LogWarning("CombatLab sonuçları okunamadı: " + e.Message); }
            _simulation = new CombatLabSimulation(_config); _hud.Bind(this, _config);
            _simulation.Changed += Refresh;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--lab-capture") { _capture = true; _capturePath = args[i + 1]; }
            if (_capture) { Application.runInBackground = true; StartMode(Array.IndexOf(args, "--lab-realtime") >= 0 ? 1 : 0); StartCoroutine(Capture()); }
            else _hud.ShowMenu();
            StartCoroutine(RefreshReadouts());
        }
        private IEnumerator RefreshReadouts()
        {
            var delay = new WaitForSecondsRealtime(.1f);
            while (true) { _hud.Refresh(); if (_simulation.Phase != LabPhase.Ready) _view.RefreshState(); yield return delay; }
        }
        private IEnumerator Capture()
        {
            yield return new WaitForSeconds(1.2f);
            if (_simulation.Mode == LabMode.Realtime) _simulation.TrySkill(LabSkill.Pull, new Vector2(0, 0));
            // A hidden Windows player may not present a backbuffer. Render the same
            // camera and HUD offscreen so unattended captures still contain pixels.
            var camera = _view.Camera;
            var canvas = _hud.GetComponent<Canvas>();
            var target = new RenderTexture(1440, 900, 24);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(_capturePath, pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Destroy(pixels); target.Release(); Destroy(target);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log("COMBAT_LAB_CAPTURE_OK " + _capturePath);
            Application.Quit(0);
        }
        public void StartMode(int mode)
        {
            _recorded = false; _aimPull = false; _simulation.Start(mode == 0 ? LabMode.Tactical : LabMode.Realtime);
            _view.Bind(_simulation, _config); _hud.ShowGame(); Refresh();
        }
        public void Restart() => StartMode((int)_simulation.Mode);
        public void OtherMode() => StartMode(_simulation.Mode == LabMode.Tactical ? 1 : 0);
        public void BackToMenu()
        {
            if (_simulation.Active && !_simulation.Paused) _simulation.TogglePause();
            _aimPull = false; _hud.ShowMenu();
        }
        public void Pause() { _simulation.TogglePause(); _aimPull = false; }
        public void Select(int id) { _aimPull = false; _simulation.Select(id); }
        public void EndTurn() { _aimPull = false; _simulation.EndTurn(); }
        public void Pull() { if (!_simulation.Active || _simulation.Paused) return; _aimPull = !_aimPull; _hud.Refresh(); }
        public void Burst() { _aimPull = false; _simulation.TrySkill(LabSkill.Burst, _simulation.Kam.Position); }
        public void PickAugment(int option) { _simulation.ChooseAugment(option); }
        public void Rate(int value)
        {
            if (!_recorded || _results.attempts.Count == 0 || _capture) return;
            _results.attempts[_results.attempts.Count - 1].replayRating = Mathf.Clamp(value, 1, 5); SaveResults(); _hud.Refresh();
        }
        private void Refresh()
        {
            if (_simulation.Phase == LabPhase.Won || _simulation.Phase == LabPhase.Lost)
            {
                if (!_recorded && !_capture)
                {
                    _recorded = true;
                    _results.attempts.Add(new LabResult { mode = _simulation.Mode.ToString(), utc = DateTime.UtcNow.ToString("O"), won = _simulation.Phase == LabPhase.Won, seconds = _simulation.Elapsed, enemyWait = _simulation.EnemyWait, commands = _simulation.Commands, chains = _simulation.Chains, kills = _simulation.Kills, damage = _simulation.DamageTaken });
                    SaveResults();
                }
            }
            _view.RefreshState(); _hud.Refresh();
        }
        private void SaveResults()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath)); File.WriteAllText(ResultsPath, JsonUtility.ToJson(_results, true)); }
            catch (Exception e) { Debug.LogWarning("CombatLab sonuç kaydı: " + e.Message); }
        }
        private string DescribeLast(string mode)
        {
            for (int i = _results.attempts.Count - 1; i >= 0; i--)
            {
                var r = _results.attempts[i]; if (r.mode != mode) continue;
                return (r.won ? "Zafer" : "Yenilgi") + " · " + Mathf.RoundToInt(r.seconds) + " sn · " + r.chains + " zincir\n" + r.commands + " komut · " + r.damage + " hasar alındı · tekrar isteği " + (r.replayRating > 0 ? r.replayRating + "/5" : "—");
            }
            return "Henüz tamamlanmış deneme yok.";
        }
        private void Update()
        {
            if (_simulation == null || _hud.MenuVisible) return;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { if (_aimPull) _aimPull = false; else Pause(); }
                if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
                if (keyboard.spaceKey.wasPressedThisFrame) { if (_simulation.Mode == LabMode.Tactical) EndTurn(); else Pause(); }
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool hasPointer = mouse != null && _view.PointerWorld(mouse.position.ReadValue(), out _pointer);
            if (keyboard != null && !_simulation.Paused)
            {
                if (keyboard.qKey.wasPressedThisFrame)
                { if (_simulation.Mode == LabMode.Realtime && hasPointer && !overUi) _simulation.TrySkill(LabSkill.Pull, _pointer); else Pull(); }
                if (keyboard.eKey.wasPressedThisFrame) Burst();
            }
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) _aimPull = false;
            if (hasPointer && !overUi && mouse.leftButton.wasPressedThisFrame)
            {
                if (_aimPull) { if (_simulation.TrySkill(LabSkill.Pull, _pointer)) _aimPull = false; }
                else
                {
                    int id = _view.PickUnit(_pointer); var unit = _simulation.Find(id);
                    if (unit != null && unit.Ally) Select(id);
                    else if (unit != null) _simulation.TryAttack(id);
                    else if (_simulation.Mode == LabMode.Tactical) _simulation.TryMove(_simulation.Nearest(_pointer));
                }
            }
            _pointerRefresh -= Time.unscaledDeltaTime;
            if (_pointerRefresh <= 0) { _pointerRefresh = .05f; _view.SetHover(_pointer, hasPointer && !overUi && _simulation.Active && !_simulation.Paused); }
            Vector2 movement = Vector2.zero;
            if (keyboard != null && _simulation.Mode == LabMode.Realtime)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            _simulation.Tick(Time.deltaTime, movement);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && _simulation != null && _simulation.Active && !_simulation.Paused && !_capture) _simulation.TogglePause(); }
        private void OnDestroy() { if (_simulation != null) _simulation.Changed -= Refresh; }
    }
}
