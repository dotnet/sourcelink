@echo off
powershell -ExecutionPolicy ByPass -command "& """%~dp0common\Build.ps1""" -restore -build -test -sign -pack -ci -integrationTest %*"
exit /b %ErrorLevel%
