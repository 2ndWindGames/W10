param([string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskAndroid=Join-Path $UnityEditor 'Data\PlaybackEngines\AndroidPlayer'
$taskJava=Join-Path $taskAndroid 'OpenJDK\bin\java.exe'
$taskBundletool=Join-Path $taskAndroid 'Tools\bundletool-all-1.17.2.jar'
$taskAab=Join-Path $taskRoot 'Builds\Android\OpenedNote-1.0.0-unsigned.aab'
$taskApk=Join-Path $taskRoot 'Builds\Android\OpenedNote-1.0.0-unsigned.apk'
$taskQa=Join-Path $taskRoot 'BuildArtifacts\QA'
& $taskJava '-jar' $taskBundletool 'validate' ('--bundle='+$taskAab) *> (Join-Path $taskQa 'bundle-validation.txt')
if($LASTEXITCODE -ne 0){throw 'Bundle validation failed'}
& $taskJava '-jar' $taskBundletool 'dump' 'manifest' ('--bundle='+$taskAab) '--module=base' > (Join-Path $taskQa 'bundle-manifest.xml')
if($LASTEXITCODE -ne 0){throw 'Manifest extraction failed'}
& $taskJava '-jar' $taskBundletool 'dump' 'config' ('--bundle='+$taskAab) > (Join-Path $taskQa 'bundle-config.json')
if($LASTEXITCODE -ne 0){throw 'Bundle config extraction failed'}
& (Join-Path $taskAndroid 'SDK\build-tools\36.0.0\zipalign.exe') '-c' '-P' '16' '-v' '4' $taskApk > (Join-Path $taskQa 'apk-zipalign.txt')
if($LASTEXITCODE -ne 0){throw 'APK zip/page alignment failed'}
& (Join-Path $taskAndroid 'SDK\build-tools\36.0.0\aapt.exe') 'dump' 'badging' $taskApk > (Join-Path $taskQa 'apk-badging.txt')
if($LASTEXITCODE -ne 0){throw 'APK inspection failed'}
$taskHashes=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Builds\Android') -File | Where-Object { $_.Extension -in '.aab','.apk','.zip' } | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower()+'  '+$_.Name }
[IO.File]::WriteAllLines((Join-Path $taskRoot 'Builds\Android\SHA256SUMS.txt'),$taskHashes,[Text.UTF8Encoding]::new($false))
Write-Output 'Bundle structure, APK alignment, manifest extraction and file hashes completed.'
