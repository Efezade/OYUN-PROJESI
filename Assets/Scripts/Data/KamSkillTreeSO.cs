using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// KAM'IN YETENEK AĞACI (2026-09-04, Efe'nin isteği): büyüler ÖZ harcanarak açılır ve
    /// yükseltilir. Ağacın kendisi KİTAP ekranında çizilir, ilerleme
    /// <see cref="TacticalRPG.Core.KamSkillProgress"/>'te tutulur.
    ///
    /// AĞAÇ ↔ DAVUL DRAFTI (Efe'nin kararı 2026-09-04): **ağaç HAVUZU ve SEVİYEYİ belirler,
    /// draft yine rastgele seçer.** Yani açılan büyü "artık draftta çıkabilir" olur, yükseltilen
    /// büyü daha büyük yarıçapla/hasarla çıkar. Böylece davulun "şimdi mi patlatayım, tahtayı mı
    /// kurayım" gerilimi ölmez; ağaç sürprizi değil, HAVUZUN KALİTESİNİ değiştirir.
    ///
    /// Neden düğüm etkisi "yeni bir büyü" değil de SEVİYE ÇARPANI: her seviye için ayrı katalog
    /// girdisi yazmak 5 büyü × 3 seviye = 15 girdi ve 15 ayrı denge sayısı demekti. Seviye,
    /// katalog girdisinin ÜSTÜNE binen bir değiştirici — <see cref="KamSkillCatalog"/> tek
    /// doğruluk kaynağı olarak kalır.
    ///
    /// SAYILAR TASLAK: denge/ekonomi hesapları durdurulmuş durumda (CLAUDE.md §9). Buradaki
    /// maliyetler ve seviye kazanımları yer tutucudur, `Docs/GAME_DESIGN.md`'ye henüz girmedi.
    /// </summary>
    [CreateAssetMenu(fileName = "KamSkillTree", menuName = "TacticalRPG/Config/KamSkillTree")]
    public class KamSkillTreeSO : ScriptableObject
    {
        /// <summary>Ağaçtaki tek bir büyü düğümü.</summary>
        [System.Serializable]
        public class Node
        {
            [Tooltip("KamSkillCatalog'daki büyünün id'si (gok_atesi, umay_sifasi, ...).")]
            [SerializeField] private string _skillId;

            [Tooltip("Bu düğüm açılmadan önce AÇILMIŞ olması gereken büyünün id'si. " +
                     "Boş = kök (ön koşulsuz).")]
            [SerializeField] private string _requires;

            [Tooltip("Oyunun başında AÇIK gelir mi? EN AZ BİRİ açık olmalı: davul her vuruşta bir " +
                     "büyü kartı sunmak zorunda, havuz boşsa o söz sessizce bozulur.")]
            [SerializeField] private bool _unlockedAtStart;

            [Tooltip("Kaç seviyeye kadar yükseltilebilir (1 = yalnız açılır, yükseltilemez).")]
            [SerializeField, Min(1)] private int _maxLevel = 3;

            [Tooltip("Düğümü AÇMANIN öz bedeli.")]
            [SerializeField] private EssenceAmount[] _unlockCost;

            [Tooltip("Bir seviye YÜKSELTMENİN taban bedeli. Gerçek bedel bununla mevcut seviyenin " +
                     "çarpımıdır (2. seviye ×1, 3. seviye ×2 ...) — sonraki seviye hep daha pahalı.")]
            [SerializeField] private EssenceAmount[] _levelCost;

            [Tooltip("Her seviyede büyünün hasarına/şifasına eklenen miktar.")]
            [SerializeField] private int _magnitudePerLevel = 2;

            [Tooltip("Her seviyede etki YARIÇAPINA eklenen hex. 0 = alan büyümez (yalnız güç artar). " +
                     "Dikkat: yarıçap alanı KAREsel büyütür (1 → 7 hex, 2 → 19 hex).")]
            [SerializeField, Min(0)] private int _radiusPerLevel;

            [Tooltip("Her seviyede İTME mesafesine eklenen karo (Yel Ata gibi itme büyüleri için).")]
            [SerializeField, Min(0)] private int _pushPerLevel;

            [Tooltip("Her seviyede SERSEMLETME süresine eklenen tur (Taş Kesilme gibi kontrol " +
                     "büyüleri için). Küçük tut: 1 tur sersemletme zaten güçlü.")]
            [SerializeField, Min(0)] private int _stunPerLevel;

            [Tooltip("Düğümün KİTAP sayfasındaki yeri (piksel, sayfanın ortasına göre).")]
            [SerializeField] private Vector2 _graphPos;

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
            public Vector2 GraphPos          => _graphPos;

            /// <summary>Katalogdaki karşılığı (id yanlışsa null).</summary>
            public KamSkillCatalog.Entry Catalog => KamSkillCatalog.Get(_skillId);

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

        public Node Find(string skillId)
        {
            if (_nodes == null || string.IsNullOrEmpty(skillId)) return null;
            foreach (var n in _nodes) if (n != null && n.SkillId == skillId) return n;
            return null;
        }

        /// <summary>Kurulum/asset düzenlemesinde sessiz hataları yakalar: katalogda olmayan id,
        /// var olmayan ön koşul, hiç açık başlangıç düğümü olmaması.</summary>
        private void OnValidate()
        {
            if (_nodes == null) return;

            bool anyStart = false;
            foreach (var n in _nodes)
            {
                if (n == null) continue;
                if (n.UnlockedAtStart) anyStart = true;

                if (n.Catalog == null)
                    Debug.LogWarning($"[YetenekAgaci] '{n.SkillId}' KamSkillCatalog'da YOK — " +
                                     "bu düğüm hiçbir zaman açılamaz.", this);

                if (!string.IsNullOrEmpty(n.Requires) && Find(n.Requires) == null)
                    Debug.LogWarning($"[YetenekAgaci] '{n.SkillId}' düğümünün ön koşulu " +
                                     $"'{n.Requires}' ağaçta yok — düğüm kilitli kalır.", this);
            }

            if (_nodes.Length > 0 && !anyStart)
                Debug.LogWarning("[YetenekAgaci] Hiçbir düğüm 'açık başlar' değil — davul her " +
                                 "vuruşta bir büyü sunmak zorunda, havuz boş kalır.", this);
        }
    }
}
