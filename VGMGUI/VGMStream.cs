using BenLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VGMGUI
{
    public class VGMStream
    {
        /// <summary>
        /// Contient les processus VGMStream en cours d'exécution ainsi que leur type.
        /// </summary>
        public static Dictionary<Process, VGMStreamProcessTypes> RunningProcess { get; set; } = new Dictionary<Process, VGMStreamProcessTypes>();

        /// <summary>
        /// À l'aide de vgmstream, obtient un Stream contenant des données audio au format WAV à partir d'un fichier.
        /// </summary>
        /// <param name="fichier">Le fichier à décoder.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <param name="useFile">true si un fichier temporaire doit être utilisé; false pour utiliser un <see cref="MemoryStream"/>.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        public static async Task<Stream> GetStream(Fichier fichier, bool Out = false, bool useFile = true, CancellationToken cancellationToken = default)
        {
            if (File.Exists(fichier.Path))
            {
                string fn = String.Empty; //Nom du fichier temporaire
                byte[] b = null; //Données du MemoryStream
                ProcessStartInfo vgmstreaminfo = new ProcessStartInfo(App.VGMStreamPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = !Out ?
                    "-p -i \"" + fichier.Path + "\"" :
                    "-p" + (fichier.StartEndLoop ? " -E" : "") + " -l " + fichier.LoopCount + (fichier.FadeOut ? " -f " + fichier.FadeTime.ToString(Literal.DecimalSeparatorPoint) + " -d " + fichier.FadeDelay.ToString(Literal.DecimalSeparatorPoint) : " -F") + " \"" + fichier.Path + "\""
                };

                Process vgmstreamprocess = new Process() { StartInfo = vgmstreaminfo };
                RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Streaming);

                if (useFile) vgmstreaminfo.Arguments = vgmstreaminfo.Arguments.Replace("-p ", "-o " + (fn = Path.GetTempFileName()) + " ");

                if (File.Exists(App.VGMStreamPath) || await App.AskVGMStream())
                {
                    TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                    if (!StartResult.Result) //N'a pas pu être démarré
                    {
                        if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }
                else return null;

                try
                {
                    if (!useFile) //MemoryStream
                    {
                        string s = await vgmstreamprocess.StandardOutput.ReadToEndAsync().WithCancellation(cancellationToken);

                        RunningProcess.Remove(vgmstreamprocess);

                        if (s.Length > 4 && s.Substring(0, 4) == "RIFF")
                        {
                            b = await s.ToByteArrayAsync(Encoding.Default, cancellationToken);
                            return new MemoryStream(b);
                        }
                        else
                        {
                            fichier.SetInvalid();
                            return null;
                        }
                    }
                    else //FileStream
                    {
                        await vgmstreamprocess.WaitForExitAsync(cancellationToken);

                        RunningProcess.Remove(vgmstreamprocess);

                        string s = await vgmstreamprocess.StandardError.ReadToEndAsync().WithCancellation(cancellationToken);

                        if (s.IsEmpty()) return new FileStream(fn, FileMode.Open);
                        else
                        {
                            fichier.SetInvalid();
                            return null;
                        }
                    }
                }
                catch (OperationCanceledException) { vgmstreamprocess.TryKill(); }
                finally { if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess); }

                return null;
            }
            else return null;
        }

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>true si la conversion a réussi; sinon false.</returns>
        public static async Task<IEnumerable<string>> ConvertFile(Fichier fichier, CancellationToken cancellationToken = default)
        {
            if (File.Exists(fichier.Path))
            {
                bool success = false;

                ProcessStartInfo vgmstreaminfo = new ProcessStartInfo(App.VGMStreamPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments =
                    "-o \"" + fichier.FinalDestination + "\"" +
                    (fichier.StartEndLoop ? " -E" : String.Empty) +
                    " -l " + fichier.LoopCount +
                    (fichier.FadeOut ?
                    " -f " + fichier.FadeTime.ToString(Literal.DecimalSeparatorPoint) + " -d " + fichier.FadeDelay.ToString(Literal.DecimalSeparatorPoint) :
                    " -F") +
                    " \"" + fichier.Path + "\""
                };

                Process vgmstreamprocess = new Process() { StartInfo = vgmstreaminfo };
                RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Conversion);

                try
                {
                    fichier.OriginalState = "FSTATE_Conversion";

                    TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                    if (!StartResult.Result) //N'a pas pu être démarré
                    {
                        if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                    RunningProcess.Remove(vgmstreamprocess);

                    string err = await vgmstreamprocess.StandardError.ReadToEndAsync().WithCancellation(cancellationToken);

                    if (err.IsEmpty())
                    {
                        success = true;
                        return await vgmstreamprocess.StandardOutput.ReadAllLinesAsync().WithCancellation(cancellationToken);
                    }
                    else return null;
                }
                catch (OperationCanceledException)
                {
                    vgmstreamprocess.TryKill();
                    return null;
                }
                finally
                {
                    if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (success) fichier.OriginalState = "FSTATE_Completed";
                        else fichier.SetInvalid();
                    }
                    else if (!success) fichier.OriginalState = "FSTATE_Canceled";
                }
            }
            else return null;
        }

        /// <summary>
        /// À partir d'un nom de fichier, obtient un fichier analysé.
        /// </summary>
        /// <param name="fileName">Nom du fichier.</param>
        /// <param name="outData">Données complémentaires pour le fichier.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le fichier analysé.</returns>
        public static async Task<Fichier> GetFile(string fileName, FichierOutData outData = default, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(fileName)) return new Fichier(null) { Invalid = true, OriginalState = "ERR_FileNotFound" };
            ProcessStartInfo vgmstreaminfo = new ProcessStartInfo(App.VGMStreamPath) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };

            if (fileName != null) vgmstreaminfo.Arguments = "-m \"" + fileName + "\"";
            else return null;

            Process vgmstreamprocess = new Process() { StartInfo = vgmstreaminfo };
            RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Metadata);

            try
            {
                TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                RunningProcess.Remove(vgmstreamprocess);

                string[] s = vgmstreamprocess.StandardOutput.ReadAllLines();

                return GetFile(s, outData);
            }
            catch (OperationCanceledException)
            {
                vgmstreamprocess.TryKill();
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally { if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess); }
        }

        /// <summary>
        /// À partir de données vgmstream, obtient un fichier analysé.
        /// </summary>
        /// <param name="data">Données vgmstream.</param>
        /// <param name="outData">Données complémentaires pour le fichier.</param>
        /// <param name="needMetadataFor">Indique si les données doivent contenir le nom du fichier.</param>
        /// <returns>Le fichier analysé.</returns>
        public static Fichier GetFile(IEnumerable<string> data, FichierOutData outData = default, bool needMetadataFor = true)
        {
            bool err = true; //s contains "metadata for " ?
            string[] linedata = null;
            Fichier f = new Fichier(null, outData) { Analyzed = true };

            foreach (string line in data)
            {
                if (line != null)
                {
                    if (err && line.Contains("metadata for "))
                    {
                        linedata = new string[2] { "metadata for", line.Replace("metadata for ", String.Empty) };
                    }
                    else
                    {
                        linedata = line.Split(new[] { ": " }, 2, StringSplitOptions.RemoveEmptyEntries);
                    }

                    if (linedata.Length == 2)
                    {
                        string value = linedata[1];

                        switch (linedata[0])
                        {
                            case "metadata for":
                                {
                                    string filename = value;
                                    FileInfo fi = new FileInfo(filename);

                                    f.Path = filename;
                                    f.Stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

                                    err = false;
                                }
                                break;
                            case "sample rate":
                                {
                                    value = value.TrimEnd(' ', 'H', 'z');
                                    if (value.IsIntegrer()) { f.SampleRate = int.Parse(value); }
                                }
                                break;
                            case "channels":
                                {
                                    if (value.IsIntegrer()) { f.Channels = int.Parse(value); }
                                }
                                break;
                            case "interleave":
                                {
                                    try { f.Interleave = Convert.ToInt32(value.Substring(0, value.IndexOf(" bytes")), 16); }
                                    catch { }
                                }
                                break;
                            case "layout":
                                {
                                    f.Layout = value;
                                }
                                break;
                            case "loop start":
                                {
                                    f.LoopFlag = true;
                                    int index = value.IndexOf(" ");
                                    f.LoopStart = index > -1 ? value.Substring(0, index).ToInt() ?? 0 : 0;
                                }
                                break;
                            case "loop end":
                                {
                                    f.LoopFlag = true;
                                    int index = value.IndexOf(" ");
                                    f.LoopEnd = index > -1 ? value.Substring(0, index).ToInt() ?? 0 : 0;
                                }
                                break;
                            case "stream total samples":
                                {
                                    int index = value.IndexOf(" ");
                                    f.TotalSamples = index > -1 ? value.Substring(0, index).ToInt() ?? 0 : 0;
                                }
                                break;
                            case "encoding":
                                {
                                    f.Encoding = value;
                                }
                                break;
                            case "metadata from":
                                {
                                    if (value == "FFmpeg supported file format") value = "FMT_FFmpeg";
                                    f.OriginalFormat = value.Replace(new[] { " Header", " header" }, String.Empty);
                                }
                                break;
                        }
                    }
                }
            }

            return !err || !needMetadataFor ? f : null;
        }

        /// <summary>
        /// Télécharge et applique la dernière version de vgmstream.
        /// </summary>
        /// <returns>true s'il n'y a pas eu d'erreur; sinon false.</returns>
        public static async Task<bool> DownloadVGMStream()
        {
            var cts = new CancellationTokenSource();
            var result = true;
            var file = Path.GetTempFileName();
            var address = @"https://raw.githubusercontent.com/bnnm/vgmstream-builds/master/bin/vgmstream-latest-test-u.zip";
            var waitingWindow = new WaitingWindow();
            var client = new WebClient();

            client.DownloadProgressChanged += (sndr, args) =>
            {
                waitingWindow.Bar.IsIndeterminate = false;
                waitingWindow.Value = args.ProgressPercentage;
            };

            waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
            waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });
            waitingWindow.Bar.IsIndeterminate = true;
            waitingWindow.Closing += (sndr, args) => cts.Cancel();
            Task.Run(() => waitingWindow.Dispatcher.Invoke(waitingWindow.ShowDialog));

            try
            {
                await client.DownloadFileTaskAsync(address, file).WithCancellation(cts.Token);

                waitingWindow.Labels.Children.RemoveAt(1);
                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Decompression");

                await Task.Run(() => new ZipArchive(new FileStream(file, FileMode.Open)).ExtractToDirectory(App.VGMStreamFolder, true), cts.Token);
            }
            catch (OperationCanceledException)
            {
                client.CancelAsync();
                result = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                result = false;
            }
            finally { await Threading.MultipleAttempts(() => File.Delete(file), throwEx: false); }

            waitingWindow.Close();

            return result;
        }
    }

    public enum VGMStreamProcessTypes { Conversion, Streaming, Metadata }
}
