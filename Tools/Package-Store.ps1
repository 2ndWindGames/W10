$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOutput=Join-Path $taskRoot 'Builds\Android\OpenedNote-1.0.0-store-assets.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskStream=[IO.File]::Open($taskOutput,[IO.FileMode]::Create)
$taskArchive=[IO.Compression.ZipArchive]::new($taskStream,[IO.Compression.ZipArchiveMode]::Create)
try {
    $taskFiles=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Store') -File -Recurse)
    $taskFiles+=@(Get-Item -LiteralPath (Join-Path $taskRoot 'Documentation\VALIDATION.md'),(Join-Path $taskRoot 'Documentation\DEVICE-QA.md'),(Join-Path $taskRoot 'Documentation\ASSETS.md'))
    foreach($taskFile in $taskFiles) {
        $taskEntry=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskFile.FullName,$taskEntry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $taskArchive.Dispose(); $taskStream.Dispose() }
Write-Output $taskOutput
