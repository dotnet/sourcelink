@echo off
powershell -ExecutionPolicy ByPass -command "& """%~dp0common\Build.ps1""" -restore -build -test -sign -pack -ci -publish -integrationTest %*"
exit /b %ErrorLevel%
