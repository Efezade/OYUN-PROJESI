using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// IMGUI HUD'ları için ÇÖZÜNÜRLÜKTEN BAĞIMSIZ ölçekleme (uGUI'deki CanvasScaler'ın IMGUI karşılığı).
    ///
    /// SORUN: <c>OnGUI</c> ham piksel koordinatlarıyla çalışır. 1920x1080'de doğru görünen bir panel
    /// 4K'da yarı boyuta düşer (yazılar minicik olur), dar/geniş en-boy oranlarında da konumlar kayar.
    ///
    /// ÇÖZÜM: Tüm HUD çizimi <see cref="ReferenceWidth"/>x<see cref="ReferenceHeight"/> SANAL bir
    /// ekrana yapılır; <c>GUI.matrix</c> bunu gerçek ekrana tek bir çarpanla ölçekler. Böylece panel
    /// boyutları/konumları/yazı boyutları her çözünürlükte AYNI oranda kalır.
    ///
    /// Çarpan en-boy oranından bağımsız olsun diye min(genişlik, yükseklik) kullanılır → panel asla
    /// ekran dışına taşmaz; geniş ekranlarda fazlalık kenarda boşluk olarak kalır.
    ///
    /// KULLANIM (her OnGUI'de):
    /// <code>
    /// private void OnGUI()
    /// {
    ///     if (gizliyse) return;                    // erken çıkışlar ÖLÇEKTEN ÖNCE
    ///     using (HudScale.Scaled())                // using → erken return'de bile matris geri alınır
    ///     {
    ///         var r = new Rect(HudScale.Width - 200f, 12f, 188f, 60f);   // Screen.width DEĞİL!
    ///         ...
    ///     }
    /// }
    /// </code>
    /// Ölçek içindeyken <c>Screen.width/height</c> KULLANILMAZ — yerine
    /// <see cref="Width"/>/<see cref="Height"/> (sanal ekran ölçüsü) kullanılır.
    /// </summary>
    public static class HudScale
    {
        /// <summary>HUD'ların tasarlandığı sanal ekran.</summary>
        public const float ReferenceWidth  = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>
        /// GENEL UI BÜYÜTME ÇARPANI — tek yerden tüm HUD'ların (panel, buton, yazı, zaman sayacı)
        /// boyutunu ölçekler. 1 = tasarım boyutu, 1.5 = her şey %50 daha büyük.
        /// Yazılar küçük kalıyorsa BURAYI değiştir; tek tek fontSize ayarlamaya gerek yok.
        /// </summary>
        public const float UiScale = 1.5f;

        /// <summary>Sanal ekrandan gerçek ekrana çarpan. 1080p'de <see cref="UiScale"/>, 4K'da 2 katı.</summary>
        public static float Factor
        {
            get
            {
                float f = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
                f *= UiScale;
                return f > 0.0001f ? f : UiScale;   // ekran ölçüsü daha okunmadıysa güvenli varsayılan
            }
        }

        /// <summary>Sanal ekran genişliği. Geniş ekranda 1920'den BÜYÜK olur (fazlalık kenarda boşluk).</summary>
        public static float Width  => Screen.width  / Factor;

        /// <summary>Sanal ekran yüksekliği.</summary>
        public static float Height => Screen.height / Factor;

        /// <summary>
        /// Gerçek ekran noktasını (sol-ALT orijin, ör. <c>Input.mousePosition</c> veya
        /// <c>Camera.WorldToScreenPoint</c>) ölçekli GUI koordinatına (sol-ÜST orijin) çevirir.
        /// Dünya üstüne etiket çizen HUD'lar (nameplate, çöküş sayacı) bunu kullanır.
        /// </summary>
        public static Vector2 ToGui(Vector3 screenPoint)
        {
            float f = Factor;
            return new Vector2(screenPoint.x / f, (Screen.height - screenPoint.y) / f);
        }

        /// <summary>OnGUI gövdesini saran ölçek kapsamı. <c>using</c> ile kullan — erken
        /// <c>return</c> olsa bile GUI.matrix eski haline döner.</summary>
        public static Scope Scaled() => new Scope(Factor);

        public struct Scope : System.IDisposable
        {
            private readonly Matrix4x4 _saved;

            public Scope(float factor)
            {
                _saved = GUI.matrix;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                           new Vector3(factor, factor, 1f));
            }

            public void Dispose() => GUI.matrix = _saved;
        }
    }
}
