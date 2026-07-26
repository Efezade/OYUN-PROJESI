# Görev Gelen Kutusu (Inbox)

> Bu dosya, ikinci bir PC'de (Unity dosyaları olmadan) Efe + Kardelen arasında tartışılıp
> karara bağlanan işlerin Unity tarafına aktarım noktasıdır.
>
> **Unity tarafı Claude için kural:** Her oturum başında bu dosyayı oku.
> - `pending` görevleri işle → uygula → `Docs/DECISION_LOG.md`'ye ne yapıldığını yaz →
>   bu dosyada görevi `done` işaretle (satırı silme, arşiv olarak kalsın).
> - Görev belirsiz/çelişkili ise `blocked` işaretle + neden yaz, silme — karşı taraf netleştirecek.
> - `Docs/GAME_DESIGN.md` denge/stat kararları için tek doğruluk kaynağıdır, görev metniyle çelişirse
>   GAME_DESIGN.md önceliklidir.

---

## Format

```
### [TASK-ID] Kısa başlık — status: pending|in_progress|blocked|done
Kaynak: (GAME_DESIGN.md ilgili bölüm / tartışma tarihi)
Açıklama: ...
Kabul kriteri: ...
Performans notu: (varsa risk — büyük harita, çok sayıda birim, vb.)
```

---

## Görevler

### [TASK-001] Hafızadaki tasarım notlarını repoya taşı — status: done
Kaynak: Docs/DECISION_LOG.md içindeki `[[reference-game-design]]`, `[[reference-ui-layout]]`,
`[[reference-tile-pipeline]]` referansları.
Açıklama: Bu üç not şu an yalnızca bu PC'deki Claude hafızasında duruyor, repoda yedeği yok
(git'e işlenmiyor, tek makineye bağımlı). Lütfen bu üç hafıza notunun güncel içeriğini oku ve
`Docs/GAME_DESIGN.md` (tasarım/hikaye/mekanik) ve `Docs/UI_LAYOUT.md` (6 ekran UI, yeni dosya) ve
`Docs/TILE_PIPELINE.md` (karo import hattı, yeni dosya) olarak üç ayrı markdown dosyasına dök.
İçerik kaybı olmasın; hafıza notları silinmesin, sadece repoya da yedeklensin (kaynak olarak kalsın).
Kabul kriteri: Üç yeni/dolu dosya repoda mevcut, DECISION_LOG.md'deki `[[...]]` referansları bu
dosyalara işaret ediyor (dosya yolu olarak güncellenmiş).
Performans notu: yok (sadece dokümantasyon).

> **DONE (Watson, 2026-07-26):** `Docs/UI_LAYOUT.md` + `Docs/TILE_PIPELINE.md` yeni oluşturuldu;
> `reference-game-design` içeriği `Docs/GAME_DESIGN.md` **§5** olarak EKLENDİ (bölüm 0-4 canonical
> içerik korunarak — üzerine yazılmadı, hikaye/mekanik iskeleti Unity tarafının alanı). DECISION_LOG.md'deki
> 3 `[[...]]` referansı dosya yollarına güncellendi (eski wikilink parantez içinde tutuldu). Hafıza notları
> SİLİNMEDİ (kaynak olarak kalıyor). Detay: DECISION_LOG.md 2026-07-26 girişi.

### [TASK-002] Oturum başında otomatik git pull (SessionStart hook) — status: done
Kaynak: Sherlock + kullanıcı arası tartışma (2026-07-26) — Watson bir oturuma başladığında
INBOX_TASKS.md'nin güncel halini görmesi için önce git pull yapması gerekiyor, ama bu şu an
CLAUDE.md kuralına (bkz §9) bağlı, otomatik değil.
Açıklama: Claude Code'un "SessionStart hook" özelliğini kullanarak bu projede (`.claude/settings.json`
veya proje ayarları) her oturum açılışında otomatik `git pull` çalıştıracak bir hook kur. Böylece
Watson elle hatırlamadan/istenmeden de repo her zaman güncel olur. Nasıl kurulacağını bilmiyorsan
"update-config" skill'ini kullanabilirsin (Claude Code kendi settings.json'ını yapılandırma konusunda
yardımcı olur).
Kabul kriteri: Yeni bir Claude Code oturumu açıldığında otomatik olarak git pull çalıştığı
doğrulanmış (test edilmiş) olsun.
Performans notu: yok.

> **DONE (Watson, 2026-07-26):** SessionStart hook kuruldu — `C:\3D OYUN\.claude\settings.local.json`
> (bu makineye özel, OYUN repo'suna DAHİL DEĞİL → Sherlock'un makinesi etkilenmez; o taraf isterse kendi
> hook'unu kurar). Script `C:\3D OYUN\.claude\hooks\session-pull.sh`: tracked commit'siz değişiklik varsa
> pull ATLA, temizse `git pull --ff-only`, her zaman exit 0, sonucu additionalContext ile bildirir.
> **Script iki dalda da test edildi** (temiz→ff-only OK; kirli→ATLANDI; ikisi de geçerli JSON+exit 0).
> Canlı SessionStart ateşlemesi yeni oturumda doğrulanacak (bu tur içinde tetiklenemez).

Not (Sherlock, 2026-07-26): Watson'ın önerdiği güvenli versiyon (uncommitted değişiklik varsa
pull'u atla / --ff-only) onaylandı — uygulanmış, teşekkürler.

### [TASK-003] DECISION_LOG.md temizlik geçişi — status: pending
Kaynak: Sherlock+Watson tartışması (2026-07-26) — Watson'ın önerisi, Sherlock onayladı.
Açıklama: DECISION_LOG.md'yi tek commit'te temizle:
1. Tamamlanmış fazların (TAMAMLANDI ✓ / BİTTİ ✓) blow-by-blow anlatımını yeni
   `Docs/DECISION_LOG_ARCHIVE.md`'ye taşı; ana logda sadece 1 satır özet + commit hash +
   arşive link kalsın.
2. **İstisna:** "Tuzaklar/Dersler" niteliğindeki satırlar (sihirli sabitler, gotcha'lar —
   ör. köprü FBX quaternion flip, off-center geometri felaketi, footprint kalkanı) arşive
   TAŞINMASIN; ana logda ayrı bir "Tuzaklar/Dersler" bölümünde kalsın.
3. Yeni/kalan girişlerde dosya-alan enumerasyonunu bırak, KARAR + NEDEN + commit hash +
   (varsa) ders formatına geç (962b076'daki format örnek). Eski girişleri zorla bu formata
   çevirmeye gerek yok — sadece arşive taşınmayanlar için, bozulma riski almadan mümkün olduğunca.
4. GAME_DESIGN.md §5 / TILE_PIPELINE.md ile birebir çakışan eski hikaye/karo anlatısını
   kısalt, canonical dosyaya link bırak.
5. BAYAT "güncel durum / son push / bir sonraki adım" bloğunu SİL — bu bilginin canlı
   kaynağı Watson'ın kendi hafızası; DECISION_LOG'da olmamalı (bayatlama + memory ile
   çelişme riski).
Kabul kriteri: DECISION_LOG.md yalın + ters-kronolojik + sadece aktif kararlar/tuzaklar;
DECISION_LOG_ARCHIVE.md tam tarihi içeriyor, içerik kaybı yok; Sherlock diff'i review eder.
Performans notu: yok.
