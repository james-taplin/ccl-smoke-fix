param(
    [string]$DVRoot = "B:\SteamLibrary\steamapps\common\Derail Valley"
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$umm = Get-ChildItem $DVRoot -Recurse -File | Where-Object { $_.Name -in "UnityModManager.dll","UnityModManagerNet.dll" } | Select-Object -First 1
$harmony = Get-ChildItem $DVRoot -Recurse -File -Filter 0Harmony.dll | Select-Object -First 1
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
& $dotnet build (Join-Path $here "CCLSmokeFix\CCLSmokeFix.csproj") -c Release --nologo "/p:DVRoot=$DVRoot" "/p:UMMDll=$($umm.FullName)" "/p:HarmonyDll=$($harmony.FullName)"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$pkgRoot = Join-Path $here "package"
$pkg = Join-Path $pkgRoot "CCLSmokeFix"
if (Test-Path $pkgRoot) { Remove-Item $pkgRoot -Recurse -Force }
New-Item $pkg -ItemType Directory | Out-Null
Copy-Item (Join-Path $here "CCLSmokeFix\bin\Release\net48\CCLSmokeFix.dll") $pkg
Copy-Item (Join-Path $here "Info.json") $pkg
$zip = Join-Path $here "CCLSmokeFix_v0.1.0.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $pkg -DestinationPath $zip
Write-Host "Package: $zip (not installed; copy to <DV>\Mods yourself)"
