@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo Nao encontrou compilador CSC & pause & exit /b 1)
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
 /out:"SAKI-0.1.exe" ^
 /resource:"SAKI_InstallerArt.jpg",SAKI.InstallerArt ^
 /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
 "SAKI-PDF-0.3-UNICO.cs"
if errorlevel 1 (echo Falha na compilacao & pause & exit /b 1)
echo Compilado. Adicione PDFium e Real-ESRGAN para usar os recursos opcionais.
pause
