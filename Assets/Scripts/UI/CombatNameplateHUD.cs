using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Savaş/yerleştirme ekranında her birimin BAŞININ ÜSTÜNE ismini + bölümlü can barını çizer
    /// (dost = yeşil, düşman = kırmızı, kalkan = mavi, aktif birim = sarı isim). Barın sağında
    /// güncel can sayısı durur. Birim hasar alınca: o an kırmızı "-N" belirir, can sayısı/bar bir
    /// an bekler, sonra gerçek değere düşer ve "-N" kaybolur (juice). Can kaybını event yerine
    /// kare başına POLLING ile yakalar (gecikmeli "shown" değeri için en sade yol).
    ///
    /// Ayrıca her birime savaş başına BENZERSİZ görünen isim atar: sınıfın isim havuzundan
    /// (CharacterClassData.UnitNames) ya da havuz boşsa "Sınıf 1 / Sınıf 2…"; komutan (Kam) numarasız.
    /// İsim Unit.SetInstanceName ile yazılır → tur paneli ve savaş mesajları da aynı ismi kullanır.
    ///
    /// Geçici whitebox UI — cila aşamasında dünya-uzayı uGUI nameplate prefab'ına taşınacak.
    /// </summary>
    public class CombatNameplateHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager _state;
        [SerializeField] private UnitManager      _unitManager;
        [Tooltip("Aktif birimin ismini vurgulamak için (opsiyonel).")]
        [SerializeField] private TurnManager      _turnManager;
        [Tooltip("Genelde BOŞ bırak — runtime'da ekranı çizen kamera otomatik seçilir. Sadece zorlamak istersen ata.")]
        [SerializeField] private Camera           _camera;

        [Header("Yerleşim (dünya/ekran birimi)")]
        [Tooltip("İsim+canın birimin merkezinden ne kadar yukarıda duracağı (dünya birimi).")]
        [SerializeField] private float _headHeight = 1.25f;
        [SerializeField] private float _barWidth   = 70f;
        [SerializeField] private float _barHeight  = 9f;
        [SerializeField] private float _nameHeight = 16f;
        [SerializeField] private float _nameWidth  = 130f;

        [Header("Bölümler / can sayısı")]
        [Tooltip("Bir bölüm kaç can? (örn. 2 → 10 can = 5 bölüm, 4 hasar = 2 bölüm).")]
        [SerializeField, Min(1)] private int   _hpPerSegment    = 2;
        [Tooltip("Bu sayıdan fazla bölüm gerekiyorsa çizgiler atlanır (çok canlı birimde kalabalık olmasın).")]
        [SerializeField, Min(2)] private int   _maxSegmentLines = 40;
        [SerializeField]         private float _segmentLineWidth = 1f;
        [Tooltip("Hasar sayısının ('-4') ekranda kalma süresi — sonra can düşer.")]
        [SerializeField]         private float _damagePopupDuration = 0.6f;

        [Header("Renkler")]
        [SerializeField] private Color _allyColor    = new(0.32f, 0.80f, 0.38f);
        [SerializeField] private Color _enemyColor   = new(0.88f, 0.30f, 0.25f);
        [SerializeField] private Color _shieldColor  = new(0.45f, 0.80f, 1f);
        [SerializeField] private Color _barBack      = new(0.08f, 0.08f, 0.08f, 0.88f);
        [SerializeField] private Color _segmentLine  = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _allyChip     = new(0.10f, 0.32f, 0.14f, 0.85f);
        [SerializeField] private Color _enemyChip    = new(0.36f, 0.10f, 0.09f, 0.85f);
        [SerializeField] private Color _activeName   = new(1f, 0.92f, 0.30f);
        [SerializeField] private Color _damageColor  = new(1f, 0.34f, 0.28f);

        // baseName → o sınıftan kaç birim isimlendirildi (numaralandırma + havuz indeksi).
        private readonly Dictionary<string, int> _nameCounts = new();
        // Birim → gösterilen can durumu (gecikmeli "shown" + bekleyen hasar).
        private readonly Dictionary<Unit, HpTrack> _tracks = new();
        private readonly List<Unit> _pruneBuffer = new();

        private Texture2D _tex;
        private GUIStyle  _nameStyle, _hpStyle;

        private class HpTrack { public int shown; public int actual; public int pending; public float timer; }

        private bool BattleActive =>
            _state != null && (_state.State == GameState.Combat || _state.State == GameState.Deployment);

        private void OnEnable()
        {
            if (_state != null) _state.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
        }

        // Savaş bitince/overworld'e dönünce isim sayaçları + can izleri sıfırlanır.
        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Overworld) { _nameCounts.Clear(); _tracks.Clear(); }
        }

        // ── Kare başı: isim ata + can izlerini güncelle (hafif; sadece birkaç birim) ───
        private void Update()
        {
            if (!BattleActive || _unitManager == null) return;

            var units = _unitManager.Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null || !u.IsAlive) continue;
                if (!u.HasInstanceName) AssignName(u);
                UpdateHpTrack(u);
            }
            PruneTracks();
        }

        private void AssignName(Unit u)
        {
            CharacterClassData data = u.Card != null ? u.Card.Data : null;
            string baseName = data != null ? data.ClassName : u.DisplayName;

            int n = _nameCounts.TryGetValue(baseName, out int c) ? c : 0;
            IReadOnlyList<string> pool = data != null ? data.UnitNames : null;

            string assigned;
            if (pool != null && n < pool.Count && !string.IsNullOrEmpty(pool[n]))
                assigned = pool[n];                  // havuzdan özel isim
            else if (data != null && data.IsCommander)
                assigned = baseName;                 // komutan tek → numarasız ("Kam")
            else
                assigned = $"{baseName} {n + 1}";    // "Goblin 1", "Goblin 2"…

            _nameCounts[baseName] = n + 1;
            u.SetInstanceName(assigned);
        }

        // Can değişimini polling ile yakala: hasar alınca "shown" beklesin, süre dolunca düşsün.
        private void UpdateHpTrack(Unit u)
        {
            if (!_tracks.TryGetValue(u, out HpTrack tr))
            {
                _tracks[u] = new HpTrack { shown = u.CurrentHP, actual = u.CurrentHP };
                return;
            }

            int cur = u.CurrentHP;
            if (cur < tr.actual)          // hasar aldı
            {
                tr.pending += tr.actual - cur;   // birden çok vuruşu biriktir
                tr.actual   = cur;
                tr.timer    = _damagePopupDuration;
            }
            else if (cur > tr.actual)     // iyileşme vb.
            {
                tr.actual = cur;
                if (tr.timer <= 0f) tr.shown = cur;
            }

            if (tr.timer > 0f)
            {
                tr.timer -= Time.deltaTime;
                if (tr.timer <= 0f) { tr.shown = tr.actual; tr.pending = 0; } // "-N" kaybolur, can düşer
            }
            else tr.shown = tr.actual;
        }

        private void PruneTracks()
        {
            _pruneBuffer.Clear();
            foreach (var kv in _tracks)
                if (kv.Key == null || !kv.Key.IsAlive) _pruneBuffer.Add(kv.Key);
            for (int i = 0; i < _pruneBuffer.Count; i++) _tracks.Remove(_pruneBuffer[i]);
        }

        // ── Çizim ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!BattleActive || _unitManager == null) return;
            Camera cam = ResolveRenderCamera();
            if (cam == null) return;

            EnsureStyles();
            Unit active = (_turnManager != null && _turnManager.CombatActive) ? _turnManager.CurrentUnit : null;

            var units = _unitManager.Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null || !u.IsAlive) continue;

                Vector3 head = u.transform.position + Vector3.up * _headHeight;
                Vector3 vp   = cam.WorldToViewportPoint(head);
                if (vp.z <= 0f) continue;            // kamera arkasında → atla

                // Viewport (0..1, alt-sol orijin) → IMGUI nokta uzayı (üst-sol orijin).
                // WorldToScreenPoint PİKSEL döndürdüğünden HiDPI/Windows ölçeklemede OnGUI (nokta)
                // ile ayrışıp nameplate sağ-üste kayıyordu. Normalize viewport'u doğrudan IMGUI'nin
                // tanımı gereği [0..Screen.width]×[0..Screen.height] olan alanıyla çarpmak DPI'dan
                // bağımsız doğru sonucu verir (ekstra API yok → derleme güvenli).
                float gx = vp.x * Screen.width;
                float gy = (1f - vp.y) * Screen.height;

                int shown = u.CurrentHP, pending = 0;
                if (_tracks.TryGetValue(u, out HpTrack tr)) { shown = tr.shown; pending = tr.timer > 0f ? tr.pending : 0; }

                DrawNameplate(u, gx, gy, u == active, shown, pending);
            }
        }

        // Ekrana ASIL çizen kamerayı seçer: ekrana (RenderTexture'a değil) çizen, en yüksek
        // depth'li (en son = en üstte render eden) etkin kamera. Sahnede yanlışlıkla birden çok
        // "MainCamera" olunca (ör. Unity'nin temizlenmemiş varsayılan kamerası) Camera.main yanlış
        // olanı döndürüp nameplate'leri kaydırıyordu; bu, gerçek render kamerasıyla hizalar.
        private Camera ResolveRenderCamera()
        {
            Camera best = null;
            Camera[] cams = Camera.allCameras;          // yalnızca etkin kameralar
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c.targetTexture != null) continue;  // ekrana çizmeyeni atla
                if (best == null || c.depth > best.depth) best = c;
            }
            return best != null ? best : (_camera != null ? _camera : Camera.main);
        }

        private void DrawNameplate(Unit u, float x, float y, bool isActive, int shownHP, int pendingDmg)
        {
            bool  ally = u.Team == UnitTeam.Player;
            int   max  = Mathf.Max(1, u.MaxHP);
            float bx   = x - _barWidth * 0.5f;
            float by   = y - _barHeight * 0.5f;

            // Arka plan
            GUI.color = _barBack;
            GUI.DrawTexture(new Rect(bx, by, _barWidth, _barHeight), Tex());

            // Can dolgusu (gösterilen/lagged değer)
            float frac = Mathf.Clamp01((float)shownHP / max);
            GUI.color = ally ? _allyColor : _enemyColor;
            GUI.DrawTexture(new Rect(bx, by, _barWidth * frac, _barHeight), Tex());

            // Kalkan (dolgunun sağına eklenir)
            if (u.Shield > 0)
            {
                float start = _barWidth * frac;
                float sw    = Mathf.Min(_barWidth - start, _barWidth * ((float)u.Shield / max));
                if (sw > 0f)
                {
                    GUI.color = _shieldColor;
                    GUI.DrawTexture(new Rect(bx + start, by, sw, _barHeight), Tex());
                }
            }

            // Bölüm çizgileri (her _hpPerSegment canda bir; çok fazlaysa atla)
            int segs = Mathf.Max(1, Mathf.CeilToInt((float)max / _hpPerSegment));
            if (segs <= _maxSegmentLines)
            {
                GUI.color = _segmentLine;
                for (int s = 1; s < segs; s++)
                {
                    float fx = bx + _barWidth * (s * _hpPerSegment / (float)max);
                    if (fx < bx + _barWidth - 0.5f)
                        GUI.DrawTexture(new Rect(fx, by, _segmentLineWidth, _barHeight), Tex());
                }
            }

            GUI.color = Color.white;

            // İsim çipi + metni (barın üstünde)
            var chip = new Rect(x - _nameWidth * 0.5f, by - _nameHeight - 1f, _nameWidth, _nameHeight);
            GUI.color = ally ? _allyChip : _enemyChip;
            GUI.DrawTexture(chip, Tex());
            GUI.color = Color.white;
            DrawText(chip, u.DisplayName, _nameStyle, isActive ? _activeName : Color.white);

            // Güncel can sayısı (barın sağında)
            var numRect = new Rect(bx + _barWidth + 5f, by - 3f, 30f, _barHeight + 6f);
            DrawText(numRect, shownHP.ToString(), _hpStyle, Color.white);

            // Hasar popup'ı ("-4") — can sayısının hemen sağında, kırmızı
            if (pendingDmg > 0)
            {
                var dmgRect = new Rect(numRect.xMax + 2f, by - 3f, 40f, _barHeight + 6f);
                DrawText(dmgRect, $"-{pendingDmg}", _hpStyle, _damageColor);
            }
        }

        // Gölgeli metin — parlak sahne üstünde okunaklı olsun.
        private void DrawText(Rect r, string text, GUIStyle style, Color color)
        {
            var shadow = new Rect(r.x + 1f, r.y + 1f, r.width, r.height);
            GUI.color = Color.white;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(shadow, text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        private void EnsureStyles()
        {
            if (_nameStyle == null)
                _nameStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11 };
            if (_hpStyle == null)
                _hpStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 12 };
        }

        private Texture2D Tex()
        {
            if (_tex == null)
            {
                _tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _tex.SetPixel(0, 0, Color.white);
                _tex.Apply();
            }
            return _tex;
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
