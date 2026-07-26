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

### [TASK-001] Hafızadaki tasarım notlarını repoya taşı — status: pending
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
