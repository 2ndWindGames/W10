param([int]$TimeoutSeconds=150)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskCommand=Join-Path $taskRoot 'BuildArtifacts\editor-command.txt'
$taskResult=Join-Path $taskRoot 'BuildArtifacts\editor-result.txt'
function Send-EditorCommand([string]$Value) {
    if(Test-Path -LiteralPath $taskCommand){throw 'An editor command is already pending.'}
    $taskStarted=[DateTime]::UtcNow
    [IO.File]::WriteAllText($taskCommand,$Value,[Text.UTF8Encoding]::new($false))
    do {
        Start-Sleep -Milliseconds 500
        if ((Test-Path -LiteralPath $taskResult) -and (Get-Item -LiteralPath $taskResult).LastWriteTimeUtc -ge $taskStarted) {
            $taskReply=[IO.File]::ReadAllText($taskResult)
            if ($taskReply.StartsWith($Value+' FAILED')) { throw $taskReply }
            if ($taskReply.StartsWith($Value+' OK')) { return }
        }
    } while(([DateTime]::UtcNow-$taskStarted).TotalSeconds -lt $TimeoutSeconds)
    throw ('Editor command timed out: '+$Value)
}
Send-EditorCommand 'refresh'
Send-EditorCommand 'qaplay'
Start-Sleep -Seconds 8
foreach($taskSize in @('390x844','360x640','1080x1920','844x390')) {
    Send-EditorCommand ('size:'+$taskSize)
    Start-Sleep -Seconds 2
    $taskStarted=[DateTime]::UtcNow
    Send-EditorCommand 'qa'
    $taskEvidence=Join-Path $taskRoot ('BuildArtifacts\QA\visual-'+$taskSize+'.txt')
    do {
        Start-Sleep -Seconds 1
        if((Test-Path -LiteralPath $taskEvidence) -and (Get-Item -LiteralPath $taskEvidence).LastWriteTimeUtc -ge $taskStarted) {
            if(![IO.File]::ReadAllText($taskEvidence).Contains('PASS ALL')) { throw ('Visual QA failed: '+$taskSize) }
            Write-Output ('PASS actual Unity UI: '+$taskSize)
            break
        }
        if(([DateTime]::UtcNow-$taskStarted).TotalSeconds -gt $TimeoutSeconds) { throw ('Visual QA timed out; inspect editor.log: '+$taskSize) }
    } while($true)
}
Send-EditorCommand 'stop'
Write-Output 'All UI flows and captures completed; Play mode stopped.'
