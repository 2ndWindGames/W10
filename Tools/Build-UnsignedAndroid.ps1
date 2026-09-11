param(
    [string]$GradleProject = (Join-Path $PSScriptRoot '..\Builds\Android\Gradle'),
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Builds\Android')
)
$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath($GradleProject)
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskLauncher = Join-Path $taskProject 'launcher\build.gradle'
if (!(Test-Path -LiteralPath $taskLauncher)) { throw 'Export the Android Gradle project with OpenedNote > Export Android first.' }
$taskText = [IO.File]::ReadAllText($taskLauncher)
# Explicitly null only release signing. No custom keystore or passwords are used.
if (!$taskText.Contains('// OpenedNote unsigned release')) {
    $taskText += "`n// OpenedNote unsigned release`nandroid { buildTypes { release { signingConfig null } } }`n"
    [IO.File]::WriteAllText($taskLauncher, $taskText, [Text.UTF8Encoding]::new($false))
}
$taskAndroid = Join-Path $UnityEditor 'Data\PlaybackEngines\AndroidPlayer'
$taskJava = Join-Path $taskAndroid 'OpenJDK\bin\java.exe'
$taskGradle = Get-ChildItem -LiteralPath (Join-Path $taskAndroid 'Tools\gradle\lib') -Filter 'gradle-launcher-*.jar' | Select-Object -First 1 -ExpandProperty FullName
if (!$taskGradle) { throw 'Bundled Gradle launcher not found.' }
New-Item -ItemType Directory -Force $taskOutput | Out-Null
Push-Location $taskProject
try {
    & $taskJava '-Xmx4096m' '-classpath' $taskGradle 'org.gradle.launcher.GradleMain' '--no-daemon' 'bundleRelease' 'assembleRelease'
    if ($LASTEXITCODE -ne 0) { throw 'Gradle Android build failed.' }
    Copy-Item -LiteralPath 'launcher\build\outputs\bundle\release\launcher-release.aab' -Destination (Join-Path $taskOutput 'OpenedNote-1.0.0-unsigned.aab') -Force
    Copy-Item -LiteralPath 'launcher\build\outputs\apk\release\launcher-release-unsigned.apk' -Destination (Join-Path $taskOutput 'OpenedNote-1.0.0-unsigned.apk') -Force
    $taskSymbols = 'launcher\build\outputs\native-debug-symbols\release\native-debug-symbols.zip'
    if (Test-Path -LiteralPath $taskSymbols) {
        Copy-Item -LiteralPath $taskSymbols -Destination (Join-Path $taskOutput 'OpenedNote-1.0.0-native-symbols.zip') -Force
    }
} finally { Pop-Location }
& (Join-Path $taskAndroid 'OpenJDK\bin\jarsigner.exe') '-verify' (Join-Path $taskOutput 'OpenedNote-1.0.0-unsigned.aab')
Write-Output 'Unsigned Android artifacts built. Upload signing is intentionally excluded.'
