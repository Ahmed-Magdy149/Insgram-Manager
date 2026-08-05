# Test Cookie Parsing Logic
# This simulates the C# parsing code

$cookieHeader = "csrftoken=ErDOlT3DYi4UZW9sRlLjowMxkSIGdL; ds_user_id=1611158923; sessionid=1611158923%3AAcLfNThIBrNzOk%3A1A1Yk3AAtYYxRc"

Write-Host "Testing Cookie Header Parsing" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Input Cookie Header:" -ForegroundColor Yellow
Write-Host $cookieHeader
Write-Host ""

$cookies = @{}
$parts = $cookieHeader -split ';'

foreach ($part in $parts) {
    $part = $part.Trim()
    $separatorIndex = $part.IndexOf('=')
    
    if ($separatorIndex -gt 0) {
        $name = $part.Substring(0, $separatorIndex).Trim()
        $value = $part.Substring($separatorIndex + 1).Trim()
        
        if ($name -and -not $cookies.ContainsKey($name)) {
            $cookies[$name] = $value
        }
    }
}

Write-Host "Parsed Cookies:" -ForegroundColor Green
Write-Host "===============" -ForegroundColor Green
foreach ($key in $cookies.Keys) {
    Write-Host "  $key = $($cookies[$key])"
}

Write-Host ""
Write-Host "Required Cookies Check:" -ForegroundColor Magenta
Write-Host "=======================" -ForegroundColor Magenta

$requiredCookies = @('sessionid', 'csrftoken', 'ds_user_id')
foreach ($required in $requiredCookies) {
    if ($cookies.ContainsKey($required)) {
        Write-Host "  ✅ $required : FOUND" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $required : MISSING" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Built Cookie Header:" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan

$builtParts = @()
if ($cookies.ContainsKey('sessionid')) {
    $builtParts += "sessionid=$($cookies['sessionid'])"
}
if ($cookies.ContainsKey('csrftoken')) {
    $builtParts += "csrftoken=$($cookies['csrftoken'])"
}
if ($cookies.ContainsKey('ds_user_id')) {
    $builtParts += "ds_user_id=$($cookies['ds_user_id'])"
}

$builtHeader = $builtParts -join '; '
Write-Host $builtHeader

Write-Host ""
Write-Host "Test Result: " -NoNewline
if ($cookies.ContainsKey('sessionid') -and $cookies.ContainsKey('csrftoken') -and $cookies.ContainsKey('ds_user_id')) {
    Write-Host "✅ PASS - All required cookies found" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL - Missing required cookies" -ForegroundColor Red
}
