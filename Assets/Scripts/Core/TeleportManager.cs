using System.Collections;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// PORTAL (teleport) karoları — Tile Painter'dan boyanır, adaları bağlar.
    /// Aynı id'li ("portalN") İKİ karo (herhangi adalarda) bir ÇİFT oluşturur. Oyuncu bir portala
    /// basınca "geç?" istemi çıkar; Evet → eşleştiği portal karosuna (gerekirse başka adaya) ışınlanır.
    /// Kullanıcı portal çiftlerini istediği adalara/yerlere boyayarak yönetir (1↔2, 5↔6 gibi).
    /// </summary>
    public class TeleportManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private WorldGridManager  _world;
        [SerializeField] private PlayerController  _player;
        [Tooltip("Opsiyonel — atanmışsa istem yalnız Overworld'de gösterilir.")]
        [SerializeField] private GameStateManager  _state;
        [Tooltip("Bu ön ekle başlayan karo id'leri portaldır (Tile Painter'da: portal1, portal2 …).")]
        [SerializeField] private string _portalPrefix = "portal";

        private bool          _pending;
        private string        _pendingId;
        private int           _pendingMap;
        private HexCoordinate _pendingCoord;

        private void OnEnable()
        {
            if (_player != null) _player.OnMoved += HandleMoved;
        }
        private void OnDisable()
        {
            if (_player != null) _player.OnMoved -= HandleMoved;
        }

        private void HandleMoved(HexCoordinate coord)
        {
            _pending = false;
            if (_grid == null || _grid.TileMap == null || _world == null) return;

            string id = _grid.TileMap.GetTileId(coord);
            if (string.IsNullOrEmpty(id) || !id.StartsWith(_portalPrefix)) return;

            if (FindPortalPair(id, _world.CurrentMap, coord, out int map, out HexCoordinate dest))
            {
                _pending      = true;
                _pendingId    = id;
                _pendingMap   = map;
                _pendingCoord = dest;
            }
        }

        // Tüm 9 adayı tara: aynı id'li, KENDİSİ olmayan ilk portal karosu = eş.
        private bool FindPortalPair(string portalId, int fromMap, HexCoordinate fromCoord,
                                    out int map, out HexCoordinate coord)
        {
            map = 0; coord = default;
            for (int i = 1; i <= 9; i++)
            {
                TileMapSO tm = _world.GetMap(i);
                if (tm == null) continue;
                foreach (var a in tm.assignments)
                {
                    if (a.tileId != portalId) continue;
                    if (i == fromMap && a.coord == fromCoord) continue; // basılan portalın kendisi
                    map = i; coord = a.coord; return true;
                }
            }
            return false;
        }

        // Ayrış (toz ol) → ışınla → yeniden oluş. Efekt boyunca girişi kilitle (IsBusy).
        private IEnumerator TeleportRoutine(int map, HexCoordinate coord)
        {
            var dust = _player != null ? _player.GetComponent<TeleportDustEffect>() : null;
            if (_world != null) _world.SetTeleporting(true);

            if (dust != null && dust.HasModel) yield return dust.Dissolve();
            if (_world != null) _world.TeleportTo(map, coord);   // model hedefe taşınır (gizli)
            if (dust != null && dust.HasModel) yield return dust.Reassemble();

            if (_world != null) _world.SetTeleporting(false);
        }

        private void OnGUI()
        {
            if (!_pending) return;
            if (_state != null && _state.State != GameState.Overworld) return;
            if (_player != null && _player.IsMoving) return;
            if (_world != null && _world.IsBusy) return;

            const float w = 320f, h = 92f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 130f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"Portal — Ada {_pendingMap}'e geç?");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Evet, Isinlan", GUILayout.Height(34)))
            {
                _pending = false;
                StartCoroutine(TeleportRoutine(_pendingMap, _pendingCoord));
            }
            if (GUILayout.Button("Hayir", GUILayout.Height(34))) _pending = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
