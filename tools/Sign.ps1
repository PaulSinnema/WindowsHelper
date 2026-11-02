# Signs the exe with a self-signed code signing cert (created once in CurrentUser\My)
# and exports the public cert for the MSI, which installs it in LocalMachine\Root (required for uiAccess).
param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string]$CerPath
)
$ErrorActionPreference = 'Stop'
$subject = 'CN=WindowsHelper Code Signing'

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) } |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "Creating code signing certificate $subject"
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy NonExportable `
        -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(10)
}

New-Item -ItemType Directory -Force -Path (Split-Path $CerPath -Parent) | Out-Null
Export-Certificate -Cert $cert -FilePath $CerPath -Force | Out-Null

$result = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -HashAlgorithm SHA256
if (-not $result.SignerCertificate) { throw "Signing failed: $($result.StatusMessage)" }
Write-Host "Signed $FilePath ($($result.Status))"
