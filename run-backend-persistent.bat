@echo off
title Al-Falah Backend API
set DOTNET_ROLL_FORWARD=Major
set ASPNETCORE_ENVIRONMENT=Development
cd /d "D:\AlFalah-Manage-System\backend\AlFalah.Api"
echo Starting Al-Falah Backend API on http://localhost:5264 ...
dotnet run --no-build --launch-profile http
pause
