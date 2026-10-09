# Blender modelleme — kalici devam kaydi

## Kullanici tercihi (2026-10-08)

Efe referans 2D gorselleri kendisi uretir. Codex dogrudan Blender'da gercek 3D mesh, materyal, UV ve dokular uretir. Efe sonucu Blender'da inceler ve sohbetten Turkce duzeltme verir. Gorselden 3D ureten siteler/servisler kullanilmaz.

## Yerel baglanti

- Blender: `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`.
- Workspace: `C:/3D OYUN`.
- Baslatma: `Blender-Baslat.cmd` dosyasina cift tikla veya workspace'te `python Tools/launch_blender.py` calistir. PowerShell betikleri makinede varsayilan olarak kapali.
- ONEMLI: Codex exec aracinin Windows masaustu `CodexSandboxDesktop-*` olarak ayridir. Aractan acilan GUI kullanicinin ekraninda gorunmez. Kullanici `Blender-Baslat.cmd` dosyasini kendi Explorer penceresinden bir kez baslatmalidir; bundan sonra localhost baglantisiyla gorunur Blender'da calisilir. Gorunurlugu yalniz surec listesinden varsayma. Mevcut insan modeli baslaticiyla otomatik yuklenir.
- Kopru: `Tools/blender_mcp.py`, https://github.com/ahujasid/mcp-for-blender topluluk eklentisi.
- Eklenti proje icinden yuklenir; global Blender ayarlari degistirilmez. Normal Blender kisayolu yerine proje baslaticisini kullan.
- Yerel istemci: `python Tools/blender_client.py ping`, `scene`, `execute <script.py>`.
- Sunucu: localhost:9876. Istemci eklentinin yerel soket protokolunu kullanir. Yerlesik Codex MCP araci ve sohbet ici viewport eklentisi kurulmus degildir; sonuc Blender penceresinde gorulur.
- Modeller/dokular/betikler: `MODELLER/<model>/`; referanslar: `MODELLER/Referanslar/`.
- Bilgisayar: RTX 4060 Laptop, 8 GB VRAM.

## Devam etme

Yeni oturum workspace `AGENTS.md`, modelleme skill'i ve bu kaydi okur. Secilen modelin `DEVAM.md` kaydindan devam eder. Sohbet hafizasi garantisi yerine dosyalar kalici kaynaktir.

Unity Git deposu `OYUN/` altindadir. Karo aktarim kurallari `Docs/TILE_PIPELINE.md` ve `Docs/Karo_Tasarim_Klavuzu.md` dosyalarindadir. Modelleme dosyalari Unity deposunun disindadir; otomatik commit edilmez.

## Kurulum dogrulamasi

2026-10-08: Blender GUI acildi. `ping` basarili; `execute_code` ile yeni sahnede altigen taban, tas sutun ve altin kure olusturuldu. Materyaller, kamera ve isiklar eklendi. `MODELLER/Baglanti-Testi/baglanti-testi.blend` kaydedildi; `preview.png` render edildi ve gorsel olarak incelendi. Render sonrasi sahne sorgusu 6 nesneyi dogruladi. Modelleme skill'i quick_validate ile gecti.

## Model ve dusunme seviyesi tercihi

Efe detayli ve zor Blender tasarimlari icin yuksek kalite ister. Oneri: GPT-6 Astra + Extra high (xhigh); hiz/kullanim dengesi icin GPT-6.1 Sol + High. Okunan global ayar GPT-6.1 Sol / high idi. Aktif sohbet modeli bu kurulumla degistirilmedi; uygulamadaki model/dusunme secicisinden degistirilmelidir. Model secimi modelleme kalitesinin garantisi degildir; gorsel dogrulama ve iterasyon gerekir.

`Codex-Modelleme.cmd` CLI'yi bu workspace'te GPT-6 Astra / xhigh secimiyle yeni oturum olarak baslatir. Mevcut uygulama sohbetini degistirmez ve global ayarlari yazmaz. Hesapta model erisimi gerekir.

## Aktif model

Son dosya: `MODELLER/Insan-Bedeni/hippi_detay_v007.blend` (funky sapka, renkli hippi kiyafet, gozluk/baris kolyesi ve cicek demeti). Baslatici ve kurtarma bu dosyayi oncelikle acar; v006/v005 geri donus dosyalaridir. Kullanici degisiklikleri canli gormek ister: buyuk tek blok komutlar yerine viewport'u aralarda yenileyen kucuk adimlar kullan. v007 betikleri 21/22 timer adimlari kullanir, agir kontrol renderlari gorunur GUI'den ayri headless surecte uretilir. Modelin DEVAM.md kaydini mutlaka oku; onceki geometri ureten betikleri korlemesine tekrar calistirma.

2026-10-08: Efe ornek insan bedeni istedi. Model `MODELLER/Insan-Bedeni/insan-bedeni_v001.blend`, devam kaydi `MODELLER/Insan-Bedeni/DEVAM.md`. Degisiklikler acik Blender'da 3 asamada olusturuldu. Sonraki duzeltme bu model uzerinde surer.

Kullanici GUI'yi goremedigini bildirdi. Ayrik sandbox masaustu dogrulandi; onceki 'Blender acik' ifadesi kullanici ekraninda gorunurluk anlamina gelmiyordu. Model kaydedilip ozel masaustundeki Blender kapatildi. Gorunur baglanti icin kullanicinin baslaticiyi kendi masaustunde acmasi bekleniyor.


## Codex kullanim paneli

Yerel panel http://127.0.0.1:8791; ozel simgeli `Codex Kullan?m Paneli.lnk` workspace kokunde. Claude web usage duzeninden esinlenen sade kota cubuklari kullanilir, veriler Codex hesabina aittir. Kaynak/kurulum devami `Tools/KullanimPaneli/DEVAM.md`. Panel yalniz okuma yapar, kendisi model cagrisi yapmaz.
