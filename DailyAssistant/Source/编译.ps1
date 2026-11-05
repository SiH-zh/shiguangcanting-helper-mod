param([string]$GameRoot = 'D:\SteamLibrary\steamapps\common\时光餐厅')
$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$packageRoot = Split-Path -Parent $sourceRoot
$managed = Join-Path $GameRoot 'TimeRestaurant_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '找不到系统自带的编译器。' }
$refs = @('Assembly-CSharp.dll','0Harmony.dll','Newtonsoft.Json.dll','netstandard.dll',
 'UnityEngine.CoreModule.dll','UnityEngine.UI.dll','Unity.TextMeshPro.dll',
 'UnityEngine.UIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll',
 'UnityEngine.IMGUIModule.dll','UnityEngine.Physics2DModule.dll','UnityEngine.AnimationModule.dll',
 'UnityEngine.ImageConversionModule.dll')
$arguments = @('/nologo','/target:library','/optimize+','/warn:4','/codepage:65001',
 ('/out:' + (Join-Path $packageRoot 'DailyAssistant.dll')))
foreach ($ref in $refs) {
    $path = Join-Path $managed $ref
    if (-not (Test-Path -LiteralPath $path)) { throw ('缺少游戏引用：' + $ref) }
    $arguments += '/reference:' + $path
}
$arguments += @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object FullName)
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败，请检查上方错误。' }
Write-Output '日常助手编译完成。'
