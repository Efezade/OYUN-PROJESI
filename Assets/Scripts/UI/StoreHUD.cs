using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Geçici IMGUI mağaza paneli (OverworldCombatHUD deseni): oyuncu bir MAĞAZA karosuna yaklaşınca
    /// "Dükkânı Aç" istemi; açıkken öz bakiyesi + katalog (kalıcı item / geçici pot) + satın-al butonları.
    /// Alım: <see cref="EssenceWallet.TrySpend"/> başarılıysa <see cref="PlayerBuffs.ApplyPurchase"/>.
    /// Yalnız Overworld'de çizilir; oyuncu uzaklaşınca kapanır. Cila aşamasında uGUI'ye taşınabilir.
    /// </summary>
    public class StoreHUD : MonoBehaviour
    {
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private StoreManager     _store;
        [SerializeField] private PlayerController  _player;
        [SerializeField] private EssenceWallet     _wallet;
        [SerializeField] private PlayerBuffs        _buffs;
        [Tooltip("Opsiyonel — öz adlarını göstermek için (yoksa enum adı).")]
        [SerializeField] private EssenceConfigSO    _config;

        private bool   _open;
        private string _flash;   // son işlem geri bildirimi
        private float  _flashUntil;

        private void OnGUI()
        {
            if (_stateManager == null || _store == null || _player == null) return;
            if (_stateManager.State != GameState.Overworld) { _open = false; return; }
            if (MenuState.IsAnyOpen) return; // tam-ekran menü (KİTAP/ÇANTA/…) açıkken dükkânı çizme

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
            var rect = new Rect((HudScale.Width - w) * 0.5f, 100f, w, h);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Mağaza yakında");
            if (GUILayout.Button("Dükkani Ac", GUILayout.Height(34))) _open = true;
            GUILayout.EndArea();
        }

        private void DrawShop()
        {
            const float w = 460f, h = 560f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, (HudScale.Height - h) * 0.5f, w, h);
            ImguiBlocker.Register(rect);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("MAGAZA — oz harcayarak al");
            DrawBalance();
            GUILayout.Space(6);

            var catalog = _store.Catalog;
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    ShopItemSO item = catalog[i];
                    if (item == null) continue;
                    DrawItemRow(item);
                }
            }

            GUILayout.FlexibleSpace();
            if (_flash != null && Time.unscaledTime < _flashUntil)
                GUILayout.Label(_flash);
            if (GUILayout.Button("Kapat", GUILayout.Height(30))) _open = false;

            GUILayout.EndArea();
        }

        private void DrawBalance()
        {
            if (_wallet == null) return;
            string a = Name(EssenceType.Ates), s = Name(EssenceType.Su), t = Name(EssenceType.Toprak);
            GUILayout.Label($"Ozler:  {_wallet.Get(EssenceType.Ates)} {a}   " +
                            $"{_wallet.Get(EssenceType.Su)} {s}   " +
                            $"{_wallet.Get(EssenceType.Toprak)} {t}");
        }

        private void DrawItemRow(ShopItemSO item)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            string tag = item.IsPermanent ? "[KALICI]" : (item.IsTimed ? $"[{item.DurationMoves} ADIM]" : "[ANLIK]");
            GUILayout.Label($"{item.DisplayName}  {tag}");
            if (!string.IsNullOrEmpty(item.Description))
                GUILayout.Label(item.Description);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Fiyat: {item.CostText(_config)}");
            GUILayout.FlexibleSpace();

            bool afford = _wallet == null || _wallet.CanAfford(item.Cost);
            GUI.enabled = afford;
            if (GUILayout.Button(afford ? "Satin Al" : "Yetersiz oz", GUILayout.Width(130), GUILayout.Height(28)))
                Buy(item);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void Buy(ShopItemSO item)
        {
            if (_wallet != null && !_wallet.TrySpend(item.Cost)) { Flash("Yetersiz oz!"); return; }
            _buffs?.ApplyPurchase(item);
            Flash($"Alindi: {item.DisplayName}");
        }

        private void Flash(string msg)
        {
            _flash      = msg;
            _flashUntil = Time.unscaledTime + 2.5f;
        }

        private string Name(EssenceType t) => _config != null ? _config.NameOf(t) : t.ToString();
    }
}
