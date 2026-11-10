$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot)
./build.ps1
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /out:tools\Tests.exe /r:dist\PeriBatt.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll tools\Tests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
try {
 Copy-Item -LiteralPath dist/PeriBatt.exe -Destination tools/PeriBatt.exe
 & ./tools/Tests.exe
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
 & $csc /nologo /out:tools\LayoutTests.exe /r:dist\PeriBatt.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll tools\LayoutTests.cs
 if ($LASTEXITCODE -ne 0) { throw 'Layout tests build failed' }
 & ./tools/LayoutTests.exe
 if ($LASTEXITCODE -ne 0) { throw 'Layout tests failed' }
} finally { Remove-Item -LiteralPath tools/Tests.exe,tools/PeriBatt.exe,tools/LayoutTests.exe -ErrorAction SilentlyContinue }
