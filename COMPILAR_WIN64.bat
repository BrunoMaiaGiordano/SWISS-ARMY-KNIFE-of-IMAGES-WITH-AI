@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo ERRO: compiller C# do .NET Framework 4.x not found.
 pause
 exit /b 1
)

"%CSC%" /win32icon:"icon.ico" /target:winexe /platform:x64 /optimize+ ^
 /out:"SAKI-0.3.exe" ^
 /resource:"SAKI_InstallerArt.jpg",SAKI.InstallerArt ^
 /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
 "SAKI-PDF-0.3.cs" "PdfiumViewer.cs" "SakiRealEsrgan.cs"
if errorlevel 1 (
 echo.
 echo Compillation mistakes. Review messages above.
 pause
 exit /b 1
)
echo.
echo SAKI-0.3.exe generated.
echo For PDF: PUT pdfium.dll x64 from side EXE.
echo For AI: look README.txt (motor and models from Real-ESRGAN).
pause
