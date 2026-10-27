using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using System.ComponentModel;
using System.Diagnostics;

class Program
{
    static string[] extensoes = { "jpg", "jpeg", "jfif", "png", "gif", "bmp", "webp", "heic", "heif", "avif" };
    static string baseMenuPath = @"Software\Classes\SystemFileAssociations\.{0}\Shell\CaniveteImagens";
    static string registryUninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CaniveteSuicoImagens";

    [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern UIntPtr WebPEncodeBGRA(IntPtr bgra, int width, int height, int stride, float quality_factor, out IntPtr output);

    [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr WebPDecodeBGRA(IntPtr data, UIntPtr data_size, out int width, out int height);

    [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void WebPFree(IntPtr ptr);

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0)
        {
            ProcessarComando(args);
            return;
        }

        string exePath = Application.ExecutablePath;

        // Imagem embutida como recurso no EXE: nao necessita de arquivo separado na instalacao.
        Form installForm = new Form {
            Text = "SAKI | Local Installer",
            ClientSize = new Size(692, 366),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9f),
            AutoScaleMode = AutoScaleMode.Font
        };
        try { installForm.Icon = Icon.ExtractAssociatedIcon(exePath); } catch { }

        PictureBox artwork = new PictureBox {
            Left = 16, Top = 16, Width = 224, Height = 332,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(245, 239, 248),
            BorderStyle = BorderStyle.FixedSingle
        };
        try {
            using (Stream asset = Assembly.GetExecutingAssembly().GetManifestResourceStream("SAKI.InstallerArt")) {
                if (asset != null) {
                    using (Image source = Image.FromStream(asset)) artwork.Image = new Bitmap(source);
                }
            }
        } catch { /* Nao impede a instalacao se o recurso nao foi incluido na compilacao. */ }

        Label heading = new Label {
            Text = "SAKI-0.1", Left = 266, Top = 28, Width = 392, Height = 49,
            Font = new Font("Segoe UI", 23f, FontStyle.Bold),
            ForeColor = Color.FromArgb(161, 21, 72)
        };
        Label subtitle = new Label {
            Text = "Swiss Army Knife of Images", Left = 269, Top = 86,
            Width = 388, Height = 28,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(85, 26, 47)
        };
        Label prompt = new Label {
            Text = "Install SAKI for the current Windows user?", Left = 268, Top = 139,
            Width = 392, Height = 40,
            Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(40, 35, 40)
        };
        Label scope = new Label {
            Text = "Local setup  |  No administrator privileges required\nAdds tools to the Windows Explorer context menu.",
            Left = 269, Top = 188, Width = 390, Height = 52,
            ForeColor = Color.FromArgb(100, 91, 100)
        };
        Label author = new Label {
            Text = "Created by Bruno Maia Giordano", Left = 269, Top = 324,
            Width = 375, Height = 22, ForeColor = Color.FromArgb(110, 104, 110),
            Font = new Font("Segoe UI", 8f)
        };
        Button btnInstall = new Button {
            Text = "Install", Left = 382, Top = 264, Width = 129, Height = 39,
            BackColor = Color.FromArgb(192, 30, 83), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Yes
        };
        btnInstall.FlatAppearance.BorderSize = 0;
        Button btnCancel = new Button {
            Text = "Cancel", Left = 525, Top = 264, Width = 129, Height = 39,
            DialogResult = DialogResult.No
        };
        installForm.Controls.AddRange(new Control[] {
            artwork, heading, subtitle, prompt, scope, author, btnInstall, btnCancel
        });
        installForm.AcceptButton = btnInstall;
        installForm.CancelButton = btnCancel;

        DialogResult result;
        try { result = installForm.ShowDialog(); }
        finally {
            if (artwork.Image != null) artwork.Image.Dispose();
            installForm.Dispose();
        }
        if (result == DialogResult.Yes)
        {
            InstalarCaniveteLocal();
            MessageBox.Show("Swiss Army Knife successfully installed!", "All done!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static void InstalarCaniveteLocal()
    {
        string exePath = Application.ExecutablePath;

        try 
        {
            using (RegistryKey expKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer")) 
            {
                expKey.SetValue("MultipleInvokePromptMinimum", 500, RegistryValueKind.DWord);
            }
        } 
        catch { }

        foreach (string ext in extensoes)
        {
            string path = string.Format(baseMenuPath, ext);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(path))
            {
                key.SetValue("MUIVerb", "Swiss Army Knife of Images");
                key.SetValue("SubCommands", "");
                key.SetValue("Icon", exePath);

                using (RegistryKey shellKey = key.CreateSubKey("shell"))
                {
                    // Adicionado a primeira entrada "View" e removido todas as referências a arquivos .dll externos
                    ConfigurarItemMenu(shellKey, "0_View", "👁 View/Slideshow", "", string.Format("\"{0}\" -view \"%1\"", exePath));
                    ConfigurarItemMenu(shellKey, "1_Converter", "🔀 Convert image(s)", "", string.Format("\"{0}\" -convert \"%1\"", exePath));
                    ConfigurarItemMenu(shellKey, "2_Redimensionar", "📐 Resize", "", string.Format("\"{0}\" -resize \"%1\"", exePath));
                    ConfigurarItemMenu(shellKey, "3_Rotacionar", "🌀 Rotate 90°", "", string.Format("\"{0}\" -rotate \"%1\"", exePath));
                    ConfigurarItemMenu(shellKey, "4_Renomear", "🆔 Batch Rename", "", string.Format("\"{0}\" -rename \"%1\"", exePath)); 
                    ConfigurarItemMenu(shellKey, "5_Sobre", "ℹ About", "", string.Format("\"{0}\" -about \"%1\"", exePath));
                }
            }
        }

        // PDF possui menu proprio: nao entra no array de formatos de imagem.
        using (RegistryKey pdfKey = Registry.CurrentUser.CreateSubKey(string.Format(baseMenuPath, "pdf")))
        {
            pdfKey.SetValue("MUIVerb", "Swiss Army Knife of Images");
            pdfKey.SetValue("SubCommands", "");
            pdfKey.SetValue("Icon", exePath);
            using (RegistryKey pdfShell = pdfKey.CreateSubKey("shell"))
            {
                ConfigurarItemMenu(pdfShell, "0_PdfView", "PDF View", "",
                    string.Format("\"{0}\" -pdfview \"%1\"", exePath));
            }
        }

        using (RegistryKey uninstallKey = Registry.CurrentUser.CreateSubKey(registryUninstallPath))
        {
            uninstallKey.SetValue("DisplayName", "Swiss Army Knife of Images");
            uninstallKey.SetValue("UninstallString", string.Format("\"{0}\" -uninstall", exePath));
            uninstallKey.SetValue("DisplayIcon", exePath);
        }
    }

    static RegistryKey ConfigurarItemMenu(RegistryKey shellKey, string id, string texto, string icone, string comando)
    {
        RegistryKey itemKey = shellKey.CreateSubKey(id);
        itemKey.SetValue("MUIVerb", texto);
        
        if (!string.IsNullOrEmpty(icone))
            itemKey.SetValue("Icon", icone);
        else
            try { itemKey.DeleteValue("Icon", false); } catch {}

        using (RegistryKey cmdKey = itemKey.CreateSubKey("command"))
        {
            cmdKey.SetValue("", comando);
        }
        return itemKey;
    }

    public static Image CarregarImagem(string caminho)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(caminho);
            if (Path.GetExtension(caminho).ToLower() == ".webp")
            {
                IntPtr unmanagedData = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedData, bytes.Length);
                int w, h;
                
                IntPtr decodificado = WebPDecodeBGRA(unmanagedData, (UIntPtr)bytes.Length, out w, out h);
                Marshal.FreeHGlobal(unmanagedData);

                if (decodificado == IntPtr.Zero)
                    throw new Exception("Falha ao decodificar arquivo WebP.");

                Bitmap bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppArgb, decodificado);
                Bitmap clone = new Bitmap(bmp);
                bmp.Dispose();
                WebPFree(decodificado);
                return clone;
            }
            else
            {
                MemoryStream ms = new MemoryStream(bytes);
                return Image.FromStream(ms);
            }
        }
        catch (DllNotFoundException)
        {
            MessageBox.Show("The 'libwebp.dll' library was not found in the executable's folder. Copy it there to use WebP..", "Dependency Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
        catch (BadImageFormatException)
        {
            MessageBox.Show("The 'libwebp.dll' found has an incompatible architecture. Download and use the x64 version of the DLL.", "Architectural Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    static void ProcessarComando(string[] args)
    {
        string acao = args[0];
        if (acao == "-uninstall") { DesinstalarCaniveteLocal(); return; }
        if (acao == "-about") { MostrarSobre(); return; }

        // PDF: visualizador independente, sem passar pelo pipeline de imagens.
        if (acao == "-pdfview")
        {
            if (args.Length >= 2 && File.Exists(args[1]) &&
                string.Equals(Path.GetExtension(args[1]), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try { Application.Run(new PdfViewerForm(args[1])); }
                catch (DllNotFoundException)
                {
                    MessageBox.Show("pdfium.dll nao encontrada. Coloque a versao x64 ao lado do SAKI-0.1.exe.",
                        "PDFium", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (BadImageFormatException)
                {
                    MessageBox.Show("pdfium.dll incompativel. O executavel e a DLL devem ser da mesma arquitetura (x64).",
                        "PDFium", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Nao foi possivel abrir o PDF: " + ex.Message,
                        "PDF View", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return;
        }

        if (args.Length < 2 || !File.Exists(args[1])) return;
        string arquivo = args[1];

        string tempFile = Path.Combine(Path.GetTempPath(), "canivete_" + acao.Replace("-", "") + ".txt");
        
        for(int i = 0; i < 10; i++) 
        {
            try 
            {
                using (StreamWriter sw = new StreamWriter(new FileStream(tempFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))) 
                {
                    sw.WriteLine(arquivo);
                }
                break;
            } 
            catch { Thread.Sleep(50); }
        }

        bool isFirstInstance;
        using (Mutex mutex = new Mutex(true, "CaniveteMutex_" + acao, out isFirstInstance))
        {
            if (!isFirstInstance) return;

            Thread.Sleep(600);

            List<string> arquivosEmLote = new List<string>();
            if (File.Exists(tempFile))
            {
                foreach (string linha in File.ReadAllLines(tempFile))
                {
                    if (!string.IsNullOrWhiteSpace(linha) && File.Exists(linha) && !arquivosEmLote.Contains(linha))
                    {
                        arquivosEmLote.Add(linha);
                    }
                }
                File.Delete(tempFile);
            }

            if (arquivosEmLote.Count == 0) return;

            try
            {
                switch (acao)
                {
                    case "-view": ExecutarVisualizador(arquivosEmLote); break;
                    case "-convert": ExecutarJanelaConversao(arquivosEmLote); break;
                    case "-resize": AbrirJanelaRedimensionar(arquivosEmLote); break;
                    case "-rotate": ProcessarRotacao(arquivosEmLote); break;
                    case "-rename": AbrirJanelaRenomear(arquivosEmLote); break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Operation Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    static void ExecutarVisualizador(List<string> arquivos)
    {
        string arquivoInicial = arquivos[0];
        string pasta = Path.GetDirectoryName(arquivoInicial);
        List<string> imagensDaPasta = new List<string>();

        try
        {
            foreach (string ext in extensoes)
            {
                imagensDaPasta.AddRange(Directory.GetFiles(pasta, "*." + ext));
            }
            HashSet<string> unique = new HashSet<string>(imagensDaPasta, StringComparer.OrdinalIgnoreCase);
            imagensDaPasta = new List<string>(unique);
            imagensDaPasta.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            imagensDaPasta = new List<string>(arquivos);
        }

        int indexInicial = imagensDaPasta.FindIndex(f => f.Equals(arquivoInicial, StringComparison.OrdinalIgnoreCase));
        if (indexInicial == -1)
        {
            imagensDaPasta.Insert(0, arquivoInicial);
            indexInicial = 0;
        }

        Application.Run(new VisualizadorForm(imagensDaPasta, indexInicial));
    }

    static void MostrarSobre()
    {
        MessageBox.Show(
            "Created and developed by Bruno Maia de Andrade\n\n" +
            "Licence | Free software under GPL 3\n" +
            "https://BrunoMaiaGiordano/simpleandstupidprojects", 
            "About the SAKI | Swiss Army Knife of Images", 
            MessageBoxButtons.OK, 
            MessageBoxIcon.Information
        );
    }

    static void ExecutarJanelaConversao(List<string> arquivos)
    {
        Form form = new Form { Text = "Convert " + arquivos.Count + " Files", Width = 280, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen };
        Label lbl = new Label { Text = "Choose the output format:", Left = 20, Top = 15, Width = 200 };
        ComboBox combo = new ComboBox { Left = 20, Top = 40, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(new string[] { "JPEG (.jpg)", "PNG (.png)", "GIF (.gif)", "BMP (.bmp)", "WebP (.webp)" });
        combo.SelectedIndex = 0;
        Button btn = new Button { Text = "Converter", Left = 140, Top = 75, Width = 100, DialogResult = DialogResult.OK };

        form.Controls.AddRange(new Control[] { lbl, combo, btn });
        form.AcceptButton = btn;

        if (form.ShowDialog() == DialogResult.OK)
        {
            ImageFormat formatoAlvo = ImageFormat.Jpeg;
            string extAlvo = ".jpg";
            bool converterParaWebP = false;

            switch (combo.SelectedIndex)
            {
                case 1: formatoAlvo = ImageFormat.Png; extAlvo = ".png"; break;
                case 2: formatoAlvo = ImageFormat.Gif; extAlvo = ".gif"; break;
                case 3: formatoAlvo = ImageFormat.Bmp; extAlvo = ".bmp"; break;
                case 4: converterParaWebP = true; extAlvo = ".webp"; break;
            }

            int convertidos = 0;
            foreach (string arq in arquivos)
            {
                try 
                {
                    string novoCaminho = Path.ChangeExtension(arq, extAlvo);
                    if (arq.ToLower() == novoCaminho.ToLower()) continue;

                    bool sucessoGravacao = false;

                    using (Image img = CarregarImagem(arq))
                    {
                        if (converterParaWebP)
                        {
                            sucessoGravacao = SalvarComoWebP(img, novoCaminho, 80f);
                        }
                        else
                        {
                            img.Save(novoCaminho, formatoAlvo);
                            sucessoGravacao = true;
                        }
                        
                        if (sucessoGravacao) convertidos++;
                    }

                    if (sucessoGravacao)
                    {
                        File.Delete(arq);
                    }
                } 
                catch (Exception ex)
                { 
                    if (ex is DllNotFoundException || ex is BadImageFormatException) break;
                }
            }
            MessageBox.Show(convertidos + "Files converted and originals deleted!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static bool SalvarComoWebP(Image img, string caminhoDestino, float qualidade)
    {
        try
        {
            using (Bitmap bmp = new Bitmap(img))
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                IntPtr outPtr = IntPtr.Zero;
                try
                {
                    UIntPtr size = WebPEncodeBGRA(bmpData.Scan0, bmp.Width, bmp.Height, bmpData.Stride, qualidade, out outPtr);
                    
                    if (size.ToUInt64() == 0 || outPtr == IntPtr.Zero) return false;

                    int length = (int)size.ToUInt64();
                    byte[] buffer = new byte[length];
                    Marshal.Copy(outPtr, buffer, 0, length);
                    File.WriteAllBytes(caminhoDestino, buffer);
                    return true;
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                    if (outPtr != IntPtr.Zero) WebPFree(outPtr);
                }
            }
        }
        catch
        {
            return false;
        }
    }

    static void AbrirJanelaRedimensionar(List<string> arquivos)
    {
        Form form = new Form { Text = "Resize " + arquivos.Count + " image(s)", Width = 450, Height = 232, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen };
        
        Label lblModo = new Label { Text = "Mode:", Left = 20, Top = 20, Width = 60 };
        ComboBox comboModo = new ComboBox { Left = 95, Top = 18, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        comboModo.Items.AddRange(new string[] {
            "Percentage (%)", "Fixed width (px)", "Fixed height (px)",
            "AI Real-ESRGAN 2x (Photo)", "AI Real-ESRGAN 4x (Photo)",
            "AI Real-ESRGAN 2x (Anime)", "AI Real-ESRGAN 4x (Anime)"
        });
        comboModo.SelectedIndex = 0;

        Label lblValor = new Label { Text = "Value:", Left = 20, Top = 58, Width = 64 };
        TextBox txtValor = new TextBox { Left = 95, Top = 55, Width = 320, Text = "100" };
        Label infoAI = new Label {
            Text = "AI modes need the local Real-ESRGAN Vulkan engine and models.\nOriginal images are preserved in AI mode.",
            Left = 23, Top = 86, Width = 390, Height = 42,
            ForeColor = Color.FromArgb(116, 38, 69), Visible = false
        };
        comboModo.SelectedIndexChanged += delegate {
            bool aiMode = comboModo.SelectedIndex >= 3;
            lblValor.Visible = !aiMode;
            txtValor.Visible = !aiMode;
            infoAI.Visible = aiMode;
        };
        Button btnOk = new Button { Text = "Execute", Left = 235, Top = 135, Width = 180, Height = 33, DialogResult = DialogResult.OK };

        form.Controls.AddRange(new Control[] { lblModo, comboModo, lblValor, txtValor, infoAI, btnOk });
        form.AcceptButton = btnOk;

        if (form.ShowDialog() == DialogResult.OK)
        {
            if (comboModo.SelectedIndex >= 3)
            {
                int aiOption = comboModo.SelectedIndex;
                int aiScale = (aiOption == 3 || aiOption == 5) ? 2 : 4;
                bool isAnime = aiOption >= 5;
                SakiRealEsrgan.Executar(arquivos, aiScale, isAnime);
                form.Dispose();
                return;
            }
            double valor;
            if (double.TryParse(txtValor.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor) && valor > 0)
            {
                int modo = comboModo.SelectedIndex;
                int processados = 0;

                foreach (string arq in arquivos)
                {
                    try 
                    {
                        using (Image img = CarregarImagem(arq))
                        {
                            int finalW = img.Width;
                            int finalH = img.Height;

                            if (modo == 0)
                            {
                                double pct = valor / 100.0;
                                finalW = (int)(img.Width * pct);
                                finalH = (int)(img.Height * pct);
                            }
                            else if (modo == 1)
                            {
                                int targetW = (int)valor;
                                double ratio = (double)targetW / img.Width;
                                finalW = targetW;
                                finalH = (int)(img.Height * ratio);
                            }
                            else if (modo == 2)
                            {
                                int targetH = (int)valor;
                                double ratio = (double)targetH / img.Height;
                                finalW = (int)(img.Width * ratio);
                                finalH = targetH;
                            }

                            if (finalW < 1) finalW = 1;
                            if (finalH < 1) finalH = 1;

                            using (Bitmap bmp = new Bitmap(finalW, finalH))
                            {
                                using (Graphics g = Graphics.FromImage(bmp))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                                    using (var wrapMode = new ImageAttributes())
                                    {
                                        wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                                        g.DrawImage(img, new Rectangle(0, 0, finalW, finalH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, wrapMode);
                                    }
                                    
                                    string ext = Path.GetExtension(arq);
                                    string pasta = Path.GetDirectoryName(arq);
                                    string nomeSemExt = Path.GetFileNameWithoutExtension(arq);
                                    string novoNome = Path.Combine(pasta, string.Format("{0}_{1}x{2}{3}", nomeSemExt, finalW, finalH, ext));
                                    
                                    bool sucessoGravacao = false;
                                    if (ext.ToLower() == ".webp")
                                    {
                                        sucessoGravacao = SalvarComoWebP(bmp, novoNome, 80f);
                                    }
                                    else
                                    {
                                        ImageFormat format = img.RawFormat;
                                        string extLower = ext.ToLower();
                                        if (extLower == ".jpg" || extLower == ".jpeg" || extLower == ".jfif") format = ImageFormat.Jpeg;
                                        else if (extLower == ".png") format = ImageFormat.Png;
                                        else if (extLower == ".gif") format = ImageFormat.Gif;
                                        else if (extLower == ".bmp") format = ImageFormat.Bmp;

                                        bmp.Save(novoNome, format);
                                        sucessoGravacao = true;
                                    }

                                    if (sucessoGravacao)
                                    {
                                        processados++;
                                        File.Delete(arq);
                                    }
                                }
                            }
                        }
                    } 
                    catch (Exception ex)
                    {
                        if (ex is DllNotFoundException || ex is BadImageFormatException) break;
                    }
                }
                MessageBox.Show(processados + " Processed images and originals deleted!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    static void ProcessarRotacao(List<string> arquivos)
    {
        foreach (var arq in arquivos)
        {
            try 
            {
                using (Image img = CarregarImagem(arq))
                {
                    img.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    if (Path.GetExtension(arq).ToLower() == ".webp")
                    {
                        SalvarComoWebP(img, arq, 80f);
                    }
                    else
                    {
                        img.Save(arq, img.RawFormat);
                    }
                }
            } 
            catch (Exception ex)
            {
                if (ex is DllNotFoundException || ex is BadImageFormatException) break;
            }
        }
    }

    static void AbrirJanelaRenomear(List<string> arquivos)
    {
        Form form = new Form { Text = "Rename " + arquivos.Count + " files", Width = 350, Height = 140, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen };
        Label lblPattern = new Label { Text = "New Base Name:", Left = 20, Top = 20, Width = 180 };
        TextBox txtBase = new TextBox { Left = 20, Top = 45, Width = 290, Text = "Photo" };
        Button btnOk = new Button { Text = "Renomear Todos", Left = 170, Top = 80, Width = 140, DialogResult = DialogResult.OK };

        form.Controls.AddRange(new Control[] { lblPattern, txtBase, btnOk });
        form.AcceptButton = btnOk;

        if (form.ShowDialog() == DialogResult.OK)
        {
            string baseName = txtBase.Text.Trim();
            if (string.IsNullOrEmpty(baseName)) baseName = "Imagem";

            List<string> tempPaths = new List<string>();
            List<string> finalPaths = new List<string>();
            int num = 1;

            foreach (string arq in arquivos)
            {
                string dir = Path.GetDirectoryName(arq);
                string ext = Path.GetExtension(arq);
                
                string tempPath = Path.Combine(dir, Guid.NewGuid().ToString() + ext);
                File.Move(arq, tempPath); 
                
                string finalName = string.Format("{0} {1:D2}{2}", baseName, num, ext); 
                string finalPath = Path.Combine(dir, finalName);
                
                tempPaths.Add(tempPath);
                finalPaths.Add(finalPath);
                num++;
            }

            int processados = 0;
            for (int i = 0; i < tempPaths.Count; i++)
            {
                string target = finalPaths[i];
                while (File.Exists(target)) 
                {
                    target = target.Replace(Path.GetExtension(target), "_copia" + Path.GetExtension(target));
                }
                File.Move(tempPaths[i], target);
                processados++;
            }

            MessageBox.Show(processados + " Files successfully renamed!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static void DesinstalarCaniveteLocal()
    {
        foreach (string ext in extensoes)
        {
            string path = string.Format(baseMenuPath, ext);
            try { Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
        }
        
        try 
        {
            using (RegistryKey expKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer")) 
            {
                expKey.DeleteValue("MultipleInvokePromptMinimum", false);
            }
        } 
        catch { }


        // Remover tambem a integracao de arquivos PDF.
        try { Registry.CurrentUser.DeleteSubKeyTree(string.Format(baseMenuPath, "pdf"), false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(registryUninstallPath, false); } catch { }
        MessageBox.Show("The Swiss Army Knife has been completely removed from its user.", "Uninstalled", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// Classe dedicada para gerenciar a janela sem menus e atalhos de navegação nativos por Setas e Tela Cheia (F)
class VisualizadorForm : Form
{
    private List<string> imagens;
    private int index;
    private PictureBox pb;
    private bool isFullscreen = false;
    private FormBorderStyle originalStyle;
    private FormWindowState originalState;

    public VisualizadorForm(List<string> todasImagens, int indexInicial)
    {
        this.imagens = todasImagens;
        this.index = indexInicial;
        
        this.Text = "Viewer | Swiss Army Knife for Images";
        this.BackColor = Color.Black;
        this.Size = new Size(900, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.KeyPreview = true;

        pb = new PictureBox();
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        this.Controls.Add(pb);

        AtualizarImagem();
    }

    private void AtualizarImagem()
    {
        if (imagens.Count == 0) return;
        try
        {
            string caminho = imagens[index];
            Image antiga = pb.Image;
            pb.Image = Program.CarregarImagem(caminho);
            if (antiga != null) antiga.Dispose();
            this.Text = string.Format("View - [{0}/{1}] - {2}", index + 1, imagens.Count, Path.GetFileName(caminho));
        }
        catch (Exception ex)
        {
            this.Text = "Error loading image: " + ex.Message;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Right)
        {
            if (imagens.Count > 0)
            {
                index = (index + 1) % imagens.Count;
                AtualizarImagem();
            }
            return true;
        }
        if (keyData == Keys.Left)
        {
            if (imagens.Count > 0)
            {
                index = (index - 1 + imagens.Count) % imagens.Count;
                AtualizarImagem();
            }
            return true;
        }
        if (keyData == Keys.F)
        {
            AlternarTelaCheia();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            if (isFullscreen)
            {
                AlternarTelaCheia();
            }
            else
            {
                this.Close();
            }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AlternarTelaCheia()
    {
        if (!isFullscreen)
        {
            originalStyle = this.FormBorderStyle;
            originalState = this.WindowState;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            isFullscreen = true;
        }
        else
        {
            this.FormBorderStyle = originalStyle;
            this.WindowState = originalState;
            isFullscreen = false;
        }
    }
}

// ======================= ENXERTO: PDF VIEW COM PDFIUM =======================
// Requer pdfium.dll (Windows x64) na mesma pasta que SAKI-0.1.exe.
// Nao precisa de NuGet, WebView2 nem assemblies gerenciados extras.
internal static class PdfiumNative
{
    private const string Dll = "pdfium.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDF_InitLibrary();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDF_DestroyLibrary();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, UIntPtr size, IntPtr password);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern uint FPDF_GetLastError();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern double FPDF_GetPageWidth(IntPtr page);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern double FPDF_GetPageHeight(IntPtr page);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format,
        IntPtr buffer, int stride);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top,
        int width, int height, uint color);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page,
        int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern void FPDF_CloseDocument(IntPtr document);
}

internal sealed class PdfViewerForm : Form
{
    private readonly string arquivo;
    private readonly Panel area;
    private readonly PictureBox tela;
    private readonly System.Windows.Forms.Timer atrasoResize;
    private byte[] dados;
    private GCHandle dadosFixos;
    private IntPtr documento = IntPtr.Zero;
    private int totalPaginas;
    private int pagina = 0;
    private double zoom = 1.0;  // 1.0 = ajustar pagina a janela
    private bool bibliotecaAberta;
    private bool telaCheia;
    private FormBorderStyle bordaOriginal;
    private FormWindowState estadoOriginal;

    public PdfViewerForm(string caminho)
    {
        arquivo = caminho;
        Text = "SAKI - PDF View";
        BackColor = Color.Black;
        Width = 950;
        Height = 720;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        area = new Panel();
        area.Dock = DockStyle.Fill;
        area.BackColor = Color.FromArgb(34, 34, 34);
        area.AutoScroll = true;

        tela = new PictureBox();
        tela.SizeMode = PictureBoxSizeMode.Normal;
        tela.BackColor = Color.White;
        area.Controls.Add(tela);
        Controls.Add(area);

        atrasoResize = new System.Windows.Forms.Timer();
        atrasoResize.Interval = 180;
        atrasoResize.Tick += delegate {
            atrasoResize.Stop();
            RenderizarPagina();
        };
        Resize += delegate {
            if (Visible && documento != IntPtr.Zero)
            {
                atrasoResize.Stop();
                atrasoResize.Start();
            }
        };

        try
        {
            PdfiumNative.FPDF_InitLibrary();
            bibliotecaAberta = true;
            dados = File.ReadAllBytes(arquivo);
            if (dados.Length == 0) throw new InvalidDataException("PDF vazio.");

            // PDFium consulta o buffer durante toda a vida do documento.
            dadosFixos = GCHandle.Alloc(dados, GCHandleType.Pinned);
            documento = PdfiumNative.FPDF_LoadMemDocument64(
                dadosFixos.AddrOfPinnedObject(), new UIntPtr((ulong)dados.LongLength), IntPtr.Zero);
            if (documento == IntPtr.Zero)
                throw new InvalidDataException("PDF nao abriu (PDFium error " + PdfiumNative.FPDF_GetLastError() + ").");

            totalPaginas = PdfiumNative.FPDF_GetPageCount(documento);
            if (totalPaginas < 1) throw new InvalidDataException("PDF sem paginas.");
        }
        catch
        {
            LiberarPdf();
            throw;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderizarPagina();
    }

    private void RenderizarPagina()
    {
        if (documento == IntPtr.Zero || totalPaginas < 1) return;
        IntPtr pag = IntPtr.Zero;
        Bitmap imagem = null;

        try
        {
            pag = PdfiumNative.FPDF_LoadPage(documento, pagina);
            if (pag == IntPtr.Zero) throw new InvalidDataException("Nao foi possivel ler a pagina.");
            double pageW = PdfiumNative.FPDF_GetPageWidth(pag);
            double pageH = PdfiumNative.FPDF_GetPageHeight(pag);
            if (pageW <= 0 || pageH <= 0) throw new InvalidDataException("Dimensoes invalidas da pagina.");

            int larguraUtil = Math.Max(100, area.ClientSize.Width - 24);
            int alturaUtil = Math.Max(100, area.ClientSize.Height - 24);
            double ajuste = Math.Min(larguraUtil / pageW, alturaUtil / pageH);
            double escala = ajuste * zoom;

            // Limite de memoria para PDFs com paginas excepcionalmente grandes.
            double pixels = pageW * pageH * escala * escala;
            if (pixels > 20000000.0) escala *= Math.Sqrt(20000000.0 / pixels);
            if (pageW * escala > 6000 || pageH * escala > 6000)
                escala *= Math.Min(6000.0 / (pageW * escala), 6000.0 / (pageH * escala));

            int w = Math.Max(1, (int)Math.Round(pageW * escala));
            int h = Math.Max(1, (int)Math.Round(pageH * escala));
            imagem = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData data = imagem.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                if (data.Stride <= 0)
                    throw new InvalidOperationException("Bitmap com stride incompativel.");

                // 4 = FPDFBitmap_BGRA. Escreve diretamente na imagem WinForms.
                IntPtr pdfBitmap = PdfiumNative.FPDFBitmap_CreateEx(w, h, 4, data.Scan0, data.Stride);
                if (pdfBitmap == IntPtr.Zero) throw new OutOfMemoryException("Falha ao criar bitmap PDFium.");
                try
                {
                    PdfiumNative.FPDFBitmap_FillRect(pdfBitmap, 0, 0, w, h, 0xFFFFFFFFu);
                    PdfiumNative.FPDF_RenderPageBitmap(pdfBitmap, pag, 0, 0, w, h, 0, 1); // 1=anotacoes
                }
                finally { PdfiumNative.FPDFBitmap_Destroy(pdfBitmap); }
            }
            finally { imagem.UnlockBits(data); }

            Image anterior = tela.Image;
            tela.Image = imagem;
            imagem = null;
            tela.Size = tela.Image.Size;
            tela.Location = new Point(
                Math.Max(0, (area.ClientSize.Width - tela.Width) / 2),
                Math.Max(0, (area.ClientSize.Height - tela.Height) / 2));
            area.AutoScrollPosition = new Point(0, 0);
            if (anterior != null) anterior.Dispose();
            Text = string.Format("PDF View - [{0}/{1}] - {2} | Zoom {3:0}%",
                pagina + 1, totalPaginas, Path.GetFileName(arquivo), zoom * 100.0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PDF View", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // Nao fechar antes do finally liberar o handle da pagina.
        }
        finally
        {
            if (imagem != null) imagem.Dispose();
            if (pag != IntPtr.Zero) PdfiumNative.FPDF_ClosePage(pag);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Right || keyData == Keys.Down || keyData == Keys.PageDown)
        {
            pagina = (pagina + 1) % totalPaginas;
            RenderizarPagina();
            return true;
        }
        if (keyData == Keys.Left || keyData == Keys.Up || keyData == Keys.PageUp)
        {
            pagina = (pagina - 1 + totalPaginas) % totalPaginas;
            RenderizarPagina();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Add) || keyData == (Keys.Control | Keys.Oemplus))
        {
            zoom = Math.Min(4.0, zoom * 1.25);
            RenderizarPagina();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Subtract) || keyData == (Keys.Control | Keys.OemMinus))
        {
            zoom = Math.Max(0.25, zoom / 1.25);
            RenderizarPagina();
            return true;
        }
        if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
        {
            zoom = 1.0;
            RenderizarPagina();
            return true;
        }
        if (keyData == Keys.F)
        {
            if (!telaCheia)
            {
                bordaOriginal = FormBorderStyle;
                estadoOriginal = WindowState;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                telaCheia = true;
            }
            else
            {
                FormBorderStyle = bordaOriginal;
                WindowState = estadoOriginal;
                telaCheia = false;
            }
            RenderizarPagina();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            if (telaCheia)
            {
                FormBorderStyle = bordaOriginal;
                WindowState = estadoOriginal;
                telaCheia = false;
                RenderizarPagina();
            }
            else Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        atrasoResize.Stop();
        Image antiga = tela.Image;
        tela.Image = null;
        if (antiga != null) antiga.Dispose();
        LiberarPdf();
        base.OnFormClosed(e);
    }

    private void LiberarPdf()
    {
        if (documento != IntPtr.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(documento);
            documento = IntPtr.Zero;
        }
        if (dadosFixos.IsAllocated) dadosFixos.Free();
        dados = null;
        if (bibliotecaAberta)
        {
            PdfiumNative.FPDF_DestroyLibrary();
            bibliotecaAberta = false;
        }
    }
}
// ===================== FIM DO ENXERTO PDFIUM ================================

// Super-resolucao REAL: usa o executavel externo Real-ESRGAN-ncnn-vulkan.
// Sem engine/modelos nao ocorre upscaling nem substituicao silenciosa por bicubico.
// Processamento em lote: salva PNGs novos, preservando TODOS os arquivos originais.
internal static class SakiRealEsrgan
{
    internal static void Executar(List<string> arquivos, int escala, bool anime)
    {
        if (arquivos == null || arquivos.Count == 0) return;
        if (escala != 2 && escala != 4) throw new ArgumentException("Escala nao suportada.");

        string baseDir = Application.StartupPath;
        string ferramenta = Path.Combine(baseDir, "realesrgan-ncnn-vulkan");
        string executavel = Path.Combine(ferramenta, "realesrgan-ncnn-vulkan.exe");
        // Tambem aceita os arquivos do motor diretamente ao lado do SAKI.
        if (!File.Exists(executavel))
        {
            ferramenta = baseDir;
            executavel = Path.Combine(ferramenta, "realesrgan-ncnn-vulkan.exe");
        }
        string modelDir = Path.Combine(ferramenta, "models");
        string modelo = anime ? "realesrgan-x4plus-anime" : "realesrgan-x4plus";

        if (!File.Exists(executavel) || !File.Exists(Path.Combine(modelDir, modelo + ".param")) ||
            !File.Exists(Path.Combine(modelDir, modelo + ".bin")))
        {
            MessageBox.Show(
                "To use AI Resize, install Real-ESRGAN-ncnn-vulkan locally.\n\n" +
                "Place its EXE and 'models' folder in:\n" +
                Path.Combine(baseDir, "realesrgan-ncnn-vulkan") + "\n\n" +
                "Download (official):\nhttps://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/releases\n\n" +
                "Missing engine or model: " + modelo + "\n\n" +
                "A Vulkan-capable graphics driver is required. Original images were not changed.",
                "SAKI | AI Resize", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Form progressForm = new Form {
            Text = "SAKI | AI Resize (Real-ESRGAN)", Width = 475, Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.White, Font = new Font("Segoe UI", 9f)
        };
        Label status = new Label {
            Left = 18, Top = 15, Width = 420, Height = 45,
            Text = "Preparing AI upscale " + escala + "x..."
        };
        ProgressBar bar = new ProgressBar { Left = 18, Top = 64, Width = 420, Height = 23,
            Minimum = 0, Maximum = 100, Value = 0 };
        Button cancel = new Button { Text = "Cancel", Left = 338, Top = 100, Width = 100, Height = 28 };
        progressForm.Controls.AddRange(new Control[] { status, bar, cancel });

        int completed = 0;
        List<string> failures = new List<string>();
        BackgroundWorker worker = new BackgroundWorker {
            WorkerReportsProgress = true, WorkerSupportsCancellation = true
        };
        cancel.Click += delegate {
            if (worker.IsBusy) {
                worker.CancelAsync();
                cancel.Enabled = false;
                status.Text = "Cancelling...";
            }
        };
        progressForm.FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (worker.IsBusy) {
                worker.CancelAsync();
                cancel.Enabled = false;
                status.Text = "Waiting for the running upscale to stop...";
                e.Cancel = true;
            }
        };
        worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
            for (int i = 0; i < arquivos.Count; i++) {
                if (worker.CancellationPending) { e.Cancel = true; return; }
                string original = arquivos[i];
                string temporario = null;
                string destino = null;
                bool sucesso = false;
                try {
                    if (!File.Exists(original)) throw new FileNotFoundException("Input not found", original);
                    string ext = Path.GetExtension(original).ToLowerInvariant();
                    string entrada = original;
                    // O motor NCNN aceita JPG/PNG/WEBP. Formatos extras sao preparados em PNG.
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp") {
                        temporario = Path.Combine(Path.GetTempPath(), "SAKI_AI_" + Guid.NewGuid().ToString("N") + ".png");
                        using (Image img = Program.CarregarImagem(original)) {
                            using (Bitmap bmp = new Bitmap(img)) bmp.Save(temporario, ImageFormat.Png);
                        }
                        entrada = temporario;
                    }
                    string baseName = Path.GetFileNameWithoutExtension(original);
                    string suffix = anime ? "anime" : "photo";
                    string dir = Path.GetDirectoryName(original);
                    string name = baseName + "_SR" + escala + "x_" + suffix;
                    destino = Path.Combine(dir, name + ".png");
                    for (int n = 2; File.Exists(destino); n++) {
                        destino = Path.Combine(dir, name + "_" + n + ".png");
                    }
                    string args = "-i \"" + entrada + "\" -o \"" + destino + "\" -n " + modelo +
                                  " -s " + escala + " -m \"" + modelDir + "\" -f png";
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = executavel, Arguments = args, WorkingDirectory = ferramenta,
                        UseShellExecute = false, CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (Process p = new Process()) {
                        p.StartInfo = psi;
                        if (!p.Start()) throw new InvalidOperationException("Could not launch Real-ESRGAN.");
                        while (!p.WaitForExit(250)) {
                            if (worker.CancellationPending) {
                                try { p.Kill(); } catch { }
                                try { p.WaitForExit(); } catch { }
                                e.Cancel = true;
                                return;
                            }
                        }
                        if (p.ExitCode != 0) throw new InvalidOperationException("Real-ESRGAN exit code: " + p.ExitCode);
                    }
                    if (!File.Exists(destino) || new FileInfo(destino).Length == 0)
                        throw new IOException("AI tool did not produce a valid output file.");
                    completed++;
                    sucesso = true;
                }
                catch (Exception ex) { failures.Add(Path.GetFileName(original) + ": " + ex.Message); }
                finally {
                    if (temporario != null) { try { File.Delete(temporario); } catch { } }
                    if (!sucesso && destino != null) { try { File.Delete(destino); } catch { } }
                }
                worker.ReportProgress((int)((i + 1) * 100.0 / arquivos.Count),
                    (i + 1) + " / " + arquivos.Count + " — " + Path.GetFileName(original));
            }
        };
        worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) {
            bar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            status.Text = e.UserState == null ? "Processing..." : e.UserState.ToString();
        };
        worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
            progressForm.Close();
            string message = completed + " image(s) saved as new PNG files (originals kept).";
            if (e.Cancelled) message += "\n\nOperation cancelled.";
            if (e.Error != null) message += "\n\n" + e.Error.Message;
            if (failures.Count > 0) {
                message += "\n\nFailed: " + failures.Count;
                for (int i = 0; i < Math.Min(4, failures.Count); i++) message += "\n" + failures[i];
            }
            MessageBox.Show(message, "SAKI | AI Resize", MessageBoxButtons.OK,
                failures.Count == 0 && e.Error == null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        };
        progressForm.Shown += delegate { worker.RunWorkerAsync(); };
        progressForm.ShowDialog();
        progressForm.Dispose();
    }
}
