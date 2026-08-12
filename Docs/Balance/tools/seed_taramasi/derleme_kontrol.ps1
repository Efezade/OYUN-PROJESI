# Unity'yi acmadan TUM proje scriptlerini derler (hata varsa listeler).
#
# Neden: Unity acilis + derleme dongusu yavas; buyuk bir refactor sonrasi "derliyor mu"
# sorusunu saniyeler icinde yanitlamak icin.
#
# Referans listesi Unity'nin KENDI urettigi Assembly-CSharp-Editor.csproj'undan okunur --
# elle dll toplamaya calismak (modul dll'leri + monolitik UnityEngine.dll karisimi) CS0433
# "tur iki yerde birden" hatalarina yol aciyordu.
#
# Kullanim: powershell -File derleme_kontrol.ps1

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path "$here\..\..\..\..").Path
$work = Join-Path $env:TEMP "tacticalrpg_derleme"
New-Item -ItemType Directory -Force -Path $work | Out-Null

$proj = Join-Path $repo "Assembly-CSharp-Editor.csproj"
if (-not (Test-Path $proj)) { Write-Error "Assembly-CSharp-Editor.csproj yok - Unity'de bir kez proje acilmali"; exit 1 }

$editors = "C:\Program Files\Unity\Hub\Editor"
$csc = Get-ChildItem $editors -Recurse -Filter "csc.dll" -ErrorAction SilentlyContinue |
       Where-Object { $_.FullName -match "DotNetSdkRoslyn" } | Select-Object -First 1
if (-not $csc) { Write-Error "Unity Roslyn (csc.dll) bulunamadi"; exit 1 }

# Unity'nin uretmis oldugu referans listesi (Assembly-CSharp.dll haric - kaynagini biz derliyoruz)
$refs = Select-String -Path $proj -Pattern '<HintPath>(.+?)</HintPath>' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -notmatch "ScriptAssemblies\\Assembly-CSharp\.dll$" -and (Test-Path $_) } |
        Select-Object -Unique | ForEach-Object { "-r:`"$_`"" }

$sources = Get-ChildItem (Join-Path $repo "Assets\Scripts") -Recurse -Filter "*.cs" |
           ForEach-Object { "`"$($_.FullName)`"" }

$rsp = @("-target:library","-langversion:9.0","-nostdlib+","-nowarn:0169,0414,0649",
         "-define:UNITY_EDITOR;UNITY_2022_1_OR_NEWER;UNITY_6000_0_OR_NEWER",
         "-out:`"$work\check.dll`"") + $refs + $sources
$rsp | Out-File "$work\check.rsp" -Encoding utf8

Write-Host ("{0} kaynak dosya, {1} referans" -f $sources.Count, $refs.Count)
& dotnet $csc.FullName "@$work\check.rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "DERLEME HATASI" -ForegroundColor Red; exit 1 }
Write-Host "*** DERLENIYOR - hata yok ***" -ForegroundColor Green
