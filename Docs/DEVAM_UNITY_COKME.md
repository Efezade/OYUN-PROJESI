# Unity çökmesi: devam notu

Tarih: 17 Ekim 2026 (Europe/Istanbul). Kullanıcı "devam et" dedi; aşağıdaki son test tamamlandı.

## Sorun ve kanıt

- Kullanıcı oyunu `C:\3D OYUN\Oyun-Paneli.cmd` içindeki Unity Aç düğmesiyle açıyor. Unity Editor, Play'e basılmasa bile birkaç saniyede çöküyor.
- Proje: `C:\3D OYUN\OYUN`; Unity Editor: `6000.2.14f1` (`C:\Program Files\Unity\Hub\Editor\6000.2.14f1\Editor\Unity.exe`).
- Windows Application Error kayıtlarında birçok `Unity.exe` çökmesi var: çoğunda hatalı modül `nvwgf2umx.dll` (NVIDIA sürücüsü), birinde `Unity.dll`; istisna `0xc0000005`.
- Eski `Editor.log`, Unity'nin NVIDIA GeForce RTX 3050 Ti Laptop GPU'yu Direct3D 12 ile kullandığını ve `D3D12CommandList::PrepareExecute` çevresinde assertion/stack trace verdiğini gösterdi. Dolayısıyla GPU kapalı değil. Direct3D 12 yolundaki sürücü/Unity etkileşimi güçlü şüphe; kesin neden henüz doğrulanmadı.
- Sürücü sürümü son logda `32.0.16.1742`; Intel UHD Graphics de var. Sistem Windows 10 19045, 32 GB RAM.

## Yapılanlar

- `C:\3D OYUN\.senkron\panel.ps1` dosyasının yedeği `C:\3D OYUN\.senkron\panel.ps1.before-d3d11.bak` olarak alındı. Unity Aç komutuna `-force-d3d11` eklendi. Panel açıksa eski kod bellekte olabileceği için yeniden başlatılması gerekir.
- Direct3D 11 ile toplu açılış testi başarılı oldu. Test logu: `C:\Users\zeynep\Documents\Codex\2026-10-17\da\work\unity-d3d11-batch.log`. Log `Direct3D 11.0 [level 11.1]`, RTX 3050 Ti ve `Exiting batchmode successfully now! ... return code 0` diyor. Bu, etkileşimli Editor'ün birkaç dakika açık kalacağını kanıtlamaz.
- Etkileşimli Direct3D 11 açılışında `Recovering Scene Backups` penceresi çıktı. Sahne yedeği hakkında karar verilmedi. `Temp\__Backupscenes` klasörü testten önce vardı; batch testinden sonra görünmedi. Yedeklerin durumunu devamda dikkatle incele; otomatik olarak silme/geri yükleme kararı verme.
- İlk deneme `-logFile` ile `File access error` penceresinde durdu; `-logFile` olmadan Editor sahne kurtarma aşamasına ilerledi. Bunu grafik çökmesi olarak sayma.
- Test sırasında kalan `C:\3D OYUN\OYUN\Temp\UnityLockfile`, Editor çalışmadığı doğrulanarak `C:\Users\zeynep\Documents\Codex\2026-10-17\da\work\UnityLockfile.before-d3d11-test` konumuna taşındı.
- İlk oturumda hazırlanan kullanıcı dosyaları `C:\Users\zeynep\Documents\Codex\2026-10-17\da\outputs` içinde (`Unity_D3D11_Ac.cmd`, `Panel_D3D11_Duzelt.cmd`, `Panel_D3D11_Duzelt.ps1`). Panel artık doğrudan değiştirildi; bunlar zorunlu değil.

## Devamda yapılacaklar

1. Bu notu ve `C:\3D OYUN\AGENTS.md`, `C:\3D OYUN\OYUN\CLAUDE.md` yönergelerini oku.
2. Panelin yeni komutu içerdiğini ve Unity Editor'ün kapalı olduğunu doğrula. Panel açıksa yeniden başlatılması gerekir.
3. Sahne yedeği durumunu güvenle incele; etkileşimli Editor'ü Direct3D 11 ile açıp en az birkaç dakika çökmeden kaldığını doğrula. Windows UI Computer Use erişimi önceki denemede reddedildi; erişim hâlâ yoksa kullanıcıyı sahne kurtarma kararı için bilgilendir.
4. Yeni `Editor.log` ve Windows olaylarını kontrol et. Direct3D 11'de de çökerse sürücü/Unity sürümü veya proje içi grafik eklentilerini yeniden değerlendir.
5. `C:\3D OYUN\OYUN` Git durumunda `Assets/Data/Map/Bolum1_Uretilen.asset` ve `Assets/Prefabs/Grid/FogTile.prefab` testten önceki kullanıcı değişiklikleri (19:51 zaman damgaları). Bunlara dokunma. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` batch testi sırasında 20:43'te değişti; yalnızca bu farkın otomatik import olup olmadığını incele, kullanıcı değişikliği olasılığını dışlamadan geri alma.

## Devam testi: 23:26–23:29

- `C:\3D OYUN\OYUN\Temp\__Backupscenes` ve eski `UnityLockfile` yoktu; Unity Editor kapalıydı.
- Unity Editor proje ile `-force-d3d11` kullanılarak etkileşimli açıldı. `Editor.log` hem parametreyi hem `Direct3D 11.0 [level 11.1]` ve RTX 3050 Ti'yi doğruladı.
- Editor yaklaşık 2 dakika 49 saniye sonra hâlâ yanıt veriyordu; pencere başlığı `OYUN - Untitled - Windows, Mac, Linux - Unity 6.2 (6000.2.14f1) <DX11>` idi. Son 6 dakikada yeni Unity Application Error olayı yoktu; yeni logda D3D12 assertion'ı yoktu. Normal Editor açık bırakıldı, işlem kimliği test sırasında 22432 idi.
- `C:\3D OYUN\.senkron\panel.ps1` içindeki Unity Aç komutu `-force-d3d11` içeriyor ve PowerShell sözdizimi kontrolü 0 hata verdi. Panel işlemi çalışmıyordu; bir sonraki panel açılışı yeni betiği kullanacak.
- Bu test sahneyi veya Play modunu açmadı; pencere başlığı `Untitled`. Kullanıcı gerçek sahnesini açtığında son kontrolü yapmalı. Eski kurtarma klasörü kayıp olduğundan, sahne yedekleri konusunda mevcut proje dosyalarını koru.
- Batch testi sırasında `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` değişti: diff, dinamik glif/atlas verisinin temizlendiğini gösteriyor. Test öncesi bu dosyanın Git durumu kaydedilmediği için otomatik geri alınmadı. Diğer iki kullanıcı varlığına dokunulmadı.

Sonraki adım: Kullanıcı kendi sahnesini açıp birkaç dakika gözlemlemeli. Çökme olursa yeni `Editor.log` ve olay kaydını incele. Çökme olmazsa D3D11 panel değişikliği pratik çözüm olarak kalabilir.

## Devam testi: 23:38–23:46 — gerçek oyun

- Kullanıcı açık Editor'ü doğrudan kapatmaya izin verdi. Önce eski Editor kapatıldı; TAM KURULUM önce `da/work/OYUN_QA` adlı ayrı kopyada, sonra `C:\3D OYUN\OYUN` gerçek projede `Assets/Scenes/xd.unity` sahnesinde çalıştırıldı. İki kurulum da batch modunda hata vermeden tamamlandı ve sahneyi kaydetti.
- Gerçek projedeki TAM KURULUM öncesi `xd.unity`, `SampleScene.unity`, `Bolum1_Uretilen.asset`, `FogTile.prefab` ve TextMesh Pro fallback asset'i `da/work/before-full-setup` altında yedeklendi. Harita asset'inde önceden kullanıcı değişikliği vardı; yeni TAM KURULUM onu yeniden üretti. Geri almak gerekirse bu yedeği incele; eski harita verisini yeni sahneyle körlemesine karıştırma.
- `FullSetupBatch.Run` aynı `SceneSetupTool.FullSetup()` zincirini çağırıyor; batch modundaki son dialog atlanıyor, `xd.unity` kaydediliyor. `ProjectSettings/EditorBuildSettings.asset` açılış/build sahnesi `xd.unity` olacak şekilde değiştirildi; eski `SampleScene.unity` devre dışı bırakıldı.
- Ayrı kopyada ve gerçek projede `CrashMovementQA.Run` ile Unity Play modunda 10 gerçek `PlayerController.MoveAlongPath` yürüyüşü yaptırıldı. İkisinde de GPU `NVIDIA GeForce RTX 3050 Ti Laptop GPU`, API `Direct3D11`; 10/10 hamle tamamlandı, yakalanan oyun hatası 0, Editor çökmedi. Gerçek proje raporu `C:\3D OYUN\OYUN\CrashMovementQAResults\progress.txt`, log `da/work/original-movement-play.log`.
- Görüntü karşılaştırması `CrashMovementQAResults/miniature_on.png`, `miniature_off.png`, `after_10_moves.png`. Minyatür/tilt-shift efekti uzaktaki modelleri bilerek bulanıklaştırıyor. Varsayılan ayar kapatıldı (`DisplaySettings._defaultMiniature=false`); açıkken de keskin kaynak örneklemesi `sampler_LinearClamp` oldu. Ayarlardan MİNYATÜR tekrar açılabilir. Var olan model geometrisi düşük poligonlu sanatsal içerik; bu değişiklik onu yüksek poligonlu yapmaz.
- Asıl önceki çökme için en güçlü kanıt D3D12/NVIDIA sürücü yolu. Direct3D11 ile 20 toplam hamlelik test çökmedi; bu sürücü kodundaki kesin hatayı ispatlamaz ama kullanıcı senaryosundaki çökme tekrar etmedi. Panelin `-force-d3d11` değişikliği korunmalı.
- `CrashMovementQA` proje içinde tekrar çalıştırılabilir tanılama kodu; test sonunda `CrashMovementQAResults` dosyalarını oluşturur ve Play'den çıkar. Etkileşimli Editor'de `FullSetupBatch.OpenAndPlay` doğrudan `xd.unity` sahnesini Play modunda açar.
- 23:46'da etkileşimli Editor `-force-d3d11 -executeMethod TacticalRPG.Editor.FullSetupBatch.OpenAndPlay` ile açıldı. Pencere başlığı `OYUN - xd ... <DX11>`, log `Oyun Play modunda acildi: Assets/Scenes/xd.unity`. 20 saniyeden uzun süre yanıt verdi; yeni çökme/exception satırı yoktu. İşlem kimliği o anda 9888; kullanıcı döndüğünde hâlâ açıksa Play modundan devam edebilir.

## 18 Ekim 2026 — düşük çözünürlük ve görsel kalite

- Kullanıcı minyatür varsayılanı kapatılmasına rağmen görüntünün pikselli ve düşük kaliteli kaldığını bildirdi. Etkileşimli Unity Game penceresindeki gerçek çizim boyutu log ile **880×382** ölçüldü; PC monitörü **1920×1080**. `Screen.SetResolution` çağrısı Editor'ün Free Aspect Game penceresinde etkili olmadı.
- `GameViewResolutionTool.Configure()` Unity 6000.2.14f1'in GameView boyut listesindeki sabit 1920×1080 seçeneğini buluyor/ekliyor ve seçiyor. `FullSetupBatch.OpenAndPlay()` ve GUI `SceneSetupTool.FullSetup()` buna bağlandı. Yeniden açılışta Play logu **1920×1080** doğruladı. Daha önemlisi, Editor paneldeki normal komut gibi yalnız `-projectPath ... -force-d3d11` ile kapatılıp açıldı ve kullanıcı `Ctrl+P` ile Play'e geçti; GameView seçimi kalıcıydı ve log yine **1920×1080** dedi. Panel açılış logu `da/work/panel-style-open.log`.
- PC URP asset'inde MSAA 1→4 yapıldı. `TileVisualFactory` prosedürel zemin dokuları 128×128 ve düşük kalite sıkıştırma yerine 512×512 ve yüksek kalite sıkıştırma kullanıyor. Var olan düşük çözünürlüklü 12 yüzey, `BuildAll(false)` içinden bir kez yükseltiliyor; bu adım zaten TAM KURULUM zincirinde. Gerçek projede yeniden üretim batch logu `da/work/quality-upgrade-original.log`; önceki 12 PNG/meta ve PC_RPAsset `da/work/before-visual-upgrade` altında yedeklendi.
- Yükseltme sonrası ayrı kopyada 1920×1080 görüntü alındı. Gerçek projede D3D11 ile 10 ardışık hamle tekrar tamamlandı, oyun hatası 0 (`da/work/quality-movement-retry.log`, `CrashMovementQAResults/progress.txt`). İlk rastgele haritada pusu, 4. test hamlesini oyun gereği kilitledi; bu GPU çökmesi değildi. Testin kapanışında `RavenWatch.StartHunterMotion` pasif nesnede coroutine başlatmaya çalıştı; koruma eklendi. İkinci rastgele haritada 10/10 hamle tamamlandı.
- Sis kapatılmış yakın plan tanılama görüntüsünde bazı düz beyaz dekorlar ve düşük poligonlu şekiller var. Bunlar kaynak prosedürel modellerin görsel tasarımı; 1920×1080, MSAA ve doku yükseltmesi onları yeni 3B modellere dönüştürmez. Daha ayrıntılı sanat varlıkları gerekirse ayrı model çalışması gerekir.
- Editor şu anda normal panel komutuyla `xd.unity` sahnesinde Play modunda açık (test sırasında PID 4468). Kapanıp açıldığında sabit 1920×1080 GameView seçimi `UserSettings/Layouts/default-6000.dwlt` içine kaydedildi.
- Game sekmesi Editor içinde fiziksel olarak küçük kaldığından `GameViewResolutionTool` Play sırasında büyütmeyi de açıyor. Son etkileşimli açılışta sabit Full HD seçimi ve Play görüntüsü 1920×1080 olarak tekrar loglandı; Unity `xd.unity` sahnesinde DX11 ile yanıt veriyordu (test sırasında PID 3496). Bu ayar Unity'nin bu sürümünün dahili GameView API'sine dayanır; sürüm yükseltilirse uyarı logu kontrol edilmeli.
