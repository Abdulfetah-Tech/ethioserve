param([string]$Batch = '')

$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'backend'

function Get-Distance([string]$a, [string]$b) {
    $previous = 0..$b.Length
    for ($i = 1; $i -le $a.Length; $i++) {
        $current = @($i) + (@(0) * $b.Length)
        for ($j = 1; $j -le $b.Length; $j++) {
            $cost = 1
            if ($a[$i - 1] -eq $b[$j - 1]) { $cost = 0 }
            $deletion = $previous[$j] + 1
            $insertion = $current[$j - 1] + 1
            $substitution = $previous[$j - 1] + $cost
            $current[$j] = [Math]::Min([Math]::Min($deletion, $insertion), $substitution)
        }
        $previous = $current
    }
    return $previous[$b.Length]
}

$problems = 0
$typePattern = '(?m)^\s*(?:public|internal|file)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+struct)?|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'

$files = Get-ChildItem $backend -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|Migrations)\\' }

# 1. A file named almost like a type inside it (one-letter slip, wrong capital) is a typo.
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    if (-not $content) { $problems++; Write-Host "EMPTY FILE: $($file.FullName.Substring($backend.Length + 1))" -ForegroundColor Red; continue }

    $base = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $names = @([regex]::Matches($content, $typePattern) | ForEach-Object { $_.Groups[1].Value })

    if ($names -ccontains $base) { continue }

    foreach ($name in $names) {
        if ((Get-Distance $name.ToLowerInvariant() $base.ToLowerInvariant()) -le 2) {
            $problems++
            $relative = $file.FullName.Substring($backend.Length + 1)
            Write-Host "FILE NAME LOOKS WRONG: $relative declares '$name' - rename it to $name.cs" -ForegroundColor Yellow
            break
        }
    }
}

# 2. Files each batch must contain (exact names, including capitals). A batch is only required
#    once at least one of its files exists, or when it is named with -Batch.
$batches = [ordered]@{
    '5.1' = @(
        'EthioServe.Application\Requests\RequestModels.cs',
        'EthioServe.Application\Requests\RequestValidators.cs',
        'EthioServe.Application\Requests\RequestProjections.cs',
        'EthioServe.Application\Requests\CustomerRequestService.cs',
        'EthioServe.Application\Requests\ProviderFeedService.cs',
        'EthioServe.API\Controllers\RequestsController.cs',
        'EthioServe.API\Controllers\ProviderFeedController.cs',
        'EthioServe.Tests\Unit\ServiceRequestDomainTests.cs',
        'EthioServe.Tests\Unit\RequestValidatorTests.cs',
        'EthioServe.Tests\Support\RequestScenario.cs',
        'EthioServe.Tests\Integration\RequestApiTests.cs',
        'EthioServe.Tests\Integration\ProviderFeedApiTests.cs'
    )
}

$batches['5.3'] = @(
    'EthioServe.Application\Abstractions\IFileStorage.cs',
    'EthioServe.Application\Attachments\ImageSniffer.cs',
    'EthioServe.Application\Attachments\AttachmentRules.cs',
    'EthioServe.Application\Attachments\AttachmentModels.cs',
    'EthioServe.Application\Attachments\RequestAttachmentService.cs',
    'EthioServe.Infrastructure\Storage\LocalFileStorage.cs',
    'EthioServe.API\Controllers\AttachmentsController.cs',
    'EthioServe.Tests\Unit\ImageSnifferTests.cs',
    'EthioServe.Tests\Unit\AttachmentRulesTests.cs',
    'EthioServe.Tests\Integration\LocalFileStorageTests.cs',
    'EthioServe.Tests\Support\AttachmentScenario.cs',
    'EthioServe.Tests\Integration\AttachmentApiTests.cs'
)

$batches['6.1'] = @(
    'EthioServe.Application\Quotes\QuoteModels.cs',
    'EthioServe.Application\Quotes\QuoteStatusRules.cs',
    'EthioServe.Application\Quotes\QuoteValidators.cs',
    'EthioServe.Application\Quotes\ProviderQuoteService.cs',
    'EthioServe.Application\Quotes\CustomerQuoteService.cs',
    'EthioServe.API\Controllers\ProviderQuotesController.cs',
    'EthioServe.API\Controllers\CustomerQuotesController.cs',
    'EthioServe.Tests\Unit\QuoteDomainTests.cs',
    'EthioServe.Tests\Unit\ServiceRequestQuoteTests.cs',
    'EthioServe.Tests\Unit\QuoteStatusRulesTests.cs',
    'EthioServe.Tests\Unit\QuoteValidatorTests.cs',
    'EthioServe.Tests\Support\QuoteScenario.cs',
    'EthioServe.Tests\Integration\QuoteApiTests.cs',
    'EthioServe.Tests\Integration\QuoteConcurrencyTests.cs'
)

$batches['6.2'] = @(
    'EthioServe.Tests\Integration\QuoteFilterApiTests.cs'
)

$batches['7.1'] = @(
    'EthioServe.Application\Appointments\AppointmentRules.cs',
    'EthioServe.Application\Appointments\AppointmentModels.cs',
    'EthioServe.Application\Appointments\AppointmentValidators.cs',
    'EthioServe.Application\Appointments\AppointmentProjections.cs',
    'EthioServe.Application\Appointments\ProviderAppointmentService.cs',
    'EthioServe.Application\Appointments\CustomerAppointmentService.cs',
    'EthioServe.API\Controllers\ProviderAppointmentsController.cs',
    'EthioServe.API\Controllers\CustomerAppointmentsController.cs',
    'EthioServe.Tests\Unit\AppointmentDomainTests.cs',
    'EthioServe.Tests\Unit\ServiceRequestAppointmentTests.cs',
    'EthioServe.Tests\Unit\AppointmentRulesTests.cs',
    'EthioServe.Tests\Unit\AppointmentValidatorTests.cs',
    'EthioServe.Tests\Support\AppointmentScenario.cs',
    'EthioServe.Tests\Integration\AppointmentApiTests.cs',
    'EthioServe.Tests\Integration\AppointmentConcurrencyTests.cs'
)

$batches['7.3'] = @(
    'EthioServe.Application\Notifications\NotificationText.cs',
    'EthioServe.Application\Notifications\INotifier.cs',
    'EthioServe.Application\Notifications\Notifier.cs',
    'EthioServe.Application\Notifications\NotificationModels.cs',
    'EthioServe.Application\Notifications\NotificationService.cs',
    'EthioServe.API\Controllers\NotificationsController.cs',
    'EthioServe.Tests\Unit\NotificationDomainTests.cs',
    'EthioServe.Tests\Unit\NotificationTextTests.cs',
    'EthioServe.Tests\Support\NotificationScenario.cs',
    'EthioServe.Tests\Integration\NotificationApiTests.cs'
)

$batches['7.4'] = @(
    'EthioServe.Application\Notifications\INotificationPublisher.cs',
    'EthioServe.API\Realtime\NotificationHub.cs',
    'EthioServe.API\Realtime\SubUserIdProvider.cs',
    'EthioServe.API\Realtime\SignalRNotificationPublisher.cs',
    'EthioServe.API\Realtime\RealtimeExtensions.cs',
    'EthioServe.API\Realtime\RealtimeAuthentication.cs',
    'EthioServe.Tests\Integration\RealtimeTests.cs'
)

$batches['7.4.1'] = @(
    'EthioServe.Tests\Integration\ExclusiveLockTests.cs'
)

$batches['8.1'] = @(
    'EthioServe.Application\Payments\PaymentRules.cs',
    'EthioServe.Application\Payments\PaymentModels.cs',
    'EthioServe.Application\Payments\PaymentGateways.cs',
    'EthioServe.Application\Payments\CustomerPaymentService.cs',
    'EthioServe.Application\Payments\ProviderPaymentService.cs',
    'EthioServe.API\Controllers\CustomerPaymentsController.cs',
    'EthioServe.API\Controllers\ProviderPaymentsController.cs',
    'EthioServe.Tests\Unit\PaymentDomainTests.cs',
    'EthioServe.Tests\Unit\PaymentRulesTests.cs',
    'EthioServe.Tests\Support\PaymentScenario.cs',
    'EthioServe.Tests\Integration\PaymentApiTests.cs',
    'EthioServe.Tests\Integration\PaymentConcurrencyTests.cs'
)

$batches['9.1'] = @(
    'EthioServe.Application\Reviews\ReviewModels.cs',
    'EthioServe.Application\Reviews\ReviewService.cs',
    'EthioServe.API\Controllers\AdminReviewsController.cs',
    'EthioServe.Tests\Integration\ReviewApiTests.cs'
)

$batches['9.2'] = @(
    'EthioServe.Infrastructure\Identity\EmailOptions.cs',
    'EthioServe.Infrastructure\Identity\SesEmailService.cs',
    'EthioServe.Tests\Unit\SesEmailServiceTests.cs',
    'EthioServe.Tests\Unit\EmailProviderRegistrationTests.cs'
)

$checked = @()
foreach ($name in $batches.Keys) {
    $paths = $batches[$name]
    $present = @($paths | Where-Object { Test-Path (Join-Path $backend $_) })

    if ($present.Count -eq 0 -and $Batch -ne $name) { continue }

    $checked += $name
    foreach ($path in $paths) {
        $folder = Join-Path $backend (Split-Path $path)
        $leaf = Split-Path $path -Leaf
        $actual = @()
        if (Test-Path $folder) { $actual = @(Get-ChildItem $folder -File -Name) }

        if ($actual -cnotcontains $leaf) {
            $problems++
            Write-Host "MISSING (not created yet, or misspelled): backend\$path" -ForegroundColor Red
        }
    }
}

if ($problems -eq 0) {
    $scope = if ($checked.Count -gt 0) { "batch $($checked -join ', ') complete" } else { 'no batch in progress' }
    Write-Host "Backend file names look right ($($files.Count) files checked, $scope)." -ForegroundColor Green
    exit 0
}

exit 1
