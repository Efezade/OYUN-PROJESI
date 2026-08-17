# 30-seed havuzu taramasi.
#
# Oyunda calisan uretici kodunun AYNISINI (MapNoise + TileCatalog + PythonRandom + TerrainGenerator)
# Unity'nin kendi Roslyn derleyicisiyle kucuk bir konsol programina derler ve binlerce seed tarar.
# Unity'yi ACMAYA GEREK YOK.
#
# Kullanim:
#   powershell -File tara.ps1                 # 4000 aday, en iyi 30
#   powershell -File tara.ps1 -Seeds 12000 -Want 30
#
# Cikti: sonuc.txt (ayni klasor) + ekrana ozet.

param(
    [int]$Seeds = 4000,
    [int]$Want  = 30,
    [switch]$Arena,    # savas arenalarini olc (overworld seed taramasi yerine)
    [switch]$Oz,       # 30 seed'in oz yerlesimini olc (60-80 hedefi tutuyor mu)
    [switch]$Minimap   # minihatita boyamasini PNG olarak yaz (gorsel dogrulama)
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path "$here\..\..\..\..").Path         # ...\OYUN  (bu dosya: Docs\Balance\tools\seed_taramasi)
$work = Join-Path $env:TEMP "tacticalrpg_seed_taramasi"
New-Item -ItemType Directory -Force -Path $work | Out-Null

# Unity'nin Roslyn'ini bul (surum bagimsiz)
$editors = "C:\Program Files\Unity\Hub\Editor"
$csc = Get-ChildItem $editors -Recurse -Filter "csc.dll" -ErrorAction SilentlyContinue |
       Where-Object { $_.FullName -match "DotNetSdkRoslyn" } | Select-Object -First 1
if (-not $csc) { Write-Error "Unity Roslyn (csc.dll) bulunamadi: $editors"; exit 1 }

# .NET calisma zamani referanslari (yonetilmeyen dll'ler haric)
$rt = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.NETCore.App" -Directory |
      Sort-Object Name -Descending | Select-Object -First 1
$skip = @("System.IO.Compression.Native.dll","mscorrc.dll","Microsoft.DiaSymReader.Native.amd64.dll",
          "clrgc.dll","clrjit.dll","coreclr.dll","hostfxr.dll","hostpolicy.dll","msquic.dll",
          "clretwrc.dll","mscordaccore.dll","mscordbi.dll")
$refs = Get-ChildItem $rt.FullName -Filter "*.dll" |
        Where-Object { $skip -notcontains $_.Name -and $_.Name -notmatch "^(api-ms|mscordaccore_)" } |
        ForEach-Object { "-r:`"$($_.FullName)`"" }

$rsp = @("-target:exe","-langversion:9.0","-nostdlib+","-optimize+","-out:`"$work\seed.dll`"") + $refs
$rsp += "`"$repo\Assets\Scripts\Grid\PythonRandom.cs`""
$rsp += "`"$repo\Assets\Scripts\Grid\MapNoise.cs`""
$rsp += "`"$repo\Assets\Scripts\Grid\TileCatalog.cs`""
$rsp += "`"$repo\Assets\Scripts\Grid\TerrainGenerator.cs`""
$rsp += "`"$repo\Assets\Scripts\Core\CombatMath.cs`""
$rsp += "`"$repo\Assets\Scripts\Grid\CombatArenaGenerator.cs`""
$rsp += "`"$here\ArenaReport.cs`""
$rsp += "`"$here\EssenceReport.cs`""
$rsp += "`"$here\MinimapPreview.cs`""
$rsp += "`"$here\SeedSearchMain.cs`""
$rsp | Out-File "$work\seed.rsp" -Encoding utf8

& dotnet $csc.FullName "@$work\seed.rsp"
if ($LASTEXITCODE -ne 0) { Write-Error "C# derlemesi basarisiz"; exit 1 }

'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' |
    Out-File "$work\seed.runtimeconfig.json" -Encoding utf8

$sw = [System.Diagnostics.Stopwatch]::StartNew()
if     ($Arena)   { & dotnet "$work\seed.dll" arena | Out-File "$here\savas_sonuc.txt" -Encoding utf8 }
elseif ($Oz)      { & dotnet "$work\seed.dll" oz    | Out-File "$here\oz_sonuc.txt"    -Encoding utf8 }
elseif ($Minimap) { & dotnet "$work\seed.dll" minimap }
else              { & dotnet "$work\seed.dll" $Seeds $Want | Out-File "$here\sonuc.txt" -Encoding utf8 }
$sw.Stop()

Write-Host ("Tarama bitti: {0:N1} sn" -f $sw.Elapsed.TotalSeconds) -ForegroundColor Green
if     ($Arena)   { Get-Content "$here\savas_sonuc.txt" }
elseif ($Oz)      { Get-Content "$here\oz_sonuc.txt" }
elseif ($Minimap) { Copy-Item "$work\minimap_*.png" "$here\" -Force; "PNG'ler: $here" }
else              { Get-Content "$here\sonuc.txt" -TotalCount 60 }
