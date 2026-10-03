using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Geçici IMGUI MARKET paneli: oyuncu bir mağaza karosuna / açık market düğümüne yaklaşınca
    /// "Dükkânı Aç" istemi; açıkken öz bakiyesi + GÜNÜN STOĞU (eşyalar · potlar) + REROLL.
    ///
    /// 2026-10-03 (Efe'nin isteği): sabit katalog yerine <see cref="StoreManager.Stock"/> — her gün
    /// rastgele dizilen raf. Alınan ürün ENVANTERE gider (eşya: ÇANTA → EŞYALAR'dan karaktere
    /// sürüklenir · pot: ÇANTA → POTLAR'dan içilir). REROLL düğmesi belirli öz karşılığı rafı
    /// baştan dizer. Nadirlik rengi satırın kenarında durur. Cila aşamasında uGUI'ye taşınabilir.
    /// </summary>
    public class StoreHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private StoreManager     _store;
        [SerializeField] private PlayerController  _player;
        [SerializeField] private EssenceWallet     _wallet;
        [SerializeField] private PlayerBuffs        _buffs;
        [Tooltip("Alınan eşya ve potların gittiği envanter.")]
        [SerializeField] private Inventory          _inventory;
        [Tooltip("Opsiyonel — öz adlarını göstermek için (yoksa enum adı).")]
        [SerializeField] private EssenceConfigSO    _config;

        [Tooltip("Bakiyede gösterilecek öz türleri. Bölüm 1 = Taş + Doğa (GAME_DESIGN §3).")]
        [SerializeField] private EssenceType[] _shownTypes = { EssenceType.Tas, EssenceType.Doga };

        [Header("Nadirlik renkleri")]
        [SerializeField] private Color _common   = new(0.80f, 0.80f, 0.78f);
        [SerializeField] private Color _rare     = new(0.40f, 0.65f, 1.00f);
        [SerializeField] private Color _epic     = new(0.80f, 0.45f, 1.00f);

        private bool    _open;
        private string  _flash;   // son işlem geri bildirimi
        private float   _flashUntil;
        private Vector2 _scroll;  // stok kaydırma konumu

        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_stateManager == null || _store == null || _player == null) return;
            if (_stateManager.State != GameState.Overworld) { _open = false; return; }

            bool near = _store.IsPlayerNearStore(_player.CurrentCoord);
            if (!near) { _open = false; return; }

            using (HudScale.Scaled())
            {
                if (_open) DrawShop();
                else       DrawOpenPrompt();
            }
        }

        private void DrawOpenPrompt()
        {
            const float w = 300f, h = 76f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudLayout.ThirdRowY, w, h);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Mağaza yakında");
            if (GUILayout.Button("Dükkânı Aç", GUILayout.Height(34))) _open = true;
            GUILayout.EndArea();
        }

        private void DrawShop()
        {
            const float w = 560f;
            // SABİT YÜKSEKLİK YAZMA: sanal ekran 1080 değil (~720, HudScale.UiScale'e bağlı).
            float h = Mathf.Min(660f, HudScale.Height - 40f);

            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("MARKET — günün rafı (her gün yenilenir)");
            DrawBalance();
            GUILayout.Space(4);

            // Raf kaydırılır; REROLL ve Kapat hep görünür kalır.
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            var stock = _store.Stock;
            bool itemsHeader = false, potsHeader = false;
            for (int i = 0; i < stock.Count; i++)
            {
                var e = stock[i];
                if (e == null) continue;
                if (e.IsItem && !itemsHeader)  { GUILayout.Label("EŞYALAR — karaktere takılır (ÇANTA'dan sürükle)"); itemsHeader = true; }
                if (!e.IsItem && !potsHeader)  { GUILayout.Space(6); GUILayout.Label("POTLAR — çantada birikir, istediğin an iç"); potsHeader = true; }
                DrawStockRow(e, i);
            }
            if (stock.Count == 0) GUILayout.Label("Raf boş.");

            GUILayout.EndScrollView();

            if (_flash != null && Time.unscaledTime < _flashUntil)
                GUILayout.Label(_flash);

            // REROLL — rafı baştan dizdirir.
            bool canReroll = _wallet == null || _wallet.CanAfford(_store.RerollCost);
            GUI.enabled = canReroll;
            if (GUILayout.Button($"YENİDEN DİZ (reroll) — {CostText(_store.RerollCost)}", GUILayout.Height(32)))
            {
                if (_store.TryReroll(_wallet)) Flash("Raf yeniden dizildi!");
                else                           Flash("Yetersiz öz!");
            }
            GUI.enabled = true;

            if (GUILayout.Button("Kapat", GUILayout.Height(28))) _open = false;

            GUILayout.EndArea();
        }

        private void DrawStockRow(StoreManager.StockEntry e, int index)
        {
            Color prev = GUI.color;
            GUI.color = RarityColor(e.Rarity);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = prev;

            string kind = e.IsItem ? $"EŞYA · {EquipSlots.Label(e.Item.Slot)}" : $"POT · {e.Potion.DurationText}";
            GUILayout.Label($"{e.Name}   [{RarityName(e.Rarity)} · {kind}]");

            string effect = e.IsItem ? e.Item.EffectText() : e.Potion.Description;
            if (!string.IsNullOrEmpty(effect)) GUILayout.Label(effect);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Fiyat: {CostText(e.Price)}");
            GUILayout.FlexibleSpace();

            if (e.Sold)
            {
                GUI.enabled = false;
                GUILayout.Button("SATILDI", GUILayout.Width(140), GUILayout.Height(28));
                GUI.enabled = true;
            }
            else
            {
                bool afford = _wallet == null || _wallet.CanAfford(e.Price);
                GUI.enabled = afford && _inventory != null;
                if (GUILayout.Button(afford ? "Satın Al" : "Yetersiz öz", GUILayout.Width(140), GUILayout.Height(28)))
                {
                    if (_store.TryBuy(index, _wallet, _inventory)) Flash($"Çantaya eklendi: {e.Name}");
                    else                                           Flash("Alınamadı.");
                }
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>Öz bakiyesi. Türler Inspector'dan gelir (bölüm 1 = Taş + Doğa).</summary>
        private void DrawBalance()
        {
            if (_wallet == null) return;
            EssenceType[] types = (_shownTypes != null && _shownTypes.Length > 0)
                ? _shownTypes : new[] { EssenceType.Tas, EssenceType.Doga };

            var sb = new System.Text.StringBuilder("Özler: ");
            foreach (EssenceType t in types) sb.Append($"  {_wallet.Get(t)} {Name(t)}");
            GUILayout.Label(sb.ToString());
        }

        private string CostText(System.Collections.Generic.IReadOnlyList<EssenceAmount> cost)
        {
            if (cost == null || cost.Count == 0) return "bedava";
            var sb = new System.Text.StringBuilder();
            foreach (var c in cost)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(c.amount).Append(' ').Append(Name(c.type));
            }
            return sb.ToString();
        }

        private Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Nadir    => _rare,
            ItemRarity.Destansi => _epic,
            _                   => _common
        };

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Nadir    => "Nadir",
            ItemRarity.Destansi => "Destansı",
            _                   => "Sıradan"
        };

        private void Flash(string msg)
        {
            _flash      = msg;
            _flashUntil = Time.unscaledTime + 2.5f;
        }

        private string Name(EssenceType t) => _config != null ? _config.NameOf(t) : t.ToString();
    }
}
