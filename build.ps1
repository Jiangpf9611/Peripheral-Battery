$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sdkVersion = Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/References' -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty Name
if (-not $sdkVersion) { throw 'Windows SDK required to build Bluetooth LE discovery' }
$sdkRefs = 'C:/Program Files (x86)/Windows Kits/10/References/' + $sdkVersion
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
python tools/make-icon.py
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed' }
New-Item -ItemType Directory -Force dist | Out-Null
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:src/assets/PeriBatt.ico /resource:src/assets/PeriBatt-window.ico,PeriBatt.ico /resource:src/assets/PeriBatt.png,PeriBatt.png /win32manifest:src\app.manifest /out:dist\PeriBatt.exe /r:"C:/Program Files (x86)/Windows Kits/10/UnionMetadata/$sdkVersion/Windows.winmd" /r:"$framework/System.Runtime.WindowsRuntime.dll" /r:"C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Facades/System.Runtime.dll" /r:"C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Facades/System.Threading.Tasks.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll src\AudioTypes.cs src\Devices.cs src\Protocols.cs src\DeviceRegistry.cs src\App.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }





