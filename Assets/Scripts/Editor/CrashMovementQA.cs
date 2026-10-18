using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Grid;
using Object = UnityEngine.Object;

namespace TacticalRPG.Editor
{
    [InitializeOnLoad]
    public static class CrashMovementQA
    {
        private static int _phase, _moves, _attempts, _errors;
        private static double _start, _next, _moveStart;
        private static PlayerController _player;
        private static HexGridManager _grid;
        private static DisplaySettings _display;
        private static Camera _camera;
        private static readonly List<string> Notes = new List<string>();
        private static string Output => Path.GetFullPath("CrashMovementQAResults");

        static CrashMovementQA()
        {
            if (SessionState.GetBool("CrashMovementQA.Play", false))
            {
                EditorApplication.update += Tick;
                Application.logMessageReceived += OnLog;
            }
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");
            Application.logMessageReceived += OnLog;
            SessionState.SetBool("CrashMovementQA.Play", true);
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            Notes.Add($"{type}: {condition}\n{stack}");
            Save();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _next) return;
            try
            {
                if (_start == 0) { _start = EditorApplication.timeSinceStartup; Wait(8); return; }
                if (EditorApplication.timeSinceStartup - _start > 150) throw new TimeoutException("Play/movement timeout");
                if (_phase == 0)
                {
                    _player = Object.FindFirstObjectByType<PlayerController>();
                    _grid = Object.FindFirstObjectByType<HexGridManager>();
                    _display = Object.FindFirstObjectByType<DisplaySettings>();
                    _camera = Camera.main;
                    if (_player == null || _grid == null || _display == null || _camera == null)
                        throw new Exception("Required gameplay component/camera missing");
                    Notes.Add($"Play ready. Gfx={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}; cells={_grid.Cells?.Count}; start={_player.CurrentCoord}; miniature={_display.MiniatureOn}");
                    Capture("miniature_on");
                    _display.SetMiniature(false);
                    _phase = 1; Wait(1); Save(); return;
                }
                if (_phase == 1)
                {
                    Capture("miniature_off");
                    _phase = 2; Wait(0.3); Save(); return;
                }
                if (_phase == 2)
                {
                    if (_player.IsMoving)
                    {
                        if (EditorApplication.timeSinceStartup - _moveStart > 12)
                            throw new TimeoutException($"Move {_attempts} did not finish");
                        Wait(0.2); return;
                    }
                    if (_attempts > 0)
                    {
                        _moves++;
                        Notes.Add($"Move {_moves} finished at {_player.CurrentCoord}");
                        Save();
                    }
                    if (_moves >= 10) { _phase = 3; Wait(3); return; }
                    if (!_grid.TryGetCell(_player.CurrentCoord, out var start))
                        throw new Exception("Player cell missing");
                    var candidates = _grid.GetNeighbors(_player.CurrentCoord)
                        .Where(c => c.IsWalkable).ToList();
                    if (candidates.Count == 0) throw new Exception("No walkable neighbor");
                    var target = candidates[_attempts % candidates.Count];
                    var path = new HexPathfinder().FindPath(start, target, _grid);
                    if (path == null || path.Count < 2) throw new Exception("Neighbor path missing");
                    _attempts++;
                    _player.MoveAlongPath(path);
                    if (!_player.IsMoving) throw new Exception($"Move {_attempts} did not start; locked={_player.IsMovementLocked}");
                    _moveStart = EditorApplication.timeSinceStartup;
                    Notes.Add($"Move {_attempts} started toward {target.Coordinate}; locked={_player.IsMovementLocked}");
                    Save(); Wait(1); return;
                }
                if (_phase == 3)
                {
                    Capture("after_10_moves");
                    Notes.Add($"Completed {_moves} moves; captured final frame; errors={_errors}");
                    Finish(_errors == 0 ? 0 : 1);
                }
            }
            catch (Exception e)
            {
                Notes.Add(e.ToString()); Debug.LogException(e); Finish(1);
            }
        }

        private static void Capture(string name)
        {
            var rt = new RenderTexture(1280, 800, 24);
            var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            _camera.targetTexture = rt; _camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; _camera.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
        }

        private static void Save() => File.WriteAllLines(Path.Combine(Output, "progress.txt"), Notes);
        private static void Wait(double seconds) => _next = EditorApplication.timeSinceStartup + seconds;
        private static void Finish(int code)
        {
            Save(); SessionState.SetBool("CrashMovementQA.Play", false);
            EditorApplication.update -= Tick; EditorApplication.Exit(code);
        }
    }
}
