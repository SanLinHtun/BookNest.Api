$ErrorActionPreference='Stop'
$root='C:/SLH/BookNest/BookNest.Api'
$start=[System.Diagnostics.ProcessStartInfo]::new('dotnet')
$start.ArgumentList.Add((Join-Path $root 'BookNest.Api/bin/Debug/net10.0/BookNest.Api.dll'))
$start.ArgumentList.Add('--urls');$start.ArgumentList.Add('http://127.0.0.1:5187')
$start.WorkingDirectory=Join-Path $root 'BookNest.Api'
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
$start.Environment['Logging__LogLevel__Default']='Warning'
$process=[System.Diagnostics.Process]::Start($start)
try{
  $ok=$false
  for($i=0;$i -lt 30;$i++){try{
    $doc=Invoke-WebRequest 'http://127.0.0.1:5187/openapi/v1.json'
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'openapi.json'),$doc.Content);$ok=$true;break
  }catch{Start-Sleep -Milliseconds 300}}
  if(!$ok){throw 'Could not retrieve OpenAPI.'}
  'Exported running API OpenAPI document.'
}finally{if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
