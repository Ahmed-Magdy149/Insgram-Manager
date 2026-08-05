# Full Session Test with Complete Cookies

Write-Host "Instagram Session Creation Test" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Complete cookies from screenshot
$csrftoken = "ErDOlT3DYi4UZW9sRlLjowMxkSIGdL"
$ds_user_id = "1611158923"
$sessionid = "1611158923%3AAcLfNThIBrNzOk%3A1A1Yk3AAtYYxRcz1XoRfTLD38BSjrWkpXc5YDr6r9axR4Gaw"

Write-Host "Extracted Cookies:" -ForegroundColor Yellow
Write-Host "  csrftoken  : $csrftoken"
Write-Host "  ds_user_id : $ds_user_id"
Write-Host "  sessionid  : $sessionid"
Write-Host ""

# Build cookie header (same format as C# code)
$cookieHeader = "sessionid=$sessionid; csrftoken=$csrftoken; ds_user_id=$ds_user_id"

Write-Host "Built Cookie Header:" -ForegroundColor Green
Write-Host "====================" -ForegroundColor Green
Write-Host $cookieHeader
Write-Host ""

Write-Host "Cookie Header Length: $($cookieHeader.Length) characters" -ForegroundColor Magenta
Write-Host ""

# Validation checks
Write-Host "Validation Checks:" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan

$checks = @{
    "sessionid length > 50" = $sessionid.Length -gt 50
    "csrftoken length > 20" = $csrftoken.Length -gt 20
    "ds_user_id is numeric" = $ds_user_id -match '^\d+$'
    "sessionid starts with ds_user_id" = $sessionid.StartsWith($ds_user_id)
}

$allPassed = $true
foreach ($check in $checks.GetEnumerator()) {
    if ($check.Value) {
        Write-Host "  ✅ $($check.Key)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $($check.Key)" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host ""
Write-Host "Overall Result: " -NoNewline
if ($allPassed) {
    Write-Host "✅ READY FOR SESSION CREATION" -ForegroundColor Green
} else {
    Write-Host "❌ VALIDATION FAILED" -ForegroundColor Red
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "==========" -ForegroundColor Yellow
Write-Host "1. Copy the cookie header above"
Write-Host "2. Open the application in browser"
Write-Host "3. Go to Data Explorer → Create Session"
Write-Host "4. Paste the cookie header in 'Raw Cookie Header' field"
Write-Host "5. Click 'Parse Cookies'"
Write-Host "6. Enter username (if not auto-filled)"
Write-Host "7. Click 'Save Session'"
Write-Host ""
Write-Host "Cookie Header (for copy-paste):" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host $cookieHeader
