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

## 8. Commit ve Dal Kuralları

- Her özellik kendi branch'ında geliştirilir: `feature/hex-grid`, `feature/combat-system`
- Commit mesajı format: `feat: hex grid pathfinding eklendi`
- Prefix'ler: `feat` `fix` `refactor` `docs` `test` `perf`
- Sahne dosyaları (`.unity`) commit'e dahil edilir; `Library/` asla dahil edilmez

---

## 9. Görev Gelen Kutusu (Inbox) Akışı

İkinci bir PC'de (Unity dosyaları olmadan) Efe + Kardelen tasarım/denge kararları tartışıyor;
bu kararlar `Docs/GAME_DESIGN.md` (tek doğruluk kaynağı) ve `Docs/INBOX_TASKS.md` (görev listesi)
üzerinden buraya push ediliyor. Bu projede o taraftaki Claude oturumuna **Sherlock**, bu tarafa
(sen, kod/implementasyon tarafı) **Watson** deniyor — dokümanlarda/loglarda bu isimlerle geçebilirsin.

- **HER OTURUMUN İLK ADIMI, konu ne olursa olsun:** önce `git pull` çalıştır (Sherlock'un push'ladığı
  yeni dosyalar/görevler local'de görünmeyebilir), sonra `Docs/INBOX_TASKS.md`'yi oku. Bu adımı
  atlama — Efe/Kardelen konuyu hatırlatmasa bile varsayılan davranışın bu olsun.
- **TEK SEFERDE SADECE BİR `pending` GÖREV işle (2026-07-27 kararlaştırıldı).** Birden fazla
  `pending` görev varsa hepsini art arda/aynı oturumda bitirmeye ÇALIŞMA — birini bitir, açıkla, DUR.
- **Görev bitince:** `Docs/DECISION_LOG.md`'ye ne yapıldığını (nasıl/neden dahil) yaz, INBOX_TASKS.md'de
  görevi `done` DEĞİL **`awaiting_review`** işaretle + kısa bir özet not düş (satırı silme — arşiv
  kalsın). `done`'a çevirme yetkisi Watson'da değil — Efe/Kardelen (Sherlock tarafı) inceleyip onaylar.
- **`awaiting_review` durumunda bekleyen bir görev varken YENİ bir `pending` göreve BAŞLAMA** — bu,
  canlı bir onay bağlantımız olmadığı için (iki ayrı PC/oturum) dosya-tabanlı bir "onay kapısı"dır.
  Sherlock bir sonraki taraf-geçişinde inceler: onaylarsa `done`'a çevirir (ya da "doğru, devam"
  notu düşer), sorun bulursa `blocked` + nedenini yazar. Ancak öyle işaretlenmiş bir görev varsa
  önce ONU düzelt, başka pending'e geçme.
- **Belirsiz/çelişkili görev:** `blocked` işaretle + nedenini yaz, sessizce atlamayan.
- `Docs/GAME_DESIGN.md` ile mevcut kod/ROADMAP arasında çelişki varsa GAME_DESIGN.md önceliklidir;
  büyük bir çelişkiyse INBOX_TASKS.md'ye not düşüp diğer tarafın netleştirmesini bekle.

**Dosya sahipliği (çakışmayı önlemek için, 2026-07-26 kararlaştırıldı):**
- `Docs/DECISION_LOG.md` — **Watson-sahipli.** Sherlock sadece okur, yazmaz. Yalın, ters-kronolojik,
  sadece aktif kararlar + "Tuzaklar/Dersler" bölümü. Tamamlanmış faz detayları
  `Docs/DECISION_LOG_ARCHIVE.md`'ye taşınır. Bayat "güncel durum/son push" bloğu tutulmaz —
  o bilginin canlı kaynağı Watson'ın kendi hafızası.
- `Docs/INBOX_TASKS.md` — **append-only.** Sherlock yeni görevleri sona ekler; Watson yalnız
  ilgili görevin status/DONE notunu günceller, başka görevlere dokunmaz.
- `Docs/GAME_DESIGN.md` — **Sherlock-sahipli.** Watson normalde sadece okur; yalnız INBOX'tan
  açıkça görevlendirilirse (TASK-001'deki gibi) yazar, canonical bölümlerin üzerine yazmaz.
- `ROADMAP.md` = ileriye dönük plan · `Docs/DECISION_LOG.md` = geriye dönük NEDEN+ders ·
  `Docs/GAME_DESIGN.md` = canonical sayılar. Yeni içerik yazarken bu ayrımı koru, üçünde aynı
  şeyi tekrar anlatma.

---

## 10. Bu Projenin Vizyonu

**For The King + XCOM** ilhamıyla hex-grid tabanlı taktiksel RPG:
- Sıra tabanlı (turn-based) savaş sistemi
- Hex-grid harita navigasyonu
- Karakter sınıfları ve ScriptableObject tabanlı yetenek sistemi
- Prosedürel veya el yapımı harita desteği
- Event-driven tur yönetimi (TurnManager → tüm sistemler dinler)
