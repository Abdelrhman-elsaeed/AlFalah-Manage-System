@echo off
title Al-Falah Frontend Dev Server
cd /d "D:\AlFalah-Manage-System\frontend"
echo Starting Angular Frontend on http://localhost:4200 ...
npm run start -- --host 0.0.0.0 --port 4200
pause
