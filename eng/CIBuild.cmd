@echo off
powershell -ExecutionPolicy ByPass -command "& """common\%~dp0Build.ps1""" -restore -build -test -sign -pack -ci -integrationTest %*"
exit /b %ErrorLevel%
