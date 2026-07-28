# C# terrain portunun Python referansiyla BIREBIR ayni ciktiyi verdigini kanitlar.
#
# Ne yapar: Unity'nin kendi Roslyn derleyicisiyle (SDK gerekmez) TerrainGenerator + PythonRandom'i
# kucuk bir konsol programina derler, 10 seed x 22x25 karo uretir; ayni seti Python referansindan
# (Docs/Balance/tools/harita_terrain_v2.py) uretir; iki ciktiyi satir satir karsilastirir.
#
# Kullanim:  powershell -File dogrula.ps1
# Beklenen:  "*** BIREBIR AYNI ***"

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path "$here\..\..\..\..").Path       # ...\OYUN (bu dosya: Docs\Balance\tools\csharp_port_dogrulama)
$work = Join-Path $env:TEMP "tacticalrpg_port_dogrulama"
New-Item -ItemType Directory -Force -Path $work | Out-Null

# Unity'nin Roslyn'ini bul (surum bagimsiz)
$editors = "C:\Program Files\Unity\Hub\Editor"
$csc = Get-ChildItem $editors -Recurse -Filter "csc.dll" -ErrorAction SilentlyContinue |
       Where-Object { $_.FullName -match "DotNetSdkRoslyn" } | Select-Object -First 1
if (-not $csc) { Write-Error "Unity Roslyn (csc.dll) bulunamadi: $editors"; exit 1 }

# .NET 8 calisma zamani referanslari (yonetilmeyen dll'ler haric)
$rt = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.NETCore.App" -Directory |
      Sort-Object Name -Descending | Select-Object -First 1
$skip = @("System.IO.Compression.Native.dll","mscorrc.dll","Microsoft.DiaSymReader.Native.amd64.dll",
          "clrgc.dll","clrjit.dll","coreclr.dll","hostfxr.dll","hostpolicy.dll","msquic.dll",
          "clretwrc.dll","mscordaccore.dll","mscordbi.dll")
$refs = Get-ChildItem $rt.FullName -Filter "*.dll" |
        Where-Object { $skip -notcontains $_.Name -and $_.Name -notmatch "^(api-ms|mscordaccore_)" } |
        ForEach-Object { "-r:`"$($_.FullName)`"" }

$rsp = @("-target:exe","-langversion:9.0","-nostdlib+","-out:`"$work\verify.dll`"") + $refs
$rsp += "`"$repo\Assets\Scripts\Grid\PythonRandom.cs`""
$rsp += "`"$repo\Assets\Scripts\Grid\TerrainGenerator.cs`""
$rsp += "`"$here\VerifyMain.cs`""
$rsp | Out-File "$work\verify.rsp" -Encoding utf8

& dotnet $csc.FullName "@$work\verify.rsp"
if ($LASTEXITCODE -ne 0) { Write-Error "C# derlemesi basarisiz"; exit 1 }

'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' |
    Out-File "$work\verify.runtimeconfig.json" -Encoding utf8

& dotnet "$work\verify.dll" | Out-File "$work\cs_out.txt" -Encoding utf8
& python "$here\verify_ref.py"  | Out-File "$work\py_out.txt" -Encoding utf8

$cs = Get-Content "$work\cs_out.txt"
$py = Get-Content "$work\py_out.txt"
Write-Host ("C#     : {0} satir" -f $cs.Count)
Write-Host ("Python : {0} satir" -f $py.Count)

$diff = Compare-Object $cs $py -SyncWindow 0
if ($diff) {
    Write-Host ("!!! FARK: {0} satir" -f $diff.Count) -ForegroundColor Red
    $diff | Select-Object -First 10 | Format-Table -AutoSize
    exit 1
}
Write-Host "*** BIREBIR AYNI *** 10 seed x 550 karo = 5500 karo, sifir fark" -ForegroundColor Green
