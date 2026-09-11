param(
    [Parameter(Mandatory=$true)][string]$SupportEmail,
    [Parameter(Mandatory=$true)][uri]$PrivacyPolicyUrl
)
$ErrorActionPreference='Stop'
$taskAddress=[System.Net.Mail.MailAddress]::new($SupportEmail)
if ($taskAddress.Address -ne $SupportEmail -or $SupportEmail -match '[<>\s]') { throw 'Enter a plain support email address.' }
if ($PrivacyPolicyUrl.Scheme -ne 'https') { throw 'Use a public HTTPS privacy policy URL.' }
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskListingPath=Join-Path $taskRoot 'Store\listing.json'
$taskListing=Get-Content -LiteralPath $taskListingPath -Raw | ConvertFrom-Json
$taskListing.supportEmail=$SupportEmail
$taskListing.privacyPolicyUrl=$PrivacyPolicyUrl.AbsoluteUri
$taskUtf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($taskListingPath,($taskListing | ConvertTo-Json -Depth 8),$taskUtf8)
[IO.File]::WriteAllText((Join-Path $taskRoot 'Assets\OpenedNote\Resources\OpenedNote\Contact.txt'),$SupportEmail,$taskUtf8)
$taskPolicyPath=Join-Path $taskRoot 'Store\privacy-policy.html'
$taskPolicy=[IO.File]::ReadAllText($taskPolicyPath)
$taskHtmlEmail=[System.Net.WebUtility]::HtmlEncode($SupportEmail)
$taskContact='<p id="contact">문의: <a href="mailto:'+$taskHtmlEmail+'">'+$taskHtmlEmail+'</a> · SecondWindGames</p>'
$taskPolicy=[regex]::Replace($taskPolicy,'<p id="contact">.*?</p>',[System.Text.RegularExpressions.MatchEvaluator]{param($m) $taskContact})
[IO.File]::WriteAllText($taskPolicyPath,$taskPolicy,$taskUtf8)
Write-Output 'Contact values saved. Publish Store/privacy-policy.html to the supplied URL and rebuild Android to include the in-app contact.'
