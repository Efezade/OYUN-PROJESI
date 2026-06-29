using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bir TileMapSO haritasını ÜSTTEN render edip RenderTexture'a "pişirir" (render-to-texture).
    /// Dokulu küpün yüzlerine basmak için. Sahnenin çok altında geçici karoları üretir, ortho
    /// kamerayla tepeden çeker, hemen temizler. Harita oranı korunur (kare RT'ye letterbox).
    /// </summary>
    public static class MapTextureBaker
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Vector3 BakeOffset = new Vector3(0f, -3000f, 0f); // sahnenin çok altı

        public static RenderTexture Bake(TileMapSO map, HexGridManager grid, GameObject placeholder, int res, Color bg)
        {
            if (map == null || grid == null || !grid.HasCells) return null;

            // 1) Geçici karoları sahnenin çok altında üret (gerçek sahneyle çakışmasın).
            var temp = new GameObject("BakeTemp").transform;
            RenderTiles(map, grid, placeholder, temp, out Vector3 center, out float ex, out float ez);

            // 2) Hedef texture.
            var rt = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32) { name = $"FaceRT_{map.name}" };
            rt.Create();

            // 3) Pişirme kamerası — ortho, tam tepede, aşağı bakar.
            var camGO = new GameObject("BakeCam");
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize  = Mathf.Max(ex, ez) * 0.5f * 1.04f; // küçük kenar payı
            cam.aspect            = 1f;
            cam.transform.position = center + new Vector3(0f, 60f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // aşağı
            cam.clearFlags        = CameraClearFlags.SolidColor;
            cam.backgroundColor   = bg;
            cam.nearClipPlane     = 0.1f;
            cam.farClipPlane      = 200f;
            cam.targetTexture     = rt;

            // Sis pişirmeyi karartmasın.
            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fogWas;

            // 4) Temizle.
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(temp.gameObject);
            return rt;
        }

        /// <summary>
        /// 6 yüzü sırayla pişirir (URP-uyumlu: kamera ENABLE + kareyi bekle; post-process kapalı).
        /// CubeRig bir korutinle çağırır. Senkron cam.Render() URP'de siyah verir — bu yüzden async.
        /// </summary>
        public static IEnumerator BakeAllRoutine(CubeFaceManager faces, HexGridManager grid,
                                                 GameObject placeholder, int res, Color bg, RenderTexture[] outRts)
        {
            if (faces == null || grid == null || !grid.HasCells) yield break;

            var camGO = new GameObject("BakeCam");
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic    = true;
            cam.aspect          = 1f;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane   = 0.1f;
            cam.farClipPlane    = 250f;
            cam.enabled         = false;
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null) { camData.renderPostProcessing = false; camData.renderShadows = false; }

            bool fogWas = RenderSettings.fog;
            for (int n = 1; n <= 6; n++)
            {
                var temp = new GameObject($"BakeTemp{n}").transform;
                RenderTiles(faces.GetFace(n), grid, placeholder, temp, out Vector3 center, out float ex, out float ez);

                var rt = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32) { name = $"FaceRT_{n}" };
                rt.Create();
                cam.orthographicSize = Mathf.Max(ex, ez) * 0.52f;
                cam.transform.SetPositionAndRotation(center + new Vector3(0f, 60f, 0f), Quaternion.Euler(90f, 0f, 0f));
                cam.targetTexture = rt;

                RenderSettings.fog = false;
                cam.enabled = true;
                yield return new WaitForEndOfFrame();   // URP bu kare rt'ye render eder
                cam.enabled = false;
                RenderSettings.fog = fogWas;

                outRts[n - 1] = rt;
                Object.DestroyImmediate(temp.gameObject);
            }
            Object.DestroyImmediate(camGO);
        }

        private static void RenderTiles(TileMapSO map, HexGridManager grid, GameObject placeholder,
                                        Transform parent, out Vector3 center, out float ex, out float ez)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue, y = 0f;
            TilePaletteSO pal = grid.TilePalette;

            foreach (var kv in grid.Cells)
            {
                Vector3 p = kv.Value.WorldPosition;
                if (p.x < minX) minX = p.x;  if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;  if (p.z > maxZ) maxZ = p.z;
                y = p.y;

                TilePaletteSO.TileEntry entry = FindEntry(pal, map.GetTileId(kv.Key));
                GameObject prefab = entry != null && entry.prefab != null ? entry.prefab : placeholder;
                if (prefab == null) continue;

                GameObject go = Object.Instantiate(prefab, parent);
                go.transform.position      = p + BakeOffset;
                go.transform.localRotation = Quaternion.identity;
                if (entry != null && entry.prefab == null) Tint(go, entry.editorColor);
            }
            center = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f) + BakeOffset;
            ex = maxX - minX; ez = maxZ - minZ;
        }

        private static TilePaletteSO.TileEntry FindEntry(TilePaletteSO pal, string id)
        {
            if (pal == null || pal.tiles == null) return null;
            string key = string.IsNullOrEmpty(id) ? "default" : id;
            TilePaletteSO.TileEntry def = null;
            foreach (var t in pal.tiles)
            {
                if (t.id == key)       return t;
                if (t.id == "default") def = t;
            }
            return def ?? (pal.tiles.Count > 0 ? pal.tiles[0] : null);
        }

        private static void Tint(GameObject go, Color color)
        {
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, color);
            r.SetPropertyBlock(mpb);
        }
    }
}
