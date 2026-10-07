# Applies the schema to an empty test database, then exercises HTTP end-to-end flows.
$ErrorActionPreference = 'Stop'
if(!$env:BOOKNEST_TEST_CONNECTION){throw 'Set BOOKNEST_TEST_CONNECTION to a dedicated EMPTY PostgreSQL database.'}
$root=Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $root 'BookNest.Verification') -- --init-test-db
if($LASTEXITCODE -ne 0){throw 'Test database initialization failed.'}
$secret=[Guid]::NewGuid().ToString('N')
$start=[System.Diagnostics.ProcessStartInfo]::new('dotnet')
$start.ArgumentList.Add((Join-Path $root 'BookNest.Api/bin/Debug/net10.0/BookNest.Api.dll'))
$start.ArgumentList.Add('--urls');$start.ArgumentList.Add('http://127.0.0.1:5188')
$start.WorkingDirectory=Join-Path $root 'BookNest.Api'
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.Environment['ConnectionStrings__BookNest']=$env:BOOKNEST_TEST_CONNECTION
$start.Environment['AdminUserIds__0']='1'
$start.Environment['Payments__WebhookSecret']=$secret
$start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
$start.Environment['Logging__LogLevel__Default']='Warning'
$process=[System.Diagnostics.Process]::Start($start)
$base='http://127.0.0.1:5188/api/v1'
function Send($method,$path,$body=$null,$token=$null,$expected=200,$extra=@{}) {
    $headers=@{};if($token){$headers['Authorization']="Bearer $token"}
    foreach($k in $extra.Keys){$headers[$k]=$extra[$k]}
    $params=@{Uri="$base/$path";Method=$method;Headers=$headers;SkipHttpErrorCheck=$true}
    if($null -ne $body){$params['Body']=($body | ConvertTo-Json -Depth 10 -Compress);$params['ContentType']='application/json'}
    $r=Invoke-WebRequest @params
    if($r.StatusCode -ne $expected){throw "$method $path expected $expected, received $($r.StatusCode): $($r.Content)"}
    if($r.Content){return $r.Content | ConvertFrom-Json}
}
function Payment($event,$resourceId,$reference,$amount) {
    $body=@{event=$event;resourceId=$resourceId;reference=$reference;amount=$amount;currencyCode='JPY'} | ConvertTo-Json -Compress
    $hmac=[System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
    try{$signature=[Convert]::ToHexString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($body)))}finally{$hmac.Dispose()}
    $r=Invoke-WebRequest "$base/payments/callback" -Method Post -ContentType 'application/json' -Body $body -Headers @{'X-BookNest-Signature'=$signature} -SkipHttpErrorCheck
    if($r.StatusCode -ne 200){throw "Callback failed $($r.StatusCode): $($r.Content)"}
}
try {
    $ready=$false
    for($i=0;$i -lt 40;$i++){try{Invoke-WebRequest 'http://127.0.0.1:5188/health' | Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 300}}
    if(!$ready){throw 'API failed to start.'}
    $a=Send POST 'auth/register' @{email='a@example.test';password='TestPassword123!';displayName='A'}
    $b=Send POST 'auth/register' @{email='b@example.test';password='TestPassword123!';displayName='B'}
    $book=Send POST 'admin/books' @{title='Integration book';description='Test';languageCode='en';pageCount=100;purchasePrice=1000;rentalPrice=100;currencyCode='JPY';rentalDays=30;contentStorageKey='test.pdf';fileFormat='PDF';contentVersion=1;premiumEligible=$true;status='PUBLISHED'} $a.accessToken
    Send GET 'admin/books' $null $b.accessToken 403 | Out-Null
    $catalog=Send GET 'books';if($catalog.total -ne 1 -or $catalog.items[0].PSObject.Properties.Name -contains 'contentStorageKey'){throw 'Catalog or secret exclusion failed.'}
    Send GET "books/$($book.bookId)/access" $null $a.accessToken 403 | Out-Null
    $order=Send POST 'me/orders' @{items=@(@{bookId=$book.bookId;accessType='PURCHASE'})} $a.accessToken 201
    Payment ORDER_PAID $order.orderId 'test-order-payment' 1000
    Payment ORDER_PAID $order.orderId 'test-order-payment' 1000
    Send GET "books/$($book.bookId)/access" $null $a.accessToken | Out-Null
    Send GET "me/orders/$($order.orderId)" $null $b.accessToken 404 | Out-Null
    $grants=Send GET 'me/entitlements' $null $a.accessToken;if(@($grants).Count -ne 1){throw 'Duplicate payment created duplicate entitlements.'}
    Send PUT "me/reading/progress/$($book.bookId)" @{currentPage=101;status='READING'} $a.accessToken 400 | Out-Null
    Send PUT "me/reading/progress/$($book.bookId)" @{currentPage=10;status='READING'} $a.accessToken | Out-Null
    $note=Send POST 'me/annotations/notes' @{bookId=$book.bookId;pageNo=10;text='Private note'} $a.accessToken
    Send DELETE "me/annotations/notes/$($note.noteId)" $null $b.accessToken 404 | Out-Null
    $private=Send POST 'collections' @{name='Private';visibility='PRIVATE'} $a.accessToken 201
    Send GET "collections/$($private.collectionId)" $null $b.accessToken 404 | Out-Null
    $shared=Send POST 'collections' @{name='Shared';visibility='SHARED'} $a.accessToken 201
    Send PUT "collections/$($shared.collectionId)/members" @{userId=2;role='VIEWER'} $a.accessToken | Out-Null
    Send GET "collections/$($shared.collectionId)" $null $b.accessToken 404 | Out-Null
    Send PUT "collections/$($shared.collectionId)/membership" @{status='ACCEPTED'} $b.accessToken | Out-Null
    Send GET "collections/$($shared.collectionId)" $null $b.accessToken | Out-Null
    Send PUT "collections/$($shared.collectionId)/books/$($book.bookId)" @{sortOrder=0} $b.accessToken 404 | Out-Null
    $end=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToUnixTimeSeconds();$from=$end-30
    $session=@{clientSessionKey=[Guid]::NewGuid().ToString('N');bookId=$book.bookId;startedAt=[DateTimeOffset]::FromUnixTimeSeconds($from).ToString('yyyy-MM-ddTHH:mm:ssZ');endedAt=[DateTimeOffset]::FromUnixTimeSeconds($end).ToString('yyyy-MM-ddTHH:mm:ssZ');activeSeconds=30;startPage=0;endPage=2;pagesRead=2}
    Send POST 'me/reading/sessions' $session $a.accessToken | Out-Null
    Send POST 'me/reading/sessions' $session $a.accessToken | Out-Null
    $session.clientSessionKey=[Guid]::NewGuid().ToString('N')
    Send POST 'me/reading/sessions' $session $a.accessToken 409 | Out-Null
    Payment ORDER_REFUNDED $order.orderId 'test-order-payment' 1000
    Payment ORDER_REFUNDED $order.orderId 'test-order-payment' 1000
    Send GET "books/$($book.bookId)/access" $null $a.accessToken 403 | Out-Null
    $plan=Send POST 'admin/subscription-plans' @{planCode='TEST';name='Test Plan';price=200;currencyCode='JPY';billingInterval='MONTH';offlineAccess=$false;adFree=$true;isActive=$true} $a.accessToken
    $sub=Send POST 'me/subscriptions' @{planId=$plan.planId} $a.accessToken
    Payment SUBSCRIPTION_PAID $sub.subscriptionId 'test-sub-payment' 200
    Send GET "books/$($book.bookId)/access" $null $a.accessToken | Out-Null
    Send GET "books/$($book.bookId)/access?offline=true" $null $a.accessToken 403 | Out-Null
    Send POST "me/subscriptions/$($sub.subscriptionId)/cancel" $null $a.accessToken | Out-Null
    Send GET "books/$($book.bookId)/access" $null $a.accessToken | Out-Null
    Send DELETE 'me' $null $b.accessToken 204 | Out-Null
    Send GET 'me' $null $b.accessToken 401 | Out-Null
    Write-Output 'PASS database, authentication, catalog, ownership, verified/duplicate payments, refunds, shared collections, session overlap and premium access.'
    Write-Output 'Test records remain in the dedicated test database for inspection.'
} finally {if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
