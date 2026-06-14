# CLAUDE.md — Taktiksel RPG Oyunu Geliştirme Kuralları

Bu dosya, Claude Code'un bu Unity projesinde nasıl davranacağını tanımlar.
Proje: Hex-grid tabanlı, For The King / XCOM tarzı taktiksel RPG (Unity 6).

---

## 1. Dil ve Motor Standartları

- **Dil:** C# (.NET Standard 2.1, Unity 6 / 2022 LTS uyumlu)
- **Motor:** Unity 6 (minimum Unity 2022 LTS)
- **Hedef Platform:** PC (Windows öncelikli)
- Yeni API'ler tercih edilir: `Physics2D`, `UnityEngine.InputSystem`, `Addressables`
- `GameObject.Find()`, `FindObjectOfType()` kesinlikle `Awake()`/`Start()` dışında kullanılmaz
- `Update()` içinde pahalı işlem yok: no Find, no GetComponent, no LINQ, no string işlemi

---

## 2. Mimari Kurallar

### Modüler Yapı
- Her sistem bağımsız çalışabilir olmalıdır (bağımlılık tek yönlü akar)
- Bir MonoBehaviour tek bir sorumluluğa sahip olur (Single Responsibility Principle)
- Spagetti kod yasaktır: mantık katmanları birbirine doğrudan bağlanmaz

### ScriptableObject Kullanımı
- Oyun verisi (karakter istatistikleri, yetenek tanımları, ırk/sınıf configs) ScriptableObject olarak tanımlanır
- SO'lar `Assets/Data/` altında organize edilir
- Runtime'da SO verisi değiştirilmez; her zaman kopyalanarak kullanılır

### Event-Driven Mimari (Observer Pattern)
- Sistemler arası iletişim doğrudan metod çağrısı ile değil, event/delegate üzerinden sağlanır
- Merkezi bir `GameEventSO` (ScriptableObject tabanlı event kanalı) sistemi kullanılır
- Örnek akış: `TurnManager` → `OnTurnChanged` event → `UIManager` dinler, `EnemyAI` dinler

---

## 3. Inspector ve Görsel Kural — WHİTEBOXİNG

> **Görsel objeler ve UI kesinlikle koda gömülmeyecek. Tüm referanslar `[SerializeField]` etiketiyle Unity Inspector'a bırakılacak (Whiteboxing mantığı).**

```csharp
// YANLIŞ
private GameObject player = GameObject.Find("Player");

// DOĞRU
[SerializeField] private GameObject player;
```

- `public` alan kullanılmaz; Inspector erişimi için her zaman `[SerializeField] private` tercih edilir
- Prefab referansları, sprite'lar, materyaller, ses klipleri Inspector'dan atanır
- Hard-coded renk, pozisyon, offset değeri yazılmaz; bunlar `[SerializeField]` veya SO üzerinden gelir

---

## 4. Klasör Yapısı

```
Assets/
├── Scripts/
│   ├── Core/           ← GameManager, TurnManager, EventBus
│   ├── Grid/           ← HexGrid, HexCell, PathFinder
│   ├── Units/          ← Unit, UnitStats, UnitAnimator
│   ├── Combat/         ← CombatSystem, AbilitySystem, DamageCalculator
│   ├── AI/             ← EnemyAI, BehaviourTree
│   ├── UI/             ← HUD, TurnUI, UnitInfoPanel
│   ├── Input/          ← InputManager, CameraController
│   └── Data/           ← ScriptableObject tanım sınıfları
├── Data/               ← ScriptableObject asset dosyaları (.asset)
│   ├── Units/
│   ├── Abilities/
│   └── Events/
├── Prefabs/
│   ├── Units/
│   ├── Grid/
│   └── UI/
├── Art/
│   ├── Textures/
│   ├── Materials/
│   └── Models/
├── Audio/
│   ├── Music/
│   └── SFX/
└── Scenes/
    ├── MainMenu.unity
    ├── GameScene.unity
    └── _Test.unity
```

---

## 5. Adlandırma Kuralları

| Tür | Kural | Örnek |
|---|---|---|
| Sınıf | PascalCase | `HexGridManager` |
| Metod | PascalCase | `CalculateDamage()` |
| Private alan | _camelCase | `_currentHealth` |
| SerializeField | _camelCase | `[SerializeField] private int _maxHealth` |
| Property | PascalCase | `public int MaxHealth { get; private set; }` |
| Interface | IPascalCase | `IDamageable` |
| ScriptableObject | PascalCase + SO | `UnitStatsSO` |
| Event | On + PascalCase | `OnUnitDied` |
| Const | UPPER_SNAKE | `MAX_HEX_RANGE` |

---

## 6. Performans Kuralları

- `Update()` her kare çalışır — içinde sadece hafif polling veya flag kontrolü olur
- Pahalı hesaplamalar (pathfinding, AI, sıra hesabı) `Coroutine` veya event tetiklemesiyle yapılır
- `GetComponent<T>()` sonuçları `Awake()`'de önbelleğe alınır, tekrar çağrılmaz
- Nesne havuzu (Object Pool) — mermi, VFX, UI elementi için `Instantiate`/`Destroy` yerine pool kullanılır
- String karşılaştırması için `tag ==` değil, `CompareTag()` kullanılır
- Boş `MonoBehaviour` metodları (`void Update() {}`) silinir

---

## 7. Analiz Araçları

- **UnityEngineAnalyzer** — Unity'ye özgü anti-pattern tespiti; `UnityEngineAnalyzer.CLI.exe Assets/` ile çalıştırılır
- **dotnet format** — kod stili tutarlılığı; `dotnet format OYUN.sln` ile çalıştırılır
- **MCP Sunucusu:** `CoplayDev/unity-mcp` — Unity Editor entegrasyonu (Roslyn validasyon, sahne yönetimi)

---

## 8. Commit ve Dal Kuralları

- Her özellik kendi branch'ında geliştirilir: `feature/hex-grid`, `feature/combat-system`
- Commit mesajı format: `feat: hex grid pathfinding eklendi`
- Prefix'ler: `feat` `fix` `refactor` `docs` `test` `perf`
- Sahne dosyaları (`.unity`) commit'e dahil edilir; `Library/` asla dahil edilmez

---

## 9. Bu Projenin Vizyonu

**For The King + XCOM** ilhamıyla hex-grid tabanlı taktiksel RPG:
- Sıra tabanlı (turn-based) savaş sistemi
- Hex-grid harita navigasyonu
- Karakter sınıfları ve ScriptableObject tabanlı yetenek sistemi
- Prosedürel veya el yapımı harita desteği
- Event-driven tur yönetimi (TurnManager → tüm sistemler dinler)
