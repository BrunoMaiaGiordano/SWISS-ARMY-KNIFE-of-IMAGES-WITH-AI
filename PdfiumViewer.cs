using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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
