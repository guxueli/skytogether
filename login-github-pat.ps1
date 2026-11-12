$env:HTTP_PROXY = 'http://127.0.0.1:8900'
$env:HTTPS_PROXY = 'http://127.0.0.1:8900'
$gh = 'C:\Program Files\GitHub CLI\gh.exe'
Write-Host 'ScreenInk GitHub PAT login'
Write-Host 'Create a classic token with repo, read:org, gist at https://github.com/settings/tokens'
$secure = Read-Host 'Paste token (input hidden)' -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
  $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  $token | & $gh auth login --hostname github.com --with-token
  Remove-Variable token -ErrorAction SilentlyContinue
  & $gh auth status
} finally {
  [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}
Write-Host ''
Read-Host 'Press Enter to close'
