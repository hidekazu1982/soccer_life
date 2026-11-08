# soccer_life 自動バックアップ
# 変更があればコミットして GitHub に push する。タスクスケジューラから呼ばれる。
$ErrorActionPreference = "Continue"
$project = "C:\project\soccer_life"
$log = Join-Path $project "backup_log.txt"

function Write-Log($msg) {
    $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Add-Content -Path $log -Value $line -Encoding UTF8
}

Set-Location $project

# ログが大きくなりすぎたら古い行を捨てる（最新500行だけ残す）
if ((Test-Path $log) -and ((Get-Item $log).Length -gt 200KB)) {
    Get-Content $log -Tail 500 | Set-Content $log -Encoding UTF8
}

git add -A 2>&1 | Out-Null
$changes = git status --porcelain
if (-not $changes) {
    Write-Log "変更なし"
    exit 0
}

$count = ($changes | Measure-Object -Line).Lines
$stamp = Get-Date -Format "yyyy-MM-dd HH:mm"
git commit -m "自動バックアップ $stamp（$count 件の変更）" 2>&1 | Out-Null

$pushOut = git push 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Log "push 成功（$count 件）"
} else {
    Write-Log "push 失敗: $pushOut"
}
