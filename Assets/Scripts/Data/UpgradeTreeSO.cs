using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// KİTAP'TAKİ GELİŞTİRME AĞAÇLARININ ortak şekli (2026-10-03): öz harcanarak açılan ve
    /// yükseltilen düğümler. İki örneği var:
    ///   • <see cref="KamSkillTreeSO"/> — Kam'ın büyüleri (YETENEK sekmesi).
    ///   • <see cref="AugmentTreeSO"/>  — davulda çıkan değiştirilebilir karolar (KAROLAR sekmesi).
    ///
    /// Neden ortak taban: iki ağacın düğüm kuralı (ön koşul · açık başlar · seviye · bedel ·
    /// sayfadaki yer) birebir aynı; ayrı ayrı yazılsaydı KİTAP sayfası, ilerleme ve kurulum kodu
    /// iki kopya olurdu. Ağaca ÖZGÜ olan tek şey düğüm kimliğinin hangi katalogda arandığı
    /// (<see cref="IsKnownId"/>) ve seviyenin girdiyi nasıl büyüttüğü (ilerleme sınıfında).
    /// </summary>
    public abstract class UpgradeTreeSO : ScriptableObject
    {
        /// <summary>Ağaçtaki tek bir düğüm (büyü ya da karo).</summary>
        [System.Serializable]
        public class Node
        {
            [Tooltip("Kataloğdaki girdinin id'si (büyü: gok_atesi … · karo: ocak, diken …).")]
            [SerializeField] private string _skillId;   // ad tarihsel: önce yalnız büyü ağacı vardı

            [Tooltip("Bu düğüm açılmadan önce AÇILMIŞ olması gereken düğümün id'si. " +
                     "Boş = kök (ön koşulsuz).")]
            [SerializeField] private string _requires;

            [Tooltip("Oyunun başında AÇIK gelir mi? Davul her vuruşta kart sunmak zorunda — " +
                     "her havuzda en az biri açık başlamalı.")]
            [SerializeField] private bool _unlockedAtStart;

            [Tooltip("Kaç seviyeye kadar yükseltilebilir (1 = yalnız açılır, yükseltilemez).")]
            [SerializeField, Min(1)] private int _maxLevel = 3;

            [Tooltip("Düğümü AÇMANIN öz bedeli.")]
            [SerializeField] private EssenceAmount[] _unlockCost;

            [Tooltip("Bir seviye YÜKSELTMENİN taban bedeli. Gerçek bedel bununla mevcut seviyenin " +
                     "çarpımıdır (2. seviye ×1, 3. seviye ×2 ...) — sonraki seviye hep daha pahalı.")]
            [SerializeField] private EssenceAmount[] _levelCost;

            [Tooltip("Her seviyede etkinin BÜYÜKLÜĞÜNE eklenen miktar (hasar/şifa/stat). Karo " +
                     "ağacında işaret karttan gelir: eksi etkili karo seviyede daha da eksiye gider.")]
            [SerializeField] private int _magnitudePerLevel = 2;

            [Tooltip("Her seviyede etki YARIÇAPINA eklenen hex. 0 = alan büyümez (yalnız güç artar). " +
                     "Dikkat: yarıçap alanı KAREsel büyütür (1 → 7 hex, 2 → 19 hex).")]
            [SerializeField, Min(0)] private int _radiusPerLevel;

            [Tooltip("Her seviyede İTME mesafesine eklenen karo (yalnız büyü: Yel Ata gibi).")]
            [SerializeField, Min(0)] private int _pushPerLevel;

            [Tooltip("Her seviyede SERSEMLETME süresine eklenen tur (yalnız büyü: Taş Kesilme gibi).")]
            [SerializeField, Min(0)] private int _stunPerLevel;

            [Tooltip("Her seviyede kaç karo DAHA yerleştirilir (yalnız karo: Taş Duvar gibi arazi kartları).")]
            [SerializeField, Min(0)] private int _tilesPerLevel;

            [Tooltip("Düğümün KİTAP sayfasındaki yeri (piksel, sayfanın ortasına göre).")]
            [SerializeField] private Vector2 _graphPos;

            /// <summary>Düğümün kataloğdaki kimliği.</summary>
            public string Id                => _skillId;
            /// <summary>Eski ad — büyü ağacı kodu bunu kullanıyor (<see cref="Id"/> ile aynı).</summary>
            public string SkillId           => _skillId;
            public string Requires          => _requires;
            public bool   UnlockedAtStart   => _unlockedAtStart;
            public int    MaxLevel          => Mathf.Max(1, _maxLevel);
            public IReadOnlyList<EssenceAmount> UnlockCost => _unlockCost;
            public IReadOnlyList<EssenceAmount> LevelCost  => _levelCost;
            public int     MagnitudePerLevel => _magnitudePerLevel;
            public int     RadiusPerLevel    => _radiusPerLevel;
            public int     PushPerLevel      => _pushPerLevel;
            public int     StunPerLevel      => _stunPerLevel;
            public int     TilesPerLevel     => _tilesPerLevel;
            public Vector2 GraphPos          => _graphPos;

            /// <summary><paramref name="fromLevel"/>'dan bir üste çıkmanın bedeli. Seviye
            /// büyüdükçe pahalanır (taban × mevcut seviye).</summary>
            public List<EssenceAmount> UpgradeCost(int fromLevel)
            {
                var list = new List<EssenceAmount>();
                if (_levelCost == null) return list;
                int mult = Mathf.Max(1, fromLevel);
                foreach (var c in _levelCost)
                    list.Add(new EssenceAmount(c.type, c.amount * mult));
                return list;
            }
        }

        [Tooltip("Ağacın düğümleri. Sıra önemsiz — bağlantı 'Requires' alanından kurulur.")]
        [SerializeField] private Node[] _nodes;

        public IReadOnlyList<Node> Nodes => _nodes ?? System.Array.Empty<Node>();

        public Node Find(string id)
        {
            if (_nodes == null || string.IsNullOrEmpty(id)) return null;
            foreach (var n in _nodes) if (n != null && n.Id == id) return n;
            return null;
        }

        /// <summary>Bu id ağacın kataloğunda var mı (büyü kataloğu / karo kataloğu)?</summary>
        public abstract bool IsKnownId(string id);

        /// <summary>Uyarı metinlerinde kullanılan ağaç adı.</summary>
        protected abstract string TreeLabel { get; }

        /// <summary>Kurulum/asset düzenlemesinde sessiz hataları yakalar: katalogda olmayan id,
        /// var olmayan ön koşul, hiç açık başlangıç düğümü olmaması.</summary>
        protected virtual void OnValidate()
        {
            if (_nodes == null) return;

            bool anyStart = false;
            foreach (var n in _nodes)
            {
                if (n == null) continue;
                if (n.UnlockedAtStart) anyStart = true;

                if (!IsKnownId(n.Id))
                    Debug.LogWarning($"[{TreeLabel}] '{n.Id}' kataloğda YOK — bu düğüm hiçbir zaman açılamaz.", this);

                if (!string.IsNullOrEmpty(n.Requires) && Find(n.Requires) == null)
                    Debug.LogWarning($"[{TreeLabel}] '{n.Id}' düğümünün ön koşulu " +
                                     $"'{n.Requires}' ağaçta yok — düğüm kilitli kalır.", this);
            }

            if (_nodes.Length > 0 && !anyStart)
                Debug.LogWarning($"[{TreeLabel}] Hiçbir düğüm 'açık başlar' değil — davul her " +
                                 "vuruşta kart sunmak zorunda, havuz boş kalır.", this);
        }
    }
}
