/¯¯¯¯/\¯¯¯¯\`·.''/¯¯¯¯/\¯¯¯¯\`·.'|¯¯¯¯|:|¯¯¯¯|`·.|¯¯¯¯|`·.
\____\::¯¯¯¯`·.'|.·´`·.·|::|`·.·´`·|:::|·.·´`·.|/____/`·.'|.·´`·.·|:::
____::\¯¯¯¯\`·.'|.·´`·.·|¨¨|       |:::|·.·´`·.|\¯¯¯¯\·./'|.·´`·.·|:::
\____\/____/`·.'|____|::|____|:::|____|:|____|`·.|____|:::
 '`·.:::::::::::::`·./'`·.:::::`·'`·.:::::`·.'`·.:::::`·`·.::::'`·.'`·.:::::`·.
SWISS ARMY KNIFE of IMAGES


SAKI-0.1 is a simple, compiled C program designed for essential batch image manipulation, supporting JPG, PNG, BMP, GIF, WEBP, and JFIF formats. It installs locally and adds an entry to the Windows Explorer context menu—labeled "SWISS ARMY KNIFE of IMAGES"—offering the following functions::

   👁View/Slideshow: View images extremely quickly and start a slideshow by pressing the 'F' key, using the arrow keys to switch between images. .
   🔀Convert image(s): Batch convert images between PNG, JPG, BMP, GIF, and WEBP formats.
   📐Resize: Batch resize images to any size using a bilinear filter to avoid quality loss.
   🌀 Rotate 90°: Batch rotate images by 90°.   
   🆔 Batch Rename: Say goodbye to Windows' annoying renaming function—where images end up with names like "(01)" in parentheses. Simply enter a name, such    as "wedding_photos," and the program will handle the numbering within the folder.


INSTALLATION
-------------
Unzip the ZIP file, open the "SAKI-0.3" folder, and run the "SAKI-0.3.exe" file. Don't worry; it performs a local installation that does not require Administrator privileges.

ATTENTION: After installation, do not delete the SAKI-0.1.exe file or the folder. They will likely be in your Windows Downloads folder; leave them there. You may delete the ZIP file if you wish.

The C# code is simple and utilizes Microsoft .NET along with the CSC compiler built into Windows 10/11.

To compile the SAKI-0.1.cs code, open a Windows terminal inside the unzipped folder and enter the following command:

C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:seu_icone.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll SAKI-0.1.cs

You can also compile it using Visual Studio. with a similar command. The installer contains libwebp.dll (BSD-3-Clause license), vcomp140.dll (Visual Studio)



SAKI 0.3 - INSTALLER WITH ART + REAL-ESRGAN RESIZE
===============================================
Base: submitted file SAKI-PDF-0.2.cs, with the PDF menu retained.

CONTENTS
- SAKI-PDF-0.3.cs: modified main code.
- PdfiumViewer.cs: PDF viewer from the previous modular version.
- SakiRealEsrgan.cs: batch super-resolution engine (external executable client).
- SAKI_InstallerArt.jpg: thumbnail resized from the provided image.
- COMPILAR_WIN64.bat: x64 compilation embedding the image as a resource in the EXE.

INSTALLER
When running SAKI-0.1.exe without arguments, the installation window displays
a side image, title, description, and buttons. The JPG is embedded directly
into the executable as the resource SAKI.InstallerArt. It does NOT need to
accompany the EXE after compilation.

RESIZE
The Resize submenu continues to use the same '-resize' command.
Four modes have been added to the Mode box:
- AI Real-ESRGAN 2x (Photo)
- AI Real-ESRGAN 4x (Photo)
- AI Real-ESRGAN 2x (Anime)
- AI Real-ESRGAN 4x (Anime)

The existing modes (% / fixed width / fixed height) remain unchanged.
With AI, the outputs are NEW .png files with suffixes like _SR2x_photo, etc.
The original file is NEVER deleted in AI mode. The AI ​​process is not
replaced by standard interpolation if the AI ​​is unavailable. REAL-ESRGAN ENGINE (REQUIRED ONLY FOR AI MODES)
Download the project's portable distribution:
https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/releases

Extract to this structure after compiling:
SAKI-0.1.exe
pdfium.dll                 (for PDF View)
libwebp.dll                (for native WebP support)
realesrgan-ncnn-vulkan/
realesrgan-ncnn-vulkan.exe
models/
realesrgan-x4plus.param
realesrgan-x4plus.bin
realesrgan-x4plus-anime.param
realesrgan-x4plus-anime.bin
[other DLLs and files included in the engine package]

It is also possible to place realesrgan-ncnn-vulkan.exe and the models folder
in the same folder as SAKI-0.1.exe.

Requires a Vulkan-compatible graphics card/driver.
If the GPU is incompatible, the tool will return an error; there is no
automatic fallback to CPU or bicubic processing.
The tool processes JPG, PNG, and WebP natively. BMP/GIF/JFIF and other
formats are temporarily converted to PNG via System.Drawing;
HEIC/HEIF/AVIF may fail if a compatible codec is missing.
Animated GIFs may be converted using only the initial frame.

PDF
- Requires x64 pdfium.dll from a trusted PDFium package.
- Context menu for .pdf files retained, invoking -pdfview.

WINDOWS DEFENDER / SMARTSCREEN
This package does not alter or bypass Windows security features.
HKCU (per-user) installation, same as the original version.

VALIDATION
- Structural check and automatic snippet comparison performed. - Compilation or execution in a Windows environment was not possible on this system.
- Test using file copies first: the legacy resize/convert modes
in your original code still delete the original file.

SINGLE-FILE C# VERSION
- SAKI-PDF-0.3-UNICO.cs incorporates the main code, PDFium, and Real-ESRGAN.
- COMPILAR_UNICO_WIN64.bat compiles this version without the two separate modules.
- SAKI_InstallerArt.jpg must still be present in the folder during COMPILATION;
afterwards, it is embedded into the executable.


SAKI-0.1 + PDFium | Modular integration (WinForms / .NET Framework)
=============================================================


COMPILATION
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x64 /out:SAKI-0.1-PDFium.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll SAKI-0.1.cs PdfiumViewer.cs

If you have a real .ico file, add /win32icon:icone.ico.

INSTALLATION
- Keep SAKI-0.1-PDFium.exe, pdfium.dll, and libwebp.dll in the same folder.
- Run SAKI-0.1-PDFium.exe without parameters and confirm the installer prompts.
- The context menu will be registered for images and, separately, for PDFs.
- If a previous installation exists, verify that the menu points
to the new executable, and do not move the files after installation. COMMANDS
- PDFs: right-click > Swiss Army Knife of Images > PDF View
- Terminal: SAKI-0.1-PDFium.exe -pdfview "C:\path\file.pdf"
- Left/Right arrows, Up/Down arrows, PgUp/PgDn: change page
- F: toggle full screen | ESC: exit full screen / close
- Ctrl + (+) / Ctrl + (-): zoom | Ctrl + 0: fit to window

LIMITATIONS
- This is a raster-based PDF viewer: no text search,
no text selection/copying, no form filling, and no printing.
- Password-protected PDFs do not open in this version.
- Large PDFs are loaded into memory. Each page is rasterized on demand.
- Code snippet statically reviewed, but NOT compiled/tested on Windows in this environment.