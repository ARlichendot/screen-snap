# Build ScreenSnap.exe
# Only needs the .NET Framework 4.x that ships with Windows -- no SDK, no NuGet, no third-party libs.
# (Kept ASCII-only so it parses correctly in Windows PowerShell 5.1 under any code page.)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Prefer the 64-bit compiler, fall back to the 32-bit one.
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "csc.exe not found. Make sure .NET Framework 4.x is enabled."
}

$out = Join-Path $root 'ScreenSnap.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/out:$out" `
    "/win32icon:$(Join-Path $root 'icon.ico')" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    (Join-Path $root 'ScreenSnap.cs')

if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
Write-Host "Build OK: $out ($((Get-Item $out).Length) bytes)"
