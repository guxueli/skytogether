@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%~dp0release" mkdir "%~dp0release"
"%CSC%" /nologo /target:winexe /out:"%~dp0release\ScreenInk.exe" /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "%~dp0src\ScreenInk.cs" "%~dp0src\AgentContext.cs" "%~dp0src\McpServer.cs"
if errorlevel 1 exit /b 1
echo Built: %~dp0release\ScreenInk.exe
