param([string]$UpTo = '')

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'frontend\ethioserve\src\app'

$batches = [ordered]@{
    '4.4a' = @(
        'core\models\marketplace.models.ts', 'core\api\load-state.ts', 'core\api\load-state.spec.ts',
        'core\api\marketplace.api.ts', 'core\api\marketplace.api.spec.ts',
        'core\api\review.api.ts',
        'features\marketplace\home.ts', 'features\marketplace\provider-card.ts', 'features\marketplace\providers.ts',
        'features\marketplace\provider-detail.ts', 'features\marketplace\search-params.ts',
        'features\marketplace\search-params.spec.ts',
        'shared\etb.pipe.ts', 'shared\etb.pipe.spec.ts', 'shared\pager.ts', 'shared\pager.spec.ts',
        'shared\rating.ts', 'shared\rating.spec.ts'
    )
    '4.4b' = @(
        'core\models\provider.models.ts', 'core\api\provider.api.ts', 'core\api\provider.api.spec.ts',
        'features\auth\register-provider.ts', 'features\provider\provider-nav.ts',
        'features\provider\provider-dashboard.ts', 'features\provider\provider-profile.ts',
        'features\provider\provider-services.ts', 'features\provider\verification.ts',
        'features\provider\verification.spec.ts'
    )
    '4.5a' = @(
        'core\models\admin.models.ts', 'core\api\admin.api.ts', 'core\api\admin.api.spec.ts',
        'features\admin\admin-nav.ts', 'features\admin\admin-dashboard.ts', 'features\admin\admin-providers.ts',
        'features\admin\admin-provider-detail.ts', 'features\admin\admin-provider-params.ts',
        'features\admin\admin-provider-params.spec.ts', 'features\admin\provider-actions.ts',
        'features\admin\provider-actions.spec.ts'
    )
    '4.5b' = @(
        'core\models\admin-catalog.models.ts', 'core\api\admin-catalog.api.ts', 'core\api\admin-catalog.api.spec.ts',
        'features\admin\pricing.ts', 'features\admin\pricing.spec.ts',
        'features\admin\admin-service-params.ts', 'features\admin\admin-service-params.spec.ts',
        'features\admin\admin-categories.ts', 'features\admin\admin-services.ts'
    )
    '5.2' = @(
        'core\models\request.models.ts', 'core\api\request.api.ts', 'core\api\request.api.spec.ts',
        'features\customer\customer-nav.ts', 'features\customer\request-status.ts', 'features\customer\request-status.spec.ts',
        'features\customer\request-list-params.ts', 'features\customer\request-list-params.spec.ts',
        'features\customer\request-form.ts', 'features\customer\request-form.spec.ts',
        'features\customer\request-editor.ts', 'features\customer\my-requests.ts', 'features\customer\request-detail.ts',
        'features\provider\feed-params.ts', 'features\provider\feed-params.spec.ts',
        'features\provider\provider-feed.ts', 'features\provider\feed-detail.ts'
    )
}
$batches['5.3'] = @(
    'core\models\attachment.models.ts', 'core\api\attachment.api.ts', 'core\api\attachment.api.spec.ts',
    'shared\attachment-rules.ts', 'shared\attachment-rules.spec.ts', 'shared\attachment-image.ts',
    'shared\request-photos.ts', 'shared\request-photos.spec.ts'
)
$batches['6.2'] = @(
    'core\models\quote.models.ts', 'core\api\quote.api.ts', 'core\api\quote.api.spec.ts',
    'shared\quote-format.ts', 'shared\quote-format.spec.ts',
    'features\provider\quote-form.ts', 'features\provider\quote-form.spec.ts',
    'features\provider\provider-quote-view.ts', 'features\provider\provider-quote-box.ts',
    'features\provider\my-quotes.ts', 'features\provider\provider-quote-detail.ts',
    'features\customer\request-quotes.ts', 'features\customer\request-quotes.spec.ts'
)
$batches['7.2'] = @(
    'core\models\appointment.models.ts', 'core\api\appointment.api.ts', 'core\api\appointment.api.spec.ts',
    'shared\appointment-format.ts', 'shared\appointment-format.spec.ts',
    'shared\appointment-list-params.ts', 'shared\appointment-list-params.spec.ts',
    'features\provider\appointment-scheduler.ts',
    'features\provider\provider-appointment-view.ts', 'features\provider\provider-appointment-view.spec.ts',
    'features\provider\provider-job.ts', 'features\provider\provider-appointments.ts',
    'features\provider\provider-appointment-detail.ts',
    'features\customer\customer-appointment-box.ts', 'features\customer\customer-appointment-box.spec.ts',
    'features\customer\my-appointments.ts'
)
$batches['7.4'] = @(
    'core\models\notification.models.ts', 'core\api\notification.api.ts', 'core\api\notification.api.spec.ts',
    'core\api\live-connection.ts', 'core\testing\fake-connection.ts',
    'core\notifications\notification.service.ts', 'core\notifications\notification.service.spec.ts',
    'shared\notification-format.ts', 'shared\notification-format.spec.ts',
    'shared\notification-links.ts', 'shared\notification-links.spec.ts',
    'shared\notification-list-params.ts', 'shared\notification-list-params.spec.ts',
    'shared\notification-bell.ts', 'shared\notification-bell.spec.ts',
    'features\notifications\notifications-page.ts'
)
$batches['8.1'] = @(
    'core\models\payment.models.ts', 'core\api\payment.api.ts', 'core\api\payment.api.spec.ts',
    'shared\payment-format.ts', 'shared\payment-format.spec.ts',
    'features\customer\customer-payment-box.ts', 'features\customer\customer-payments.ts',
    'features\customer\customer-payment-box.spec.ts',
    'features\customer\payment-checkout.ts', 'features\customer\payment-checkout.spec.ts',
    'features\provider\provider-payments.ts', 'features\provider\provider-payments.spec.ts'
)
$batches['8.2'] = @(
    'core\models\message.models.ts', 'core\api\message.api.ts', 'core\api\message.api.spec.ts',
    'shared\request-messages.ts', 'shared\request-messages.spec.ts'
)
$batches['8.3'] = @(
    'features\provider\provider-availability.ts', 'features\provider\provider-availability.spec.ts'
)
$batches['8.4'] = @(
    'features\admin\admin-reviews.ts', 'features\admin\admin-reviews.spec.ts'
)
$names = @($batches.Keys)

# Without -UpTo, the newest batch that has at least one file is the one being worked on.
if (-not $UpTo) {
    $UpTo = $names[0]
    foreach ($name in $names) {
        if (@($batches[$name] | Where-Object { Test-Path (Join-Path $app $_) }).Count -gt 0) { $UpTo = $name }
    }
}
if ($names -notcontains $UpTo) {
    Write-Host "Unknown batch '$UpTo'. Use one of: $($names -join ', ')" -ForegroundColor Red
    exit 2
}

$expected = @()
foreach ($name in $names) {
    $expected += $batches[$name]
    if ($name -eq $UpTo) { break }
}

$older = @(
    'core\models\auth.models.ts', 'core\api\api-error.ts', 'core\api\api-error.spec.ts',
    'core\api\apply-server-errors.ts', 'features\auth\login.ts', 'features\auth\register.ts',
    'features\auth\forgot-password.ts', 'features\auth\forgot-password.spec.ts',
    'features\auth\reset-password.ts', 'features\customer\customer-dashboard.ts'
)
$known = $expected + $older
$folders = 'core\models\', 'core\api\', 'features\marketplace\', 'features\provider\', 'features\auth\', 'features\admin\', 'features\customer\', 'shared\'

$actual = Get-ChildItem $app -Recurse -File -Filter *.ts | ForEach-Object { $_.FullName.Substring($app.Length + 1) }
$missing = @($expected | Where-Object { $actual -cnotcontains $_ })
$unexpected = @($actual | Where-Object {
    $path = $_
    (@($folders | Where-Object { $path.StartsWith($_) })).Count -gt 0 -and $known -cnotcontains $path
})
$empty = @($expected | Where-Object { ($actual -ccontains $_) -and ((Get-Item (Join-Path $app $_)).Length -eq 0) })

$scriptNames = 'verify.ps1', 'sync-db-port.ps1', 'check-files.ps1', 'approve-provider.ps1', 'check-backend-files.ps1', 'show-notifications.ps1'
$scriptsActual = Get-ChildItem (Join-Path $root 'Scripts') -File -Name
$scriptsMissing = @($scriptNames | Where-Object { $scriptsActual -cnotcontains $_ })
$scriptsUnexpected = @($scriptsActual | Where-Object { $scriptNames -cnotcontains $_ })

$problems = 0

foreach ($file in $missing) {
    $problems++
    Write-Host "MISSING (not created yet, or misspelled): $file" -ForegroundColor Red
    $folder = Split-Path $file
    foreach ($hint in ($unexpected | Where-Object { (Split-Path $_) -eq $folder })) {
        Write-Host "   you may have created: $hint" -ForegroundColor Yellow
    }
}
foreach ($file in $empty) {
    $problems++
    Write-Host "EMPTY FILE (no code pasted yet): $file" -ForegroundColor Red
}
foreach ($file in $unexpected) {
    $problems++
    Write-Host "NOT IN THE PLAN (typo, or leftover): $file" -ForegroundColor Yellow
}
foreach ($file in $scriptsMissing) {
    $problems++
    Write-Host "MISSING script: Scripts\$file" -ForegroundColor Red
}
foreach ($file in $scriptsUnexpected) {
    $problems++
    Write-Host "UNEXPECTED script (typo?): Scripts\$file" -ForegroundColor Yellow
}

if ($problems -eq 0) {
    Write-Host "All $($expected.Count) frontend files (up to batch $UpTo) and $($scriptNames.Count) scripts are present and correctly named." -ForegroundColor Green
    exit 0
}

exit 1
