# Watson Görev İnceleme Kılavuzu

> Bu dosya, Watson'ın (Unity/kod tarafındaki Claude oturumu) bitirdiği görevleri incelemek için
> yazıldı — teknik/oyun-tasarım geçmişi olmayan biri de takip edebilsin diye sade tutuldu.

## Genel bağlam (kısaca)

Bu proje iki "taraf" arasında git üzerinden yürüyor:
- **Sherlock** = tasarım tarafı (Efe+Kardelen'in bulunduğu PC, Unity yok, sadece kararlar/belgeler).
- **Watson** = Unity/implementasyon tarafı (kodu yazan Claude oturumu).

Kararlar `Docs/GAME_DESIGN.md`'de, yapılacaklar `Docs/INBOX_TASKS.md`'de tutulur. Watson bir görevi
bitirince artık direkt `done` yazmıyor — **`awaiting_review`** yazıp DURUYOR, senin onayını
bekliyor. Onay gelmeden bir sonraki göreve geçmiyor (2026-07-27'de kararlaştırılan yeni kural).

## Senin (incelemeci) rolün — adım adım

1. `Docs/INBOX_TASKS.md` dosyasını aç, status'u **`awaiting_review`** olan görevi bul.
2. `Docs/DECISION_LOG.md`'nin EN ÜSTÜNDEKİ (en yeni) girişini oku — Watson orada ne yaptığını
   açıklamış olacak.
3. Görevin altındaki **"Kabul kriteri"** satırına bak (aynı dosyada, görevin içinde yazıyor).
   Watson'ın yaptığı bunu karşılıyor mu? Mümkünse gerçekten Unity'de/oyunda dene — sadece yazıyı
   okumak yerine, ilgili özelliği açıp bir kere kendin kullan.
4. Karar ver:
   - **Doğru/tamamsa:** `INBOX_TASKS.md`'de o görevin satırındaki `awaiting_review` yazısını
     `done` yap. Satırı SİLME (arşiv olarak kalsın).
   - **Eksik/yanlışsa:** `blocked` yap, hemen altına **NE eksik/yanlış olduğunu açıkça yaz**
     (ör. "gün 14'te harita donmuyor, hâlâ ilerlenebiliyor" gibi somut, test edilebilir bir cümle).
     Watson bir sonraki oturumunda bunu okuyup düzeltecek.
5. Değişikliği kaydet: `git add Docs/INBOX_TASKS.md`, sonra `git commit` ve `git push` (ya da bir
   Claude Code oturumunda "INBOX_TASKS.md'yi commit'le" de yeter). Watson bir sonraki oturumunda
   `git pull` yapınca bunu görecek.

**Emin olamadığın bir şeyle karşılaşırsan `blocked` yapıp "emin değilim, X mi Y mi olmalı?" diye
soru yazman yeterli** — kendi başına büyük bir tasarım kararı vermen gerekmiyor, sonra Efe/Kardelen'e
danışılır.

## Sırada bekleyen görevler ve her birinde nelere bakman gerektiği

Şu an bu sırayla işlenecekler (`Docs/INBOX_TASKS.md`'de tam detayları var):

- **TASK-003** (DECISION_LOG temizliği): `Docs/DECISION_LOG_ARCHIVE.md` diye yeni bir dosya
  oluşmuş mu, eski detaylı anlatılar oraya taşınmış mı, ana log sade kalmış mı kontrol et. İçerik
  KAYBOLMAMALI, sadece yer değiştirmeli.
- **TASK-004** (9-harita/3x3 ekran kontrolü): Oyunda "HARİTA" ekranını aç. 3x3'lük bir dünya
  haritası hâlâ duruyorsa ve bunun "8 bölüm" ile uyumlu hale getirilip getirilmediği belirsizse,
  Watson'ın notunu oku — belirsizse `blocked` bırakıp sen de emin değilsen bana/Efe'ye sor.
- **TASK-005** (harita üretimi): Bölüm 1'e birkaç kere gir (ya da retry et), her seferinde
  haritanın **görünüşü/engelleri farklı** olmalı (10 sabit varyanttan biri geliyor). Bir öz
  karosunu topla, o karo boş/tükenmiş kalmalı.
- **TASK-006** (görev/zindan/market/kule): 3 tane zorunlu görev işareti haritanın her yerinden
  görünüyor mu (sis olsa bile)? Bir zindana yaklaşınca girmeden ÖNCE zorluğu görebiliyor musun ama
  ödülü GÖRMÜYOR musun (bu önemli — tam tersi olursa, yani ödül önceden görünüyorsa, `blocked` yaz)?
  Gündüz marketi geceleyin kapalı mı? Gözetleme kulesini kullanınca etraf kalıcı açılıyor mu?
  Boss'a haritanın farklı bir yerinden de girebiliyor musun?
- **TASK-007** (zaman baskısı): Gün 10'u geçince zindanlar daha pahalı/riskli hissettiriyor mu?
  Silinecek karolar ÖNCEDEN çatlama gibi bir uyarı veriyor mu (sessizce mi kayboluyor — sessizce
  kayboluyorsa bu KESİNLİKLE `blocked`, adaletsiz hissettirir)? Gün 14'te gerçekten ilerlenemez
  hale geliyor mu? Kaybedince SADECE o bölüm mü sıfırlanıyor yoksa her şeyin mi (roster/birimlerin
  hâlâ elinde kalması lazım — kalmıyorsa `blocked`)?

## Takılırsan nereye bakabilirsin

- `Docs/GAME_DESIGN.md` — bütün tasarım kararlarının resmi/güncel hâli.
- `Docs/Balance/HARITA_DENGE_DURUM.md` — bu haritanın neden bu şekilde tasarlandığının hikayesi
  (sayılar nereden geldi, hangi denemeler yapıldı).
- Rakamların çoğu (zindan zorluğu, collapse zamanlaması, seviye sistemi gibi) hâlâ **TASLAK** —
  "bu sayı ideal mi" diye takılmana gerek yok, önemli olan mekaniğin ÇALIŞMASI ve Kabul Kriteri'ni
  karşılaması. İnce ayar sonradan oynanarak yapılacak.
