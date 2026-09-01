using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Grid;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// YOL BELİRLE BARI (kullanıcı isteği 2026-09-01) — harita ekranında güçlü yol taşının
    /// ALTINA ikinci bir bar koyar; çift tıklanan karo 3B haritada kesik çizgiyle işaretlenir.
    ///
    /// NEDEN AYRI, DAR KAPSAMLI BİR GİRİŞ: tam UI kurulumu ("UI - Menu Iskeleti Kur") panelleri
    /// baştan yaratıyor; mağaza fazı PlayerBuffs'ı yeniden ekleyip harita ekranının referansını
    /// kırdığı 2026-08-17 vakası bunun bedelini gösterdi. Buradaki iş yalnız EKLEMEK ve BAĞLAMAK:
    /// mevcut düğmeler yerinden oynatılır, eksik olan yaratılır, hiçbir şey yok edilmez.
    /// Menü girişi de batch girişi de aynı gövdeyi (<see cref="ApplyRouteBar"/>) çağırır.
    /// </summary>
    public static partial class SceneSetupTool
    {
        /// <summary>Açıklama şeridinin (legend) alt bloğundaki dikey yerleşim. Hem ilk kurulum
        /// hem sonradan yamalama AYNI sayıları kullansın diye burada duruyor — iki yerde ayrı
        /// yazılsaydı, biri değiştiğinde düğmeler üst üste binerdi.</summary>
        internal static class RouteBarLayout
        {
            // DİKKAT: bu düğme/etiketlerin pivotu ALT kenar (anchor 0.5,0) — y değeri nesnenin
            // ALTININ panel tabanından yüksekliğidir, merkezi değil. Düğme yüksekliği 44, sayaç
            // 30; aralıklar buna göre seçildi ve üstteki not şeridinin altı (236) korunuyor.
            public const float CountY = 152f;   // "Güçlü yol taşı: n"    → 152..182
            public const float PowerY = 106f;   // GÜÇLÜ YOL TAŞI KULLAN  → 106..150
            public const float RouteY =  58f;   // YOL BELİRLE (yeni bar) →  58..102
        }

        [MenuItem("TacticalRPG/UI - Yol Belirle Barini Ekle (harita ekrani)", false, 23)]
        public static void SetupRouteBarMenu()
        {
            bool ok = ApplyRouteBar();
            EditorUtility.DisplayDialog("Yol Belirle",
                ok ? "Bar eklendi: harita ekranindaki 'GUCLU YOL TASI KULLAN' dugmesinin altinda " +
                     "'YOL BELIRLE' duruyor, RouteMarker sahneye takildi.\n\n" +
                     "SAHNEYI KAYDET (Ctrl+S) — degisiklikler acik sahnede duruyor."
                   : "Eklenemedi: sahnede harita ekrani (MinimapTravelSelector) bulunamadi. " +
                     "Once 'UI - Menu Iskeleti Kur'.",
                "Tamam");
        }

        /// <summary>
        /// Batch girişi (Unity KAPALIYKEN):
        /// <code>
        /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
        ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupRouteBarBatch -logFile log.txt
        /// </code>
        /// </summary>
        public static void SetupRouteBarBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[YolBelirle] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (!ApplyRouteBar()) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Ortak gövde. false = kurulamadı.</summary>
        private static bool ApplyRouteBar()
        {
            // Harita ekranı KAPALI (panel deaktif) olabilir → aktif nesne araması onu bulamaz.
            var selector = FindInSceneIncludingInactive<MinimapTravelSelector>();
            if (selector == null)
            {
                Debug.LogError("[YolBelirle] Sahnede MinimapTravelSelector yok — once 'UI - Menu Iskeleti Kur'.");
                return false;
            }

            // Hiyerarşi: MapBody / MinimapBoard / Fill(=selector) ve MapBody / LegendPanel / Fill.
            Transform board  = selector.transform;
            Transform mapBody = board.parent != null ? board.parent.parent : null;
            Transform legend  = mapBody != null ? mapBody.Find("LegendPanel/Fill") : null;
            if (legend == null)
            {
                Debug.LogError("[YolBelirle] LegendPanel/Fill bulunamadi — harita ekrani beklenen yapida degil.");
                return false;
            }

            // Mevcut iki satır yukarı kayar, altta yeni bara yer açılır.
            MoveTo(legend.Find("PowerStoneCount"), RouteBarLayout.CountY);
            MoveTo(legend.Find("Btn_PowerStone"),  RouteBarLayout.PowerY);

            Transform existing = legend.Find("Btn_RouteMark");
            Button routeButton = existing != null ? existing.GetComponent<Button>() : null;
            if (routeButton == null)
            {
                routeButton = CreateUIButton(legend, "Btn_RouteMark", "YOL BELİRLE",
                    new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.RouteY), new Vector2(252f, 44f),
                    new Color(0.36f, 0.16f, 0.14f, 0.98f), 17f);
            }
            else
            {
                MoveTo(existing, RouteBarLayout.RouteY);
            }

            RouteMarker marker = EnsureRouteMarker();
            if (marker == null) return false;   // sebebi EnsureRouteMarker yazdı

            var so = new SerializedObject(selector);
            so.FindProperty("_routeButton").objectReferenceValue = routeButton;
            so.FindProperty("_routeMarker").objectReferenceValue = marker;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(selector);
            EditorUtility.SetDirty(routeButton);
            EditorSceneManager.MarkSceneDirty(selector.gameObject.scene);

            Debug.Log("[YolBelirle] Bar hazir: Btn_RouteMark + RouteMarker bagli " +
                      $"({marker.gameObject.name} uzerinde).");
            return true;
        }

        /// <summary>Yol işaretini tutan bileşen sahnede TEK olmalı — varsa bulur, yoksa
        /// GameManager'a (yoksa grid nesnesine) takar ve bağlarını yazar.</summary>
        private static RouteMarker EnsureRouteMarker()
        {
            RouteMarker marker = FindInSceneIncludingInactive<RouteMarker>();
            if (marker == null)
            {
                // PathPreview ile aynı ev: harita tıklamasını işleyen GameManager.
                var input = FindInSceneIncludingInactive<MapInputHandler>();
                GameObject host = input != null ? input.gameObject
                                : FindInSceneIncludingInactive<HexGridManager>()?.gameObject;
                if (host == null)
                {
                    Debug.LogError("[YolBelirle] RouteMarker takilacak nesne yok (GameManager/Grid bulunamadi).");
                    return null;
                }
                marker = host.AddComponent<RouteMarker>();
            }

            var mso = new SerializedObject(marker);
            mso.FindProperty("_grid").objectReferenceValue   = FindInSceneIncludingInactive<HexGridManager>();
            mso.FindProperty("_player").objectReferenceValue = FindInSceneIncludingInactive<PlayerController>();
            mso.FindProperty("_state").objectReferenceValue  = FindInSceneIncludingInactive<GameStateManager>();
            // Sis: rota yalnız KEŞFEDİLMİŞ karolardan planlansın diye gerekiyor.
            mso.FindProperty("_fog").objectReferenceValue    = FindInSceneIncludingInactive<FogOfWarManager>();

            // ANLAMI DEĞİŞEN ALANLAR — sahnede YAZILI eski değer C#'taki yeni varsayılanı EZER
            // (CLAUDE.md tuzağı). İlk sürümde iz havada süzülen DÜZ bir çizgiydi:
            //   _lift 0.85  = çizginin zeminden yüksekliği   → artık patika karo yüzeyine oturuyor
            //   _dashGap 0.45 = kesikler arası DÜNYA BİRİMİ  → artık adımın uçlarından kırpılan ORAN
            //                   (0.45 kalsaydı kesikler noktaya inerdi)
            // Bu yüzden eski varsayılanları taşıyorlar; oyuncunun elle verdiği başka bir değer
            // varsa dokunulmaz.
            MigrateDefault(mso, "_lift",    0.85f, 0.14f);
            MigrateDefault(mso, "_dashGap", 0.45f, 0.22f);
            MigrateDefault(mso, "_alpha",   0.34f, 0.42f);
            MigrateDefault(mso, "_pulse",   0.09f, 0.07f);
            mso.ApplyModifiedProperties();
            return marker;
        }

        private static void MoveTo(Transform t, float y)
        {
            if (t == null) return;
            var rt = (RectTransform)t;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        /// <summary>Sahnedeki bileşeni KAPALI nesnelerde de bulur. Harita/çanta panelleri
        /// kapalıyken duruyor; <c>FindFirstObjectByType</c> onları görmez, prefab asset'lerini
        /// de karıştırmamak için sahne nesnesi olma şartı ayrıca denetlenir.</summary>
        private static T FindInSceneIncludingInactive<T>() where T : Component
        {
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                if (candidate == null) continue;
                if (EditorUtility.IsPersistent(candidate)) continue;                 // prefab/asset
                if ((candidate.hideFlags & HideFlags.HideAndDontSave) != 0) continue; // editör içi
                if (!candidate.gameObject.scene.IsValid()) continue;
                return candidate;
            }
            return null;
        }
    }
}
