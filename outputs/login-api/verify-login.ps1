$ErrorActionPreference = 'Stop'
$apiBase = 'https://booknest-api-3jl3.onrender.com'
$artifactDir = $PSScriptRoot
function CallApi($path, $method = 'GET', $body = $null, $token = $null) {
    $argsForRequest = @{Uri="$apiBase$path"; Method=$method; TimeoutSec=120; ContentType='application/json'}
    if ($null -ne $body) { $argsForRequest.Body = $body | ConvertTo-Json -Depth 20 }
    if ($token) { $argsForRequest.Headers = @{Authorization="Bearer $token"} }
    $response = Invoke-WebRequest @argsForRequest -MaximumRedirection 0
    if ($response.Content) { ConvertFrom-Json $response.Content }
}
function SaveJson($name, $value) {
    ConvertTo-Json -InputObject $value -Depth 30 | Set-Content (Join-Path $artifactDir "$name.json")
}
function Check($condition, $message) { if (!$condition) { throw $message }; Write-Output "PASS $message" }
$demo = Get-Content (Join-Path $artifactDir '../figma-integration/demo-login.private.json') -Raw | ConvertFrom-Json
$login = CallApi '/api/v1/auth/login' 'POST' @{email=$demo.email;password=$demo.password}
Check ($login.tokenType -eq 'Bearer' -and $login.expiresIn -eq 3600 -and $login.refreshToken) 'Login issues access and refresh tokens'
# Tokens are deliberately not saved in response artifacts.
SaveJson 'login-response' @{tokenType=$login.tokenType;accessToken='<access-token>';expiresIn=$login.expiresIn;refreshToken='<refresh-token>'}
$profile = CallApi '/api/v1/me' -token $login.accessToken
Check ($profile.email -eq $demo.email) 'Token resolves to the expected demo user'
SaveJson 'profile-response' $profile
$paths = [ordered]@{
    'library-response'='/api/v1/screens/library?pageSize=100'
    'reading-response'='/api/v1/screens/library?tab=reading'
    'notes-screen-response'='/api/v1/screens/library?tab=notes'
    'collections-response'='/api/v1/screens/collections'
    'counts-response'='/api/v1/me/library/counts'
    'notes-response'='/api/v1/me/annotations/notes'
    'bookmarks-response'='/api/v1/me/annotations/bookmarks'
    'highlights-response'='/api/v1/me/annotations/highlights'
    'statistics-response'='/api/v1/me/reading/statistics?period=week'
    'goal-response'='/api/v1/me/reading/goals/2026'
}
foreach ($entry in $paths.GetEnumerator()) {
    $value = CallApi $entry.Value -token $login.accessToken
    SaveJson $entry.Key $value
    Write-Output "PASS same login token: $($entry.Value)"
}
$library = Get-Content (Join-Path $artifactDir 'library-response.json') -Raw | ConvertFrom-Json
Check ($library.total -ge 31) 'Existing demo data is available after login'
$renewed = CallApi '/api/v1/auth/refresh' 'POST' @{refreshToken=$login.refreshToken}
$renewedProfile = CallApi '/api/v1/me' -token $renewed.accessToken
Check ($renewedProfile.userId -eq $profile.userId) 'Refreshed token keeps the same user and data'
SaveJson 'refresh-response' @{tokenType=$renewed.tokenType;accessToken='<new-access-token>';expiresIn=$renewed.expiresIn;refreshToken='<new-refresh-token>'}
try { CallApi '/api/v1/auth/login' 'POST' @{email=$demo.email;password='Definitely-Wrong-Password-123!'} | Out-Null; throw 'Wrong password accepted' }
catch { Check ([int]$_.Exception.Response.StatusCode -eq 401) 'Wrong password rejected with 401' }
try { CallApi '/api/v1/screens/library' | Out-Null; throw 'Anonymous access accepted' }
catch { Check ([int]$_.Exception.Response.StatusCode -eq 401) 'Personal data requires bearer token' }
try { CallApi '/api/v1/auth/refresh' 'POST' @{refreshToken='invalid-demo-token'} | Out-Null; throw 'Invalid refresh accepted' }
catch { Check ([int]$_.Exception.Response.StatusCode -eq 401) 'Invalid refresh rejected with 401' }
# A separate, reserved-address fixture tests signup and user-data isolation.
$signupPath = Join-Path $artifactDir 'signup-demo.private.json'
if (!(Test-Path $signupPath)) {
    $signup = @{email='login.screen.20261020@example.invalid';password=('Bn!'+[guid]::NewGuid().ToString('N'));displayName='Login Screen Demo'}
    SaveJson 'signup-demo.private' $signup
} else { $signup = Get-Content $signupPath -Raw | ConvertFrom-Json }
try { $signedUp = CallApi '/api/v1/auth/register' 'POST' $signup }
catch {
    if ([int]$_.Exception.Response.StatusCode -ne 409) { throw }
    $signedUp = CallApi '/api/v1/auth/login' 'POST' @{email=$signup.email;password=$signup.password}
}
Check ($signedUp.accessToken -and $signedUp.refreshToken) 'Signup/login issues the same token shape'
$signupProfile = CallApi '/api/v1/me' -token $signedUp.accessToken
Check ($signupProfile.userId -ne $profile.userId) 'Separate account has a separate identity'
$catalog = CallApi '/api/v1/screens/catalog?pageSize=1'
$bookId = $catalog.items[0].book.bookId
CallApi "/api/v1/me/library/$bookId" 'PUT' -token $signedUp.accessToken | Out-Null
CallApi "/api/v1/me/saved/$bookId" 'PUT' -token $signedUp.accessToken | Out-Null
CallApi '/api/v1/me/reading/goals/2026' 'PUT' @{targetBooks=24} $signedUp.accessToken | Out-Null
$ownCollections = CallApi '/api/v1/screens/collections' -token $signedUp.accessToken
if ($ownCollections.total -eq 0) {
    $collection = CallApi '/api/v1/collections' 'POST' @{name='Login demo shelf';description='Created through the signup token for integration testing';visibility='PRIVATE'} $signedUp.accessToken
    $collectionId = $collection.collectionId
} else {
    $collectionId = $ownCollections.items[0].collection.collectionId
}
CallApi "/api/v1/collections/$collectionId/books/$bookId" 'PUT' @{sortOrder=1} $signedUp.accessToken | Out-Null
$ownLibrary = CallApi '/api/v1/screens/library' -token $signedUp.accessToken
$ownNotes = CallApi '/api/v1/me/annotations/notes' -token $signedUp.accessToken
$ownCollections = CallApi '/api/v1/screens/collections' -token $signedUp.accessToken
Check ($ownLibrary.total -eq 1 -and @($ownNotes | Where-Object { $null -ne $_ }).Count -eq 0 -and $ownCollections.total -eq 1) 'Signup user sees only its own sample data, not the original demo account data'
SaveJson 'signup-profile-response' $signupProfile
SaveJson 'signup-library-response' $ownLibrary
SaveJson 'signup-collections-response' $ownCollections
Write-Output 'Login/signup integration complete. No real tokens or passwords were printed.'
