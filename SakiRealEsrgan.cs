using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

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
