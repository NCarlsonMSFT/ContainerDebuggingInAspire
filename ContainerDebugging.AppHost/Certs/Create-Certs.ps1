[string]$Password = [Guid]::NewGuid().ToString("N")
Set-Content -Path "${PSScriptRoot}\Password.txt" -Value $Password -NoNewline

docker run --rm -u 1654 --entrypoint="/bin/bash" -v "${PSScriptRoot}:/Certs" -w="/Certs" mcr.microsoft.com/dotnet/aspnet:8.0 "/Certs/CreateCerts.sh"

$testCaCert = New-Object -TypeName "System.Security.Cryptography.X509Certificates.X509Certificate2" @("${PSScriptRoot}\test-ca.crt", $null)

$storeName = [System.Security.Cryptography.X509Certificates.StoreName]::Root;
$storeLocation = [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, $storeLocation)
$store.Open(([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite))
try
{
    $store.Add($testCaCert)
}
finally
{
    $store.Close()
    $store.Dispose()
}
