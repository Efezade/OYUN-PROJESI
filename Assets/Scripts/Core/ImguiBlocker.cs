using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// IMGUI istem/HUD pencerelerinin ekran alanlarını toplar; harita girişi
    /// (<see cref="MapInputHandler"/>) tıklamanın bir panelin ÜSTÜNDE olup olmadığını buradan sorar.
    ///
    /// Neden gerekli: <c>Update()</c> her karede <c>OnGUI()</c>'den ÖNCE çalışır. Oyuncu "Evet,
    /// Isinlan" gibi bir butona bastığında aynı tık önce haritaya raycast edilir ve panelin
    /// arkasındaki karoda yol önizlemesi açar. uGUI'deki <c>EventSystem.IsPointerOverGameObject</c>
    /// IMGUI'de çalışmadığı için panel alanları burada elle bildirilir.
    ///
    /// Kullanım: OnGUI çizen HUD <see cref="Register"/> ile pencere Rect'ini (GUI koordinatı,
    /// sol-ÜST orijin) bildirir; girişi kesen taraf <see cref="IsPointerOver"/> sorar. Sorgu bir
    /// ÖNCEKİ karenin kayıtlarına bakar — panel zaten tıklanmadan önce ekranda durduğu için doğru
    /// sonuç verir. Dünya üstüne çizilen etiketler (ör. çöküş AP sayacı) KAYDEDİLMEZ; onlar tıklamayı
    /// engellememeli.
    /// </summary>
    public static class ImguiBlocker
    {
        private static readonly List<Rect> _thisFrame = new List<Rect>();
        private static readonly List<Rect> _lastFrame = new List<Rect>();
        private static int _frame = -1;

        /// <summary>OnGUI içinden çağrılır: bu panelin GUI-koordinatlı (sol-ÜST orijin) alanı.</summary>
        public static void Register(Rect guiRect)
        {
            SyncFrame();
            // OnGUI kare başına birkaç kez çalışır (Layout/Repaint/event) — aynı rect'i biriktirme.
            for (int i = 0; i < _thisFrame.Count; i++)
                if (_thisFrame[i] == guiRect) return;
            _thisFrame.Add(guiRect);
        }

        /// <summary>Fare (<c>Input.mousePosition</c>, sol-ALT orijin) bir HUD panelinin üstünde mi?</summary>
        public static bool IsPointerOver(Vector3 mousePosition)
        {
            SyncFrame();
            if (_lastFrame.Count == 0) return false;

            // Kayıtlı Rect'ler HudScale'in SANAL ekranındadır (GUI.matrix ile ölçeklenir), fare ise
            // gerçek piksel — çarpanla bölünmezse 4K'da panel alanı yarı yerde sanılırdı.
            var p = HudScale.ToGui(mousePosition);
            for (int i = 0; i < _lastFrame.Count; i++)
                if (_lastFrame[i].Contains(p)) return true;
            return false;
        }

        // Kare değişince bu karenin kayıtları "önceki kare" olur (Update, OnGUI'den önce çalıştığı
        // için sorgu her zaman en son ÇİZİLMİŞ panelleri görür).
        private static void SyncFrame()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _lastFrame.Clear();
            _lastFrame.AddRange(_thisFrame);
            _thisFrame.Clear();
        }
    }
}
