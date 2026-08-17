using System.Collections;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ÖZÜ SÖKÜLMÜŞ KARONUN CANLANDIRMASI — "karo kararacak, kuraklaşacak, hatta hafif çatlayacak"
    /// (kullanıcı isteği 2026-08-17).
    ///
    /// NEDEN AYRI BİR KATMAN: karoyu sadece <c>_BaseColor</c>'dan boyamak YETMİYOR. O renk karonun
    /// dokusuyla ÇARPILIR — yeşil çim dokusunu griye çeviremez, yalnızca koyultur. Karo bu yüzden
    /// "kendi rengine dönmüş" görünüyordu. Çözüm: karonun üstüne KENDİ yüzeyini sermek —
    /// yarı saydam kurak bir kapak + üstünde koyu çatlaklar.
    ///
    /// Animasyon iki aşamalı, çünkü "ruh sömürülüyor" hissi sıradan bir soluklaşmadan gelmiyor:
    ///   1. Kuraklık kapağı belirir → renk çekilir, karo kararır.
    ///   2. Biraz SONRA çatlaklar merkezden dışa doğru yayılır → toprak kurudu, yarıldı.
    /// Geçmişte sökülmüş karolar (savaştan dönüş, harita yenileme) animasyonsuz, doğrudan son
    /// hâlleriyle kurulur — eski bir olay tekrar oynamaz.
    /// </summary>
    public class EssenceDrainVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        [Tooltip("Çatlakların kuraklıktan ne kadar SONRA yayılmaya başlayacağı (0-1 arası oran).")]
        [SerializeField, Range(0f, 0.9f)] private float _crackDelay = 0.3f;
        [Tooltip("Çatlaklar en küçük hâlinde karonun ne kadarını kaplasın (yayılma buradan başlar).")]
        [SerializeField, Range(0.02f, 0.9f)] private float _crackStartScale = 0.12f;

        private Renderer  _cap, _cracks;
        private Transform _cracksT;
        private Color     _capColor, _crackColor;
        private float     _duration;
        private MaterialPropertyBlock _mpb;

        /// <summary>Katmanları bağla ve oynat. <paramref name="animate"/> false ise doğrudan
        /// son hâline kurulur (geçmişte sökülmüş karolar).</summary>
        public void Begin(Renderer cap, Color capColor, Renderer cracks, Color crackColor,
                          float duration, bool animate)
        {
            _cap        = cap;
            _cracks     = cracks;
            _cracksT    = cracks != null ? cracks.transform : null;
            _capColor   = capColor;
            _crackColor = crackColor;
            _duration   = duration;
            _mpb      ??= new MaterialPropertyBlock();

            if (!animate || duration <= 0.01f) { Apply(1f); return; }
            Apply(0f);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float e = 0f;
            while (e < _duration)
            {
                e += Time.deltaTime;
                Apply(Mathf.Clamp01(e / _duration));
                yield return null;
            }
            Apply(1f);
        }

        private void Apply(float k)
        {
            // Kuraklık: hızlı başlayıp yavaşlayan bir belirişle rengi çeker.
            if (_cap != null)
            {
                Color c = _capColor;
                c.a = _capColor.a * EaseOut(k);
                Paint(_cap, c);
            }

            if (_cracks == null) return;

            // Çatlaklar: kapak oturmaya başladıktan SONRA, merkezden dışa doğru yarılır.
            float g = Mathf.Clamp01((k - _crackDelay) / Mathf.Max(0.01f, 1f - _crackDelay));
            float s = Mathf.Lerp(_crackStartScale, 1f, EaseOut(g));

            if (_cracksT != null) _cracksT.localScale = new Vector3(s, 1f, s);

            Color cc = _crackColor;
            cc.a = _crackColor.a * g;
            Paint(_cracks, cc);
        }

        private void Paint(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId,     c);
            r.SetPropertyBlock(_mpb);
        }

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
    }
}
