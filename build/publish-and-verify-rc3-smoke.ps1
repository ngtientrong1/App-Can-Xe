# Phase 6 rc3: clean publish + binary/UI smoke checks (no stress).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Assert-True($cond, $msg) {
    if (-not $cond) { throw "SMOKE FAIL: $msg" }
    Write-Host "SMOKE OK: $msg"
}

Write-Host "=== Clean old publish ==="
if (Test-Path "publish\win10-x64") {
    Remove-Item "publish\win10-x64" -Recurse -Force
}

Write-Host "=== Release build ==="
dotnet build CanXe.sln -c Release /nr:false /m:1 -v q
if ($LASTEXITCODE -ne 0) { throw "Release build failed" }

Write-Host "=== Short Phase6 tests ==="
dotnet test tests\CanXe.Desktop.Tests\CanXe.Desktop.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Phase6Rc" -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Phase6Rc desktop tests failed" }
dotnet test tests\CanXe.Tests\CanXe.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Phase6Rc1|FullyQualifiedName~SafeLogFileAppend" -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Phase6 application tests failed" }

Write-Host "=== Fresh publish ==="
cmd /c "build\publish-win10-x64.cmd"
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$exe = Join-Path $root "publish\win10-x64\CanXe.Desktop.exe"
$dll = Join-Path $root "publish\win10-x64\CanXe.Desktop.dll"
Assert-True (Test-Path $exe) "publish CanXe.Desktop.exe exists"
Assert-True (Test-Path $dll) "publish CanXe.Desktop.dll exists"

$vi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
Write-Host "ProductVersion=$($vi.ProductVersion)"
Assert-True ($vi.ProductVersion -like "*0.6.0-rc3*") "ProductVersion contains 0.6.0-rc3 (got $($vi.ProductVersion))"

$bytes = [System.IO.File]::ReadAllBytes($dll)
$all = [System.Text.Encoding]::UTF8.GetString($bytes)
Assert-True ($all.Contains("IsWeight1InlineEditing")) "DLL contains IsWeight1InlineEditing"
Assert-True ($all.Contains("AdminInlineWeightHint")) "DLL contains AdminInlineWeightHint"
Assert-True ($all.Contains("Weight1Value_OnMouseLeftButtonDown")) "DLL contains Weight1 click handler"
Assert-True ($all.Contains("Admin: nhấp vào số cân")) "DLL contains Admin inline hint text"
Assert-True (-not (Select-String -Path "src\CanXe.Desktop\MainWindow.xaml" -Pattern "NHẬP CÂN TAY" -Quiet)) "MainWindow.xaml has no NHẬP CÂN TAY"

Write-Host "=== Launch app briefly for automation smoke ==="
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.WorkingDirectory = (Split-Path $exe)
$psi.UseShellExecute = $true
$p = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 8

$rootEl = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $null
for ($i = 0; $i -lt 20 -and $null -eq $win; $i++) {
    Start-Sleep -Milliseconds 500
    $win = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}
Assert-True ($null -ne $win) "App window found for PID $($p.Id)"

function Find-Names($start) {
    $foundManualBtn = $false
    $foundUnlock = $false
    $foundHint = $false
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $stack = New-Object System.Collections.Generic.Stack[System.Windows.Automation.AutomationElement]
    $stack.Push($start)
    while ($stack.Count -gt 0) {
        $el = $stack.Pop()
        $name = $el.Current.Name
        if ($name -eq "NHẬP CÂN TAY") { $foundManualBtn = $true }
        if ($name -like "*MỞ KHÓA ADMIN*") { $foundUnlock = $true }
        if ($name -like "*nhấp vào số cân*") { $foundHint = $true }
        try {
            $child = $walker.GetFirstChild($el)
            while ($null -ne $child) {
                $stack.Push($child)
                $child = $walker.GetNextSibling($child)
            }
        } catch {}
    }
    return @{ Manual = $foundManualBtn; Unlock = $foundUnlock; Hint = $foundHint }
}

$scan = Find-Names $win
Assert-True (-not $scan.Manual) "No NHẬP CÂN TAY button in live UI"
Assert-True $scan.Unlock "MỞ KHÓA ADMIN visible when admin locked"

$btnCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "MỞ KHÓA ADMIN")))
$unlockBtn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if ($null -ne $unlockBtn) {
    $inv = $unlockBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $inv.Invoke()
    Start-Sleep -Seconds 1
    $dlg = $null
    for ($i = 0; $i -lt 12; $i++) {
        $wins = $rootEl.FindAll([System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)))
        foreach ($w in $wins) {
            $n = $w.Current.Name
            if ($n -like "*ADMIN*" -or $n -like "*Mở khóa*" -or $n -like "*Admin*") { $dlg = $w; break }
        }
        if ($dlg) { break }
        Start-Sleep -Milliseconds 400
    }
    if ($dlg) {
        $edit = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
        if ($edit) {
            $edit.SetFocus()
            [System.Windows.Forms.SendKeys]::SendWait("admin123")
            Start-Sleep -Milliseconds 300
            [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
            Start-Sleep -Seconds 2
        }
    }
}

# Reacquire main window and scan for hint
$win = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
$scan2 = Find-Names $win
Assert-True $scan2.Hint "Admin inline hint visible after unlock"
Assert-True (-not $scan2.Manual) "Still no NHẬP CÂN TAY after unlock"

try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch {}
Get-Process CanXe.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "=== Create ZIP from fresh publish ==="
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "build\create-acceptance-zip.ps1") -ZipName "CanXe-0.6.0-rc3-Phase6-RealInlineAdminWeightEdit.zip"

$zip = Join-Path $root "CanXe-0.6.0-rc3-Phase6-RealInlineAdminWeightEdit.zip"
Assert-True (Test-Path $zip) "ZIP exists"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tmp = Join-Path $env:TEMP ("p6rc3-zip-" + [guid]::NewGuid().ToString("N"))
[System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tmp)
$zipExeVi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $tmp "CanXe.Desktop.exe"))
Assert-True ($zipExeVi.ProductVersion -like "*0.6.0-rc3*") "ZIP exe ProductVersion is rc3 (got $($zipExeVi.ProductVersion))"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "SMOKE+PUBLISH+ZIP COMPLETE"
Write-Host "ZIP=$zip"
