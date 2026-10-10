using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// BİR BÖLGENİN TARİFİ (2026-10-10) — hikâye coğrafyasının oyundaki karşılığı (Yırtık Koru,
    /// Halka Köyü, Fısıltı Bataklığı…). Minecraft biyomu gibi: kendi karo tablosu, kendi göl/orman/
    /// dağ türü, kendi landmark'ları ve kendi OYNANIŞ kuralı vardır; haritadaki YERİ her koşuda değişir.
    ///
    /// Üretici bunun saf kopyasını (<see cref="RegionDef"/>) kullanır — Unity'siz seed taraması
    /// aynı kodu derlesin diye. Varsayılanlar: <see cref="KokAhdiRegions"/>.
    /// Karo listesinin insan okunur hâli: MODELLER/Karolar/KARO_LISTESI.md.
    ///
    /// SAF VERİ: runtime durumu (oyuncu hangi bölgede, çürüme ne kadar) burada TUTULMAZ.
    /// </summary>
    [CreateAssetMenu(fileName = "Bolge", menuName = "TacticalRPG/Region")]
    public class RegionSO : ScriptableObject
    {
        [System.Serializable]
        public class TileChance
        {
            [Tooltip("TileCatalog id'si (ör. cayir, solgun_cayir).")]
            public string id;
            [Min(0f)] public float weight = 1f;
        }

        [Header("Kimlik")]
        [SerializeField] private string _id = "bolge";
        [SerializeField] private string _displayName = "Bölge";
        [TextArea(2, 4)]
        [Tooltip("Hikâyedeki atmosfer — giriş yazısında ve tasarım notunda kullanılır.")]
        [SerializeField] private string _flavor;
        [Tooltip("Giriş yazısının ve minimap bölge renginin tonu.")]
        [SerializeField] private Color _color = new(0.85f, 0.70f, 0.35f);

        [Header("İskelet")]
        [SerializeField] private RegionRole _role = RegionRole.Filler;
        [Tooltip("Kaç çekirdekten büyür (ara doku 2 parça olabilir).")]
        [SerializeField, Min(1)] private int _seeds = 1;
        [Tooltip("Göreli alan payı (bölümdeki tüm bölgelerin toplamına oranlanır).")]
        [SerializeField, Min(0.05f)] private float _area = 1f;
        [Tooltip("Yükseklik bükmesi: + tepelik/dağlık, − alçak/göllü.")]
        [SerializeField, Range(-0.3f, 0.3f)] private float _elevationBias;
        [Tooltip("Nem bükmesi: + göl/bataklık, − kuru.")]
        [SerializeField, Range(-0.3f, 0.3f)] private float _moistureBias;

        [Header("Karolar")]
        [Tooltip("Yürünür zemin tablosu. Düzlük (öz yok) payı ~%45 altına inmesin — düğümler yalnız düzlüğe konur.")]
        [SerializeField] private List<TileChance> _ground = new();
        [SerializeField] private string[] _forest = new string[0];
        [SerializeField] private string[] _lake = new string[0];
        [Tooltip("[0] silsilenin içi, kalanı etek. Boş = iklim kuralı (karlı zirve çıkabilir).")]
        [SerializeField] private string[] _mountain = new string[0];
        [SerializeField] private string[] _landmarks = new string[0];
        [Tooltip("Bölge çekirdeğine BİR KEZ konan karolar (Bayterek, Halka Meclisi…).")]
        [SerializeField] private string[] _unique = new string[0];

        [Header("Kara Aşı çürümesi")]
        [SerializeField] private bool _corruptionSource;
        [SerializeField] private string _sourceTile = TileCatalog.KaraAsi;
        [Tooltip("Kalıcı kaynak: zorunlu görevle ARINMAZ (Zar yırtığı, Bayterek'in kalbi).")]
        [SerializeField] private bool _sourcePermanent;

        [Header("Oynanış kuralı")]
        [Tooltip("Bu bölgedeyken görüş menziline eklenir (bataklık sisi: −1).")]
        [SerializeField, Range(-3, 3)] private int _visionDelta;

        public string     Id          => _id;
        public string     DisplayName => _displayName;
        public string     Flavor      => _flavor;
        public Color      Color       => _color;
        public RegionRole Role        => _role;
        public int        VisionDelta => _visionDelta;
        public bool       HasCorruptionSource => _corruptionSource;
        public bool       SourcePermanent     => _sourcePermanent;

        /// <summary>Özü toplanan karonun döneceği karo: bölge tablosundaki İLK düzlük (öz taşımayan)
        /// karo — bataklıkta bataklık, Kalp'te kök ağı. Yoksa null (genel "ova"ya düşülür).</summary>
        public string DepletedTileId
        {
            get
            {
                if (_ground == null) return null;
                foreach (var t in _ground)
                {
                    TileCatalog.Entry e = t != null ? TileCatalog.Get(t.id) : null;
                    if (e != null && e.Family == TileFamily.Plain) return e.Id;
                }
                return null;
            }
        }

        /// <summary>Üreticinin anladığı saf tarif. Bilinmeyen karo id'leri ATLANIR (uyarıyla) —
        /// yazım hatası yüzünden haritaya katalogda olmayan karo düşmesin.</summary>
        public RegionDef ToDef()
        {
            var ground = new List<TileWeight>();
            foreach (var t in _ground)
                if (t != null && Known(t.id)) ground.Add(new TileWeight(t.id, t.weight));

            return new RegionDef
            {
                Id = _id, Name = _displayName, Role = _role,
                Seeds = Mathf.Max(1, _seeds), Area = Mathf.Max(0.05f, _area),
                ElevBias = _elevationBias, MoistBias = _moistureBias,
                Ground = ground.ToArray(),
                Forest = Filter(_forest), Lake = Filter(_lake), Mountain = Filter(_mountain),
                Landmarks = Filter(_landmarks), Unique = Filter(_unique),
                CorruptionSource = _corruptionSource && Known(_sourceTile),
                SourceTile = _sourceTile, SourcePermanent = _sourcePermanent
            };
        }

        private string[] Filter(string[] ids)
        {
            if (ids == null) return new string[0];
            var list = new List<string>(ids.Length);
            foreach (var id in ids) if (Known(id)) list.Add(id);
            return list.ToArray();
        }

        private bool Known(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (TileCatalog.Get(id) != null) return true;
            Debug.LogWarning($"[Bolge] {name}: '{id}' katalogda yok — atlandi.");
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Kurulum aracı: saf tariften ilk değerleri yazar (asset YENİ üretilirken bir kez).</summary>
        public void EditorInitFrom(RegionDef d, string flavor, Color color, int visionDelta)
        {
            _id = d.Id; _displayName = d.Name; _role = d.Role; _seeds = d.Seeds; _area = d.Area;
            _elevationBias = d.ElevBias; _moistureBias = d.MoistBias;
            _ground = new List<TileChance>();
            foreach (var g in d.Ground) _ground.Add(new TileChance { id = g.Id, weight = g.Weight });
            _forest = d.Forest; _lake = d.Lake; _mountain = d.Mountain;
            _landmarks = d.Landmarks; _unique = d.Unique;
            _corruptionSource = d.CorruptionSource; _sourceTile = d.SourceTile ?? TileCatalog.KaraAsi;
            _sourcePermanent = d.SourcePermanent;
            _flavor = flavor; _color = color; _visionDelta = visionDelta;
        }
#endif
    }
}
