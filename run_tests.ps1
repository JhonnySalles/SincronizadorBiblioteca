param(
    [string]$Configuration = "Debug"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$esc = [char]27
$green  = "$esc[92m"
$red    = "$esc[91m"
$yellow = "$esc[93m"
$cyan   = "$esc[96m"
$bold   = "$esc[1m"
$reset  = "$esc[0m"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  SyncLib - Executando Testes Automatizados" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""

$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$processInfo.FileName = "dotnet"
$processInfo.Arguments = "test SyncLib.Tests/SyncLib.Tests.csproj --configuration $Configuration --logger `"console;verbosity=normal`""
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$processInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $processInfo

$process.Start() | Out-Null

while (-not $process.StandardOutput.EndOfStream) {
    $line = $process.StandardOutput.ReadLine()
    if ($null -eq $line) { continue }

    if ($line -match '^\s*(Aprovado|Passed)\b') {
        $status = $matches[1]
        $idx = $line.IndexOf($status)
        $prefix = $line.Substring(0, $idx)
        $rest = $line.Substring($idx + $status.Length)
        Write-Host "$prefix$green$bold$status$reset$rest"
    }
    elseif ($line -match '^\s*(Com falha|Failed|Falhou|Falha)\b') {
        $status = $matches[1]
        $idx = $line.IndexOf($status)
        $prefix = $line.Substring(0, $idx)
        $rest = $line.Substring($idx + $status.Length)
        Write-Host "$prefix$red$bold$status$reset$rest"
    }
    elseif ($line -match '^\s*(Ignorado|Skipped)\b') {
        $status = $matches[1]
        $idx = $line.IndexOf($status)
        $prefix = $line.Substring(0, $idx)
        $rest = $line.Substring($idx + $status.Length)
        Write-Host "$prefix$yellow$bold$status$reset$rest"
    }
    elseif ($line -match '^\s*\[FAIL\]') {
        Write-Host "$red$line$reset"
    }
    elseif ($line -match 'Execução de Teste Bem-sucedida|Passed!') {
        Write-Host "$green$bold$line$reset"
    }
    elseif ($line -match 'Falha na Execução de Teste|Failed!') {
        Write-Host "$red$bold$line$reset"
    }
    else {
        Write-Host $line
    }
}

$stderr = $process.StandardError.ReadToEnd()
if (-not [string]::IsNullOrWhiteSpace($stderr)) {
    Write-Host "$red$stderr$reset"
}

$process.WaitForExit()
$exitCode = $process.ExitCode

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "===================================================" -ForegroundColor Green
    Write-Host "  [SUCESSO] Todos os testes passaram com exito!  " -ForegroundColor Green
    Write-Host "===================================================" -ForegroundColor Green
} else {
    Write-Host "===================================================" -ForegroundColor Red
    Write-Host "  [FALHA] Ocorreram falhas durante os testes.     " -ForegroundColor Red
    Write-Host "===================================================" -ForegroundColor Red
}

exit $exitCode
