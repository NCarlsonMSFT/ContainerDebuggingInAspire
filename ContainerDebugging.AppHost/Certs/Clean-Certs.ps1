# Remove all Root certificates from test-ca
Get-ChildItem -Path Cert:\CurrentUser\Root\ | Where-Object -Property Subject -EQ "O=test-ca" | Remove-Item