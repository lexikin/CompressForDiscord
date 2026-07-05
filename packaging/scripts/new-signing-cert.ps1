#Requires -Version 7
<#
.SYNOPSIS
  ONE-TIME runbook: generates the self-signed code-signing certificate for the Win11 sparse
  package, exports the private .pfx (keep OFFLINE, never commit) and the public .cer
  (committed to packaging/windows/certs/), and prints the follow-up steps.

.NOTES
  The certificate Subject MUST byte-match the AppxManifest Publisher: CN=CompressForDiscordOSS.
  TrustedPeople scope = MSIX sideload trust only — not a root CA; bounded blast radius.
  All release signatures are timestamped, so packages outlive the cert's own validity.
#>
[CmdletBinding()]
param(
    [string]$PfxPath = 'CompressForDiscordOSS.pfx'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$subject = 'CN=CompressForDiscordOSS'
$cerPath = Join-Path $PSScriptRoot '..\windows\certs\CompressForDiscordOSS.cer'

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
    -KeyUsage DigitalSignature -FriendlyName 'CompressForDiscord OSS signing' `
    -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(10)

$password = Read-Host -AsSecureString 'Choose a PFX password'
Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $password | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $cerPath -Parent) | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host ''
Write-Host "PFX (PRIVATE — keep offline):  $PfxPath"
Write-Host "CER (public — commit this):    $cerPath"
Write-Host "Thumbprint (put in release.yml CERT_THUMBPRINT / MSI CertThumbprint):"
Write-Host "  $($cert.Thumbprint)"
Write-Host ''
Write-Host 'Now set the GitHub secrets:'
Write-Host '  gh secret set SIGNING_PFX_BASE64 --body "$([Convert]::ToBase64String([IO.File]::ReadAllBytes(''' + $PfxPath + ''')))"'
Write-Host '  gh secret set SIGNING_PFX_PASSWORD'
Write-Host '  gh variable set CERT_THUMBPRINT --body "' + $cert.Thumbprint + '"'
Write-Host ''
Write-Host 'Optional: remove the private key from this machine''s store afterwards:'
Write-Host "  Remove-Item Cert:\CurrentUser\My\$($cert.Thumbprint)"
