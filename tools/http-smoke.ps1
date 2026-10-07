$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$start.ArgumentList.Add((Join-Path $root 'BookNest.Api/bin/Debug/net10.0/BookNest.Api.dll'))
$start.ArgumentList.Add('--urls')
$start.ArgumentList.Add('http://127.0.0.1:5187')
$start.WorkingDirectory = Join-Path $root 'BookNest.Api'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
$start.Environment['Logging__LogLevel__Default'] = 'Warning'
$process = [System.Diagnostics.Process]::Start($start)
try {
    $base = 'http://127.0.0.1:5187'
    $ready = $false
    for ($i=0; $i -lt 30; $i++) {
        try { $response=Invoke-WebRequest "$base/health"; if($response.StatusCode -eq 200){$ready=$true;break} } catch { Start-Sleep -Milliseconds 300 }
    }
    if(!$ready){throw 'API did not become ready.'}
    $doc=Invoke-RestMethod "$base/openapi/v1.json"
    $operations=@($doc.paths.PSObject.Properties.Value | ForEach-Object { $_.PSObject.Properties } | Where-Object Name -In get,post,put,delete).Count
    if($operations -lt 60){throw "Unexpected OpenAPI operation count: $operations"}
    Write-Output "PASS health and OpenAPI ($operations operations)"
    foreach($path in @('/api/v1/me','/api/v1/admin/books','/api/v1/me/annotations/notes','/api/v1/me/orders')) {
        $r=Invoke-WebRequest "$base$path" -SkipHttpErrorCheck
        if($r.StatusCode -ne 401){throw "Expected 401: $path, received $($r.StatusCode)"}
        Write-Output "PASS unauthenticated request blocked: $path"
    }
    $r=Invoke-WebRequest "$base/api/v1/auth/register" -Method Post -ContentType 'application/json' -Body '{"email":"bad","password":"short","displayName":""}' -SkipHttpErrorCheck
    if($r.StatusCode -ne 400){throw "Expected invalid registration 400, received $($r.StatusCode)"}
    Write-Output 'PASS invalid registration rejected before database access'
} finally {
    if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
    $process.Dispose()
}
