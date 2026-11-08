# auto_backup.ps1 を 1 時間ごとに実行するタスクを登録する（1 回だけ実行すればよい）
$script = "C:\project\soccer_life\auto_backup.ps1"
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`""
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) `
    -RepetitionInterval (New-TimeSpan -Hours 1)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName "soccer_life 自動バックアップ" `
    -Action $action -Trigger $trigger -Settings $settings -Force
Write-Host "登録しました。1 時間ごとに自動バックアップします。"
