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
            Text = "SAKI-0.3", Left = 266, Top = 28, Width = 392, Height = 49,
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
                    // Olho desenhado em ASCII; opcao propria para IA, sem mudar Resize normal.
                    ConfigurarItemMenu(shellKey, "2a_ResizeAI", "(<o>) Resize with AI", "", string.Format("\"{0}\" -resizeai \"%1\"", exePath));
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
                    case "-resizeai": AbrirJanelaRedimensionarIA(arquivosEmLote); break;
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

    // Entrada de menu separada: IA aparece diretamente no Explorer.
    // Reutiliza SakiRealEsrgan.cs (inalterado), incluindo correcao dos tiles do 0.3.1.
    static void AbrirJanelaRedimensionarIA(List<string> arquivos)
    {
        using (Form form = new Form {
            Text = "SAKI | Resize with AI - " + arquivos.Count + " image(s)",
            ClientSize = new Size(374, 182), FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.White, Font = new Font("Segoe UI", 9f)
        })
        {
            Label lblModo = new Label { Text = "AI model:", Left = 18, Top = 21, Width = 76 };
            ComboBox comboModo = new ComboBox {
                Left = 96, Top = 17, Width = 256, DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboModo.Items.AddRange(new string[] {
                "Real-ESRGAN 2x (Photo)", "Real-ESRGAN 4x (Photo)",
                "Real-ESRGAN 2x (Anime)", "Real-ESRGAN 4x (Anime)"
            });
            comboModo.SelectedIndex = 0;
            Label lblInfo = new Label {
                Text = "Requires Real-ESRGAN and its models (Vulkan).\nAI outputs are new PNG files. Originals are kept.",
                Left = 18, Top = 63, Width = 338, Height = 43,
                ForeColor = Color.FromArgb(116, 38, 69)
            };
            Button btnOk = new Button {
                Text = "Execute", Left = 245, Top = 128, Width = 107, Height = 32,
                DialogResult = DialogResult.OK
            };
            Button btnCancel = new Button {
                Text = "Cancel", Left = 131, Top = 128, Width = 104, Height = 32,
                DialogResult = DialogResult.Cancel
            };
            form.Controls.AddRange(new Control[] { lblModo, comboModo, lblInfo, btnOk, btnCancel });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                int opcao = comboModo.SelectedIndex;
                int escala = (opcao == 0 || opcao == 2) ? 2 : 4;
                bool anime = opcao >= 2;
                // Fechar primeiro o seletor, antes de abrir a janela de progresso.
                form.Hide();
                SakiRealEsrgan.Executar(arquivos, escala, anime);
            }
        }
    }

    // Resize convencional: apenas percentual, largura e altura.
    static void AbrirJanelaRedimensionar(List<string> arquivos)
    {
        Form form = new Form { Text = "Resize " + arquivos.Count + " image(s)", Width = 320, Height = 190, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen };

        Label lblModo = new Label { Text = "Mode:", Left = 20, Top = 20, Width = 60 };
        ComboBox comboModo = new ComboBox { Left = 90, Top = 18, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        comboModo.Items.AddRange(new string[] { "Percentage (%)", "Fixed width (px)", "Fixed height (px)" });
        comboModo.SelectedIndex = 0;

        Label lblValor = new Label { Text = "Value:", Left = 20, Top = 55, Width = 60 };
        TextBox txtValor = new TextBox { Left = 90, Top = 52, Width = 180, Text = "100" };
        Button btnOk = new Button { Text = "Execute", Left = 90, Top = 95, Width = 180, DialogResult = DialogResult.OK };
        form.Controls.AddRange(new Control[] { lblModo, comboModo, lblValor, txtValor, btnOk });
        form.AcceptButton = btnOk;

        if (form.ShowDialog() == DialogResult.OK)
        {
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
