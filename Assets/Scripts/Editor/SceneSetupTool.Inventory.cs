using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// EŞYA + POT + MARKET kurulumu (Efe'nin isteği 2026-10-03). Unity KAPALIYKEN:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupInventoryBatch -logFile log.txt
    /// </code>
    /// Üretir (VARSA DOKUNMAZ, CLAUDE.md §9.1): 24 eşya (Assets/Data/Items) + 14 pot
    /// (Assets/Data/Potions), simgeleri <see cref="InkArtFactory"/>'den. Kurar: <see cref="Inventory"/>,
    /// market havuzları, pot/eşya etkilerinin bağları, ÇANTA görünümlerinin bağları.
    /// ÇANTA sayfalarının kendisi KİTAP/ÇANTA kabuğuyla (SetupUIShell → PopulateBagScreen) çizilir.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string ItemFolder   = "Assets/Data/Items";
        private const string PotionFolder = "Assets/Data/Potions";

        [MenuItem("TacticalRPG/Market - Esya + Pot + Reroll Kur", false, 32)]
        public static void SetupInventoryMenu()
        {
            int n = ApplyInventory();
            EditorUtility.DisplayDialog("Esya + Pot + Market",
                n > 0 ? "Kuruldu: 24 esya, 14 pot, envanter, market stogu + reroll.\n\n" +
                        "CANTA sayfalari icin 'UI - Kam Yetenek Agacini Kur' da kostur (kabugu yeniden cizer).\n" +
                        "SAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede StoreManager yok. Once TAM KURULUM.",
                "Tamam");
        }

        public static void SetupInventoryBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { EditorApplication.Exit(1); return; }

            if (ApplyInventory() == 0) { EditorApplication.Exit(1); return; }
            if (ApplySkillTree() == 0) { EditorApplication.Exit(1); return; }   // kabuk (ÇANTA dahil) yeniden çizilir
            ApplyInventory();                                                    // yeni çizilen ÇANTA görünümlerini bağla

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TAM KURULUM zincirindeki adım (SetupStore'dan SONRA: PlayerBuffs orada kurulur).</summary>
        public static void SetupInventory() => ApplyInventory();

        private static int ApplyInventory()
        {
            List<ItemSO>   items   = EnsureItems();
            List<PotionSO> potions = EnsurePotions();

            var store = FindComponentAnywhere<StoreManager>();
            if (store == null) { Debug.LogError("[Market] Sahnede StoreManager yok — once TAM KURULUM."); return 0; }

            Inventory inv = EnsureInventory();
            var ap     = FindComponentAnywhere<ActionPointManager>();
            var buffs  = FindComponentAnywhere<PlayerBuffs>();
            var turns  = FindComponentAnywhere<TurnManager>();
            var fog    = FindComponentAnywhere<TacticalRPG.Grid.FogOfWarManager>();

            var sso = new SerializedObject(store);
            WriteList(sso.FindProperty("_itemPool"),   items);
            WriteList(sso.FindProperty("_potionPool"), potions);
            sso.FindProperty("_ap").objectReferenceValue = ap;
            sso.ApplyModifiedProperties();

            if (buffs != null)
            {
                var so = new SerializedObject(buffs);
                so.FindProperty("_fog").objectReferenceValue   = fog;
                so.FindProperty("_turns").objectReferenceValue = turns;
                so.ApplyModifiedProperties();
            }

            var hud = FindComponentAnywhere<TacticalRPG.UI.StoreHUD>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                so.FindProperty("_inventory").objectReferenceValue = inv;
                so.ApplyModifiedProperties();
            }

            var field = FindComponentAnywhere<EssenceFieldManager>();
            if (field != null)
            {
                var so = new SerializedObject(field);
                so.FindProperty("_buffs").objectReferenceValue = buffs;
                so.ApplyModifiedProperties();
            }

            var deploy = FindComponentAnywhere<DeploymentManager>();
            if (deploy != null)
            {
                var so = new SerializedObject(deploy);
                so.FindProperty("_inventory").objectReferenceValue = inv;
                so.FindProperty("_buffs").objectReferenceValue     = buffs;
                so.ApplyModifiedProperties();
            }

            // ÇANTA görünümleri (kabuk sahnede kapalı başlar → kapalılar da aranır).
            // EŞYALAR + KAM sayfaları: İKİ donanım görünümü var, hepsi bağlanır.
            var bagViews = Object.FindObjectsByType<TacticalRPG.UI.BagInventoryView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bagView in bagViews)
            {
                var so = new SerializedObject(bagView);
                so.FindProperty("_inventory").objectReferenceValue = inv;
                so.FindProperty("_party").objectReferenceValue     = FindComponentAnywhere<PartyManager>();
                so.ApplyModifiedProperties();
            }
            var potView = Object.FindFirstObjectByType<TacticalRPG.UI.BagPotionView>(FindObjectsInactive.Include);
            if (potView != null)
            {
                var so = new SerializedObject(potView);
                so.FindProperty("_inventory").objectReferenceValue = inv;
                so.FindProperty("_buffs").objectReferenceValue     = buffs;
                so.ApplyModifiedProperties();
            }

            Debug.Log($"[Market] DOGRULAMA — esya:{items.Count} pot:{potions.Count} envanter:{(inv != null)} " +
                      $"etki:{(buffs != null)} hud:{(hud != null)} oz-alani:{(field != null)} " +
                      $"yerlestirme:{(deploy != null)} canta-donanim:{bagViews.Length} canta-pot:{(potView != null)}");
            return 1;
        }

        private static Inventory EnsureInventory()
        {
            EssenceWallet wallet = FindComponentAnywhere<EssenceWallet>();
            GameObject host = wallet != null ? wallet.gameObject : GameObject.Find(SceneRootName);
            if (host == null) return null;
            var inv = host.GetComponent<Inventory>();
            if (inv == null) inv = host.AddComponent<Inventory>();
            return inv;
        }

        private static void WriteList<T>(SerializedProperty list, List<T> values) where T : Object
        {
            if (list == null) return;
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ── Eşyalar ──────────────────────────────────────────────────────────

        private struct ItemSpec
        {
            public string Id, Name, Desc; public ItemRarity Rarity; public InkIcon Icon; public EquipSlot Slot;
            public EvolutionTrait T1; public int A1; public EvolutionTrait T2; public int A2;
            public ItemSpec(string id, string name, string desc, ItemRarity r, InkIcon icon, EquipSlot slot,
                            EvolutionTrait t1, int a1, EvolutionTrait t2 = EvolutionTrait.None, int a2 = 0)
            { Id = id; Name = name; Desc = desc; Rarity = r; Icon = icon; Slot = slot; T1 = t1; A1 = a1; T2 = t2; A2 = a2; }
        }

        /// <summary>24 EŞYA — Türk mitolojisi tonunda adlar. Etkiler evrim kümesinden. SAYILAR ÖRNEK.</summary>
        private static readonly ItemSpec[] ItemSpecs =
        {
            // SIRADAN
            new("pasli_kilic",    "Paslı Kılıç",      "Eski bir akıncının kılıcı.",          ItemRarity.Siradan,  InkIcon.Sword, EquipSlot.SagKol,  EvolutionTrait.BonusAttack, 1),
            new("deri_yelek",     "Deri Yelek",       "Kalın, işlenmiş at derisi.",          ItemRarity.Siradan,  InkIcon.Shield, EquipSlot.Govde, EvolutionTrait.BonusDefense, 1),
            new("hafif_cizme",    "Hafif Çizme",      "Bozkırda yorulmayan ayaklar.",        ItemRarity.Siradan,  InkIcon.Wind, EquipSlot.Ayak,   EvolutionTrait.BonusMove, 1),
            new("kurt_disi",      "Kurt Dişi Kolye",  "Börü'nün çevikliği.",                 ItemRarity.Siradan,  InkIcon.Star, EquipSlot.Boyun,   EvolutionTrait.BonusSpeed, 1),
            new("ahsap_kalkan",   "Ahşap Kalkan",     "Kayın ağacından yuvarlak kalkan.",    ItemRarity.Siradan,  InkIcon.Shield, EquipSlot.SolKol, EvolutionTrait.StartShield, 3),
            new("sifaci_muska",   "Şifacı Muskası",   "Kamın kutsadığı küçük muska.",        ItemRarity.Siradan,  InkIcon.Drop, EquipSlot.Boyun,   EvolutionTrait.Regen, 1),
            new("kemik_bilezik",  "Kemik Bilezik",    "Dokunanı dalayan kemik dikenler.",    ItemRarity.Siradan,  InkIcon.Spiral, EquipSlot.SolKol, EvolutionTrait.Thorns, 1),
            new("tunc_mizrak",    "Tunç Mızrak Ucu",  "Kısa ama keskin.",                    ItemRarity.Siradan,  InkIcon.Sword, EquipSlot.SagKol,  EvolutionTrait.BonusAttack, 1, EvolutionTrait.BonusSpeed, 1),
            // NADİR
            new("celik_kilic",    "Çelik Kılıç",      "Demirci Kava'nın işi.",               ItemRarity.Nadir,    InkIcon.Sword, EquipSlot.SagKol,  EvolutionTrait.BonusAttack, 2),
            new("zincir_zirh",    "Zincir Zırh",      "Bin halkalı gömlek.",                 ItemRarity.Nadir,    InkIcon.Shield, EquipSlot.Govde, EvolutionTrait.BonusDefense, 2),
            new("kartal_gozu",    "Kartal Gözü",      "Göğün gözüyle nişan al.",             ItemRarity.Nadir,    InkIcon.Leaf, EquipSlot.Kafa,   EvolutionTrait.BonusRange, 1),
            new("ruzgar_pelerin", "Rüzgâr Pelerini",  "Yel İye'nin armağanı.",               ItemRarity.Nadir,    InkIcon.Wind, EquipSlot.Govde,   EvolutionTrait.BonusMove, 1, EvolutionTrait.BonusSpeed, 1),
            new("vampir_disi",    "Albastı Dişi",     "Kan içtikçe güçlenir.",               ItemRarity.Nadir,    InkIcon.Drop, EquipSlot.Boyun,   EvolutionTrait.Lifesteal, 25),
            new("dikenli_kalkan", "Dikenli Kalkan",   "Vuranın eli kanar.",                  ItemRarity.Nadir,    InkIcon.Shield, EquipSlot.SolKol, EvolutionTrait.Thorns, 2, EvolutionTrait.BonusDefense, 1),
            new("yay_teli",       "Kiriş Teli",       "İkinci ok kendiliğinden fırlar.",     ItemRarity.Nadir,    InkIcon.Leaf, EquipSlot.SagKol,   EvolutionTrait.DoubleStrike, 40),
            new("ayi_postu",      "Ayı Postu",        "Kalın post, kalın can.",              ItemRarity.Nadir,    InkIcon.Bag, EquipSlot.Govde,    EvolutionTrait.StartShield, 5, EvolutionTrait.BonusDefense, 1),
            new("kam_tutsu",      "Kam Tütsüsü",      "Ruhlar daha çabuk cevap verir.",      ItemRarity.Nadir,    InkIcon.Book, EquipSlot.Kafa,   EvolutionTrait.CooldownReduction, 1),
            new("balta_agzi",     "Balta Ağzı",       "Darbe yana da sıçrar.",               ItemRarity.Nadir,    InkIcon.Sword, EquipSlot.SagKol,  EvolutionTrait.Cleave, 30),
            // DESTANSI
            new("tengri_kilic",   "Tengri'nin Kılıcı","Gökten inen demir.",                  ItemRarity.Destansi, InkIcon.Flame, EquipSlot.SagKol,  EvolutionTrait.BonusAttack, 3, EvolutionTrait.Cleave, 25),
            new("umay_muska",     "Umay Muskası",     "Ana tanrıçanın koruması.",            ItemRarity.Destansi, InkIcon.Drop, EquipSlot.Boyun,   EvolutionTrait.Regen, 3),
            new("bori_baslik",    "Börü Başlığı",     "Kurt atanın öfkesi.",                 ItemRarity.Destansi, InkIcon.Star, EquipSlot.Kafa,   EvolutionTrait.DoubleStrike, 60),
            new("erlik_pence",    "Erlik'in Pençesi", "Yeraltının açlığı.",                  ItemRarity.Destansi, InkIcon.Spiral, EquipSlot.SolKol, EvolutionTrait.Lifesteal, 40, EvolutionTrait.BonusAttack, 1),
            new("gok_demir",      "Gök Demir Zırh",   "Kırılmaz, dokunanı yaralar.",         ItemRarity.Destansi, InkIcon.Shield, EquipSlot.Govde, EvolutionTrait.BonusDefense, 3, EvolutionTrait.Thorns, 2),
            new("yel_ati_nali",   "Yel Atı Nalı",     "Rüzgârla yarışan atın nalı.",         ItemRarity.Destansi, InkIcon.Wind, EquipSlot.Ayak,   EvolutionTrait.BonusMove, 2, EvolutionTrait.BonusSpeed, 2),
        };

        private static List<ItemSO> EnsureItems()
        {
            EnsureFolder(ItemFolder);
            var list = new List<ItemSO>();
            foreach (ItemSpec s in ItemSpecs)
            {
                string path = $"{ItemFolder}/{s.Id}.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemSO>();
                    AssetDatabase.CreateAsset(item, path);
                    var so = new SerializedObject(item);
                    so.FindProperty("_id").stringValue          = s.Id;
                    so.FindProperty("_displayName").stringValue = s.Name;
                    so.FindProperty("_description").stringValue = s.Desc;
                    so.FindProperty("_rarity").enumValueIndex   = (int)s.Rarity;
                    so.FindProperty("_slot").enumValueIndex     = (int)s.Slot;
                    so.FindProperty("_trait").enumValueIndex    = (int)s.T1;
                    so.FindProperty("_amount").intValue         = s.A1;
                    so.FindProperty("_trait2").enumValueIndex   = (int)s.T2;
                    so.FindProperty("_amount2").intValue        = s.A2;
                    so.FindProperty("_icon").objectReferenceValue = InkArtFactory.Icon(s.Icon, 64);
                    var (tas, doga) = PriceFor(s.Rarity, s.Id, potion: false);
                    WriteCost(so.FindProperty("_price"), tas, doga);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
                else if (item.Slot == EquipSlot.Yok)
                {
                    // Bölge alanı sonradan eklendi (2026-10-03): eski asset'lere YALNIZ boşsa yazılır.
                    var so = new SerializedObject(item);
                    so.FindProperty("_slot").enumValueIndex = (int)s.Slot;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
                list.Add(item);
            }
            return list;
        }

        // ── Potlar ───────────────────────────────────────────────────────────

        private struct PotionSpec
        {
            public string Id, Name, Desc; public ItemRarity Rarity; public Color Color;
            public PotionEffectKind Kind; public int Mag, Dur;
            public EvolutionTrait T1, T2; public int A2;
            public PotionSpec(string id, string name, string desc, ItemRarity r, Color c, PotionEffectKind k,
                              int mag, int dur, EvolutionTrait t1 = EvolutionTrait.None,
                              EvolutionTrait t2 = EvolutionTrait.None, int a2 = 0)
            { Id = id; Name = name; Desc = desc; Rarity = r; Color = c; Kind = k; Mag = mag; Dur = dur; T1 = t1; T2 = t2; A2 = a2; }
        }

        /// <summary>14 POT — yarısı ana harita (yürüme/görüş/zaman), yarısı savaş. SAYILAR ÖRNEK.</summary>
        private static readonly PotionSpec[] PotionSpecs =
        {
            // ANA HARİTA
            new("yel_iksiri",     "Yel İksiri",        "Sise doğru tek tıkla +2 karo yürü",    ItemRarity.Siradan,  new Color(0.55f, 0.85f, 0.95f), PotionEffectKind.MoveRange,   2, 12),
            new("hiz_serbeti",    "Hız Şerbeti",       "Yürüme 2 kat hızlı (AP aynı)",         ItemRarity.Siradan,  new Color(0.95f, 0.85f, 0.35f), PotionEffectKind.MoveSpeed, 100, 20),
            new("kartal_iksiri",  "Kartal Gözü İksiri","Görüş +2 karo",                        ItemRarity.Nadir,    new Color(0.85f, 0.65f, 0.30f), PotionEffectKind.Vision,      2, 15),
            new("toplayici_ozu",  "Toplayıcı Özü",     "Öz toplamak AP harcamaz",              ItemRarity.Siradan,  new Color(0.55f, 0.90f, 0.45f), PotionEffectKind.FreeCollect, 0, 15),
            new("zaman_kumu",     "Zaman Kumu",        "Anında +3 AP",                         ItemRarity.Nadir,    new Color(0.90f, 0.78f, 0.55f), PotionEffectKind.BonusAP,     3, 0),
            new("kahin_tutsu",    "Kâhin Tütsüsü",     "Çevrendeki 4 karoda sisi kalıcı aç",   ItemRarity.Nadir,    new Color(0.70f, 0.60f, 0.90f), PotionEffectKind.RevealArea,  4, 0),
            new("yol_tasi",       "Yol Taşı",          "+1 güçlü yol taşı (haritadan seyahat)",ItemRarity.Nadir,    new Color(0.65f, 0.65f, 0.70f), PotionEffectKind.TravelStones,1, 0),
            // SAVAŞ (sonraki savaş/lar, tüm birlik)
            new("ofke_iksiri",    "Öfke İksiri",       "Sonraki savaş: birliğe +2 saldırı",    ItemRarity.Siradan,  new Color(0.90f, 0.30f, 0.25f), PotionEffectKind.CombatTrait, 2, 1, EvolutionTrait.BonusAttack),
            new("tas_deri",       "Taş Deri İksiri",   "Sonraki savaş: birliğe +2 savunma",    ItemRarity.Siradan,  new Color(0.60f, 0.58f, 0.55f), PotionEffectKind.CombatTrait, 2, 1, EvolutionTrait.BonusDefense),
            new("kalkan_tilsimi", "Kalkan Tılsımı",    "Sonraki savaş: herkes 4 kalkanla iner",ItemRarity.Nadir,    new Color(0.80f, 0.85f, 0.95f), PotionEffectKind.CombatTrait, 4, 1, EvolutionTrait.StartShield),
            new("cevik_iksir",    "Çevik İksir",       "Sonraki savaş: +1 hareket, +2 hız",    ItemRarity.Nadir,    new Color(0.45f, 0.85f, 0.75f), PotionEffectKind.CombatTrait, 1, 1, EvolutionTrait.BonusMove, EvolutionTrait.BonusSpeed, 2),
            new("sifa_bugusu",    "Şifa Buğusu",       "Sonraki 2 savaş: tur başı 2 can",      ItemRarity.Nadir,    new Color(1.00f, 0.92f, 0.60f), PotionEffectKind.CombatTrait, 2, 2, EvolutionTrait.Regen),
            new("kan_iksiri",     "Kan İksiri",        "Sonraki 2 savaş: can çalma %25",       ItemRarity.Destansi, new Color(0.65f, 0.08f, 0.12f), PotionEffectKind.CombatTrait,25, 2, EvolutionTrait.Lifesteal),
            new("davul_serbeti",  "Savaş Davulu Şerbeti","Sonraki savaş: çift vuruş %35",      ItemRarity.Destansi, new Color(0.95f, 0.55f, 0.20f), PotionEffectKind.CombatTrait,35, 1, EvolutionTrait.DoubleStrike),
        };

        private static List<PotionSO> EnsurePotions()
        {
            EnsureFolder(PotionFolder);
            var list = new List<PotionSO>();
            Sprite drop = InkArtFactory.Icon(InkIcon.Drop, 64);
            foreach (PotionSpec s in PotionSpecs)
            {
                string path = $"{PotionFolder}/{s.Id}.asset";
                var pot = AssetDatabase.LoadAssetAtPath<PotionSO>(path);
                if (pot == null)
                {
                    pot = ScriptableObject.CreateInstance<PotionSO>();
                    AssetDatabase.CreateAsset(pot, path);
                    var so = new SerializedObject(pot);
                    so.FindProperty("_id").stringValue          = s.Id;
                    so.FindProperty("_displayName").stringValue = s.Name;
                    so.FindProperty("_description").stringValue = s.Desc;
                    so.FindProperty("_rarity").enumValueIndex   = (int)s.Rarity;
                    so.FindProperty("_color").colorValue        = s.Color;
                    so.FindProperty("_kind").enumValueIndex     = (int)s.Kind;
                    so.FindProperty("_magnitude").intValue      = s.Mag;
                    so.FindProperty("_duration").intValue       = s.Dur;
                    so.FindProperty("_trait").enumValueIndex    = (int)s.T1;
                    so.FindProperty("_trait2").enumValueIndex   = (int)s.T2;
                    so.FindProperty("_amount2").intValue        = s.A2;
                    so.FindProperty("_icon").objectReferenceValue = drop;
                    var (tas, doga) = PriceFor(s.Rarity, s.Id, potion: true);
                    WriteCost(so.FindProperty("_price"), tas, doga);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pot);
                }
                list.Add(pot);
            }
            return list;
        }

        /// <summary>Nadirliğe göre fiyat bandı + id'den türeyen küçük sapma (yer tutucu).</summary>
        private static (int tas, int doga) PriceFor(ItemRarity r, string id, bool potion)
        {
            var rnd = new System.Random(id.GetHashCode());
            int bt = potion ? (r == ItemRarity.Siradan ? 3 : r == ItemRarity.Nadir ? 5 : 8)
                            : (r == ItemRarity.Siradan ? 5 : r == ItemRarity.Nadir ? 9 : 15);
            int bd = potion ? (r == ItemRarity.Siradan ? 2 : r == ItemRarity.Nadir ? 4 : 6)
                            : (r == ItemRarity.Siradan ? 3 : r == ItemRarity.Nadir ? 6 : 10);
            return (bt + rnd.Next(0, 2), bd + rnd.Next(0, 2));
        }
    }
}
