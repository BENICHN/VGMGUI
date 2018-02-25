using BenLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Z.Linq;
using AsyncIO.FileSystem.Extensions;
using static VGMGUI.Settings;

namespace VGMGUI
{
    public class VGMStream
    {
        public static readonly byte[] RIFF = { 82, 73, 70, 70 };
        public static readonly byte[] WAVE = { 87, 65, 86, 69 };

        /// <summary>
        /// Liste des fichiers temporaires.
        /// </summary>
        public static Queue<string> TempFiles { get; set; } = new Queue<string>();

        /// <summary>
        /// Emplacement de l'archive zip où se trouve VLC.
        /// </summary>
        public static string VLCArcPath { get; set; }

        /// <summary>
        /// Supprime les fichiers de <see cref="TempFiles"/> si possible. En cas d'erreur, le fichier est remis dans la file.
        /// </summary>
        /// <returns></returns>
        public static async Task DeleteTMPFiles()
        {
            var filesToEnqueue = new List<string>();
            while (TempFiles.Count > 0)
            {
                string fileName = null;
                try { await FileAsync.TryAndRetryDeleteAsync(fileName = TempFiles.Dequeue()); }
                catch { filesToEnqueue.Add(fileName); }
            }
            foreach (string file in filesToEnqueue) TempFiles.Enqueue(file);
        }

        /// <summary>
        /// Contient les processus VGMStream en cours d'exécution ainsi que leur type.
        /// </summary>
        public static Dictionary<Process, VGMStreamProcessTypes> RunningProcess { get; set; } = new Dictionary<Process, VGMStreamProcessTypes>();

        /// <summary>
        /// À l'aide de vgmstream, obtient le nom du fichier audio au format WAV à partir d'un fichier.
        /// </summary>
        /// <param name="fichier">Le fichier à décoder.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        public static async Task<string> GetStream(Fichier fichier, bool Out = false, CancellationToken cancellationToken = default)
        {
            if (File.Exists(fichier.Path))
            {
                string fn = Path.ChangeExtension(Path.GetTempFileName(), ".wav"); //Nom du fichier temporaire

                Process vgmstreamprocess = new Process() { StartInfo = Out ? StartInfo(fichier.Path, fn, fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : StartInfo(fichier.Path, fn, 1, false) };
                RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Streaming);

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
                    await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                    RunningProcess.Remove(vgmstreamprocess);

                    string s = await vgmstreamprocess.StandardError.ReadToEndAsync().WithCancellation(cancellationToken);

                    if (s.IsEmpty())
                    {
                        TempFiles.Enqueue(fn);
                        return fn;
                    }
                    else
                    {
                        fichier.SetInvalid();
                        return null;
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

                Process vgmstreamprocess = new Process() { StartInfo = StartInfo(fichier.Path, fichier.FinalDestination, fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) };
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

        public static async Task<Fichier> GetFileWithOtherFormats(string fileName, FichierOutData outData = default, CancellationToken cancellationToken = default)
        {
            Fichier result = null;
            if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.GetFile(fileName, outData, cancellationToken)) != null || cancellationToken.IsCancellationRequested) return result;
            else return await GetFile(fileName, outData, cancellationToken);
        }

        public static async Task<string> GetStreamWithOtherFormats(Fichier fichier, bool Out = false, CancellationToken cancellationToken = default)
        {
            string result = null;
            if (IO.ReadBytes(fichier.Path, 0, 4).SequenceEqual(RIFF) && IO.ReadBytes(fichier.Path, 8, 4).SequenceEqual(WAVE)) return fichier.Path;
            else if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.GetStream(fichier, Out, cancellationToken)) != null || cancellationToken.IsCancellationRequested) return result;
            else return await GetStream(fichier, Out, cancellationToken);
        }

        public static async Task<IEnumerable<string>> ConvertFileWithOtherFormats(Fichier fichier, CancellationToken cancellationToken = default)
        {
            IEnumerable<string> result = null;
            if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.ConvertFile(fichier, cancellationToken)) != null || cancellationToken.IsCancellationRequested) return result;
            else return await ConvertFile(fichier, cancellationToken);
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

            if (fileName == null) return null;

            Process vgmstreamprocess = new Process() { StartInfo = StartInfo(fileName, VGMStreamProcessTypes.Metadata) };
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
        public static Fichier GetFile(IEnumerable<string> data, FichierOutData outData = default, bool needMetadataFor = true, bool openStream = true)
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
                                    if (openStream) f.Stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

                                    err = false;
                                }
                                break;
                            case "sample rate":
                                {
                                    value = value.Replace(" Hz", String.Empty);
                                    if (value.IsIntegrer()) { f.SampleRate = int.Parse(value); }
                                }
                                break;
                            case "bitrate":
                                {
                                    value = value.Replace(" kbps", String.Empty);
                                    if (value.IsIntegrer()) { f.Bitrate = int.Parse(value); }
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
            var result = false;
            var tempPath = IO.GetTempDirectory();
            var arcFile = Path.GetTempFileName();
            var address = @"https://raw.githubusercontent.com/bnnm/vgmstream-builds/master/bin/vgmstream-latest-test-u.zip";
            var waitingWindow = new WaitingWindow();
            var client = new WebClient();

            client.DownloadProgressChanged += (sndr, args) =>
            {
                waitingWindow.IsIndeterminate = false;
                waitingWindow.Value = args.ProgressPercentage;
                waitingWindow.State = $"{IO.GetFileSize(args.BytesReceived)} / {IO.GetFileSize(args.TotalBytesToReceive)} - {(100 * (double)args.BytesReceived / args.TotalBytesToReceive).ToString("00.00")} %";
            };

            waitingWindow.SetResourceReference(Window.TitleProperty, "WW_DownloadVGMStream");
            waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
            waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });
            waitingWindow.IsIndeterminate = true;
            waitingWindow.Closing += (sndr, args) => cts.Cancel();
            Task.Run(() => waitingWindow.Dispatcher.Invoke(waitingWindow.ShowDialog));

            try
            {
                await client.DownloadFileTaskAsync(address, arcFile).WithCancellation(cts.Token);

                waitingWindow.Labels.Children.RemoveAt(1);
                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Decompression");
                waitingWindow.IsIndeterminate = true;

                bool ok = false;
                Exception exception = null;

                do
                {
                    try
                    {
                        using (var arc = ZipFile.OpenRead(arcFile))
                        {
                            await arc.ExtractToDirectoryAsync(tempPath, true, cts.Token);
                            await DirectoryAsync.CopyAsync(tempPath, App.VGMStreamFolder, true, cts.Token);
                            ok = result = true;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        exception = ex;
                        result = false;
                    }
                } while (!ok && System.Windows.Forms.MessageBox.Show(exception.Message, App.Str("TT_Error"), System.Windows.Forms.MessageBoxButtons.RetryCancel, System.Windows.Forms.MessageBoxIcon.Error) == System.Windows.Forms.DialogResult.Retry);
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
            finally
            {
                await FileAsync.TryAndRetryDeleteAsync(arcFile, throwEx: false);
                await DirectoryAsync.TryAndRetryDeleteAsync(tempPath, throwEx: false);
            }

            waitingWindow.Close();

            return result;
        }

        /// <summary>
        /// Télécharge et applique la dernière version de ffmpeg.
        /// </summary>
        /// <returns>true s'il n'y a pas eu d'erreur; sinon false.</returns>
        public static async Task<bool> DownloadFFmpeg()
        {
            var cts = new CancellationTokenSource();
            var result = false;
            var tempFile = Path.GetTempFileName();
            var arcFile = Path.GetTempFileName();
            var address = Environment.Is64BitOperatingSystem ? @"https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip" : @"https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip";
            var waitingWindow = new WaitingWindow();
            var client = new WebClient();

            client.DownloadProgressChanged += (sndr, args) =>
            {
                waitingWindow.IsIndeterminate = false;
                waitingWindow.Value = args.ProgressPercentage;
                waitingWindow.State = $"{IO.GetFileSize(args.BytesReceived)} / {IO.GetFileSize(args.TotalBytesToReceive)} - {(100 * (double)args.BytesReceived / args.TotalBytesToReceive).ToString("00.00")} %";
            };

            waitingWindow.SetResourceReference(Window.TitleProperty, "WW_DownloadFFmpeg");
            waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
            waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });
            waitingWindow.IsIndeterminate = true;
            waitingWindow.Closing += (sndr, args) => cts.Cancel();
            Task.Run(() => waitingWindow.Dispatcher.Invoke(waitingWindow.ShowDialog));

            try
            {
                await client.DownloadFileTaskAsync(address, arcFile).WithCancellation(cts.Token);

                waitingWindow.Labels.Children.RemoveAt(1);
                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Decompression");
                waitingWindow.IsIndeterminate = true;

                bool ok = false;
                Exception exception = null;
                do
                {
                    try
                    {
                        using (var arc = ZipFile.OpenRead(arcFile))
                        {
                            var ffFile = await arc.Entries.FirstOrDefaultAsync(entry => entry.FullName == "ffmpeg-latest-win64-static/bin/ffmpeg.exe", cts.Token);
                            Directory.CreateDirectory(App.FFmpegFolder);
                            await ffFile.ExtractToFileAsync(tempFile, true, cts.Token);
                            await FileAsync.CopyAsync(tempFile, App.FFmpegPath, true, cts.Token);
                            ok = result = true;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        exception = ex;
                        result = false;
                    }
                } while (!ok && System.Windows.Forms.MessageBox.Show(exception.Message, App.Str("TT_Error"), System.Windows.Forms.MessageBoxButtons.RetryCancel, System.Windows.Forms.MessageBoxIcon.Error) == System.Windows.Forms.DialogResult.Retry);
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
            finally
            {
                await FileAsync.TryAndRetryDeleteAsync(arcFile, throwEx: false);
                await FileAsync.TryAndRetryDeleteAsync(tempFile, throwEx: false);
            }

            waitingWindow.Close();

            return result;
        }

        /// <summary>
        /// Télécharge et applique la dernière version stable de VLC.
        /// </summary>
        /// <param name="extract">Indique si VLC doit être extrait.</param>
        /// <param name="path">Emplacement de l'archive zip où se trouve VLC. Si null, VLC sera téléchargé.</param>
        /// <returns>true s'il n'y a pas eu d'erreur; sinon false.</returns>
        public static async Task<bool> DownloadVLC(bool extract = false, string path = null)
        {
            var download = !File.Exists(path);
            var cts = new CancellationTokenSource();
            var downloadResult = false;
            var extractResult = false;
            var tempPath = IO.GetTempDirectory();
            var arcFile = Path.GetTempFileName();
            var dirAddress = Environment.Is64BitProcess ? @"http://download.videolan.org/vlc/last/win64/" : @"http://download.videolan.org/vlc/last/win32/";
            var client = new WebClient();

            var waitingWindow = new WaitingWindow();
            waitingWindow.Closing += (sndr, args) => cts.Cancel();

            if (download)
            {
                client.DownloadProgressChanged += (sndr, args) =>
                {
                    waitingWindow.IsIndeterminate = false;
                    waitingWindow.Value = args.ProgressPercentage;
                    waitingWindow.State = $"{IO.GetFileSize(args.BytesReceived)} / {IO.GetFileSize(args.TotalBytesToReceive)} - {(100 * (double)args.BytesReceived / args.TotalBytesToReceive).ToString("00.00")} %";
                };

                waitingWindow.SetResourceReference(Window.TitleProperty, "WW_DownloadVLC");
                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Search");
                waitingWindow.IsIndeterminate = true;
            }

            Task.Run(() => waitingWindow.Dispatcher.Invoke(waitingWindow.ShowDialog));

            try
            {
                if (download)
                {
                    var files = await new WebClient().DownloadStringTaskAsync(new Uri(dirAddress));
                    var addresses = new Regex("<a href=\".+\\.zip\">").Matches(files);

                    if (addresses.Count == 0) return false;

                    var address = dirAddress + addresses[0].Value.Replace(new[] { "<a href=\"", "\">" }, String.Empty);

                    waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
                    waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });

                    await client.DownloadFileTaskAsync(address, arcFile).WithCancellation(cts.Token);

                    downloadResult = true;
                }

                if (extract)
                {
                    bool ok = false;
                    Exception exception = null;

                    waitingWindow.SetResourceReference(Window.TitleProperty, "WW_VLCExtraction");
                    waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Decompression");
                    if (download) waitingWindow.Labels.Children.RemoveAt(1);
                    waitingWindow.IsIndeterminate = true;

                    do
                    {
                        try
                        {
                            using (var arc = ZipFile.OpenRead(download ? arcFile : path))
                            {
                                var pluginsTMPFolder = Path.Combine(tempPath, "plugins");
                                var libvlcTMPFile = Path.Combine(tempPath, "libvlc.dll");
                                var libvlccoreTMPFile = Path.Combine(tempPath, "libvlccore.dll");

                                var pluginsFolder = await arc.Entries.FirstOrDefaultAsync(entry => entry.FullName == "vlc-2.2.8/plugins/", cts.Token);
                                var libvlcFile = await arc.Entries.FirstOrDefaultAsync(entry => entry.FullName == "vlc-2.2.8/libvlc.dll", cts.Token);
                                var libvlccoreFile = await arc.Entries.FirstOrDefaultAsync(entry => entry.FullName == "vlc-2.2.8/libvlccore.dll", cts.Token);

                                await pluginsFolder.ExtractToDirectoryAsync(pluginsTMPFolder, true, cts.Token);
                                await libvlcFile.ExtractToFileAsync(libvlcTMPFile, true, cts.Token);
                                await libvlccoreFile.ExtractToFileAsync(libvlccoreTMPFile, true, cts.Token);

                                if (Directory.Exists(App.VLCFolder)) await DirectoryAsync.TryAndRetryDeleteAsync(App.VLCFolder);
                                Directory.CreateDirectory(App.VLCFolder);

                                await DirectoryAsync.CopyAsync(pluginsTMPFolder, Path.Combine(App.VLCFolder, "plugins"), true, cts.Token);
                                await FileAsync.CopyAsync(libvlcTMPFile, Path.Combine(App.VLCFolder, "libvlc.dll"), true, cts.Token);
                                await FileAsync.CopyAsync(libvlccoreTMPFile, Path.Combine(App.VLCFolder, "libvlccore.dll"), true, cts.Token);

                                ok = extractResult = true;
                            }
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            exception = ex;
                            extractResult = false;
                        }
                    } while (!ok && System.Windows.Forms.MessageBox.Show(exception.Message, App.Str("TT_Error"), System.Windows.Forms.MessageBoxButtons.RetryCancel, System.Windows.Forms.MessageBoxIcon.Error) == System.Windows.Forms.DialogResult.Retry);
                }
            }
            catch (OperationCanceledException)
            {
                if (download) client.CancelAsync();
                downloadResult = extractResult = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                downloadResult = extractResult = false;
            }
            finally
            {
                if (downloadResult && !extract) VLCArcPath = arcFile;
                else await FileAsync.TryAndRetryDeleteAsync(arcFile, throwEx: false);

                await DirectoryAsync.TryAndRetryDeleteAsync(tempPath, throwEx: false);
            }

            waitingWindow.Close();

            return extract ? extractResult : downloadResult;
        }

        /// <summary>
        /// VGMStream wrapper method.
        /// </summary>
        public static ProcessStartInfo StartInfo(string inFile, string outFile = null, int loopCount = 2, bool fadeOut = true, double fadeDelay = 0, double fadeTime = 10, bool startEndLoop = false) => new ProcessStartInfo(App.VGMStreamPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            Arguments = $"-o {(Uri.IsWellFormedUriString(new Uri(outFile).AbsoluteUri, UriKind.RelativeOrAbsolute) ? outFile : Path.ChangeExtension(inFile, "wav"))}{(startEndLoop ? " -E" : "")} -l {loopCount} {(fadeOut ? $"-f {fadeTime.ToString(Literal.DecimalSeparatorPoint)} -d {fadeDelay.ToString(Literal.DecimalSeparatorPoint)}" : "-F")} \"{inFile}\""
        };

        /// <summary>
        /// VGMStream wrapper method.
        /// </summary>
        public static ProcessStartInfo StartInfo(string inFile, VGMStreamProcessTypes processType, int loopCount = 2, bool fadeOut = true, double fadeDelay = 0, double fadeTime = 10, bool startEndLoop = false)
        {
            switch (processType)
            {
                case VGMStreamProcessTypes.Conversion:
                    return StartInfo(inFile, null, loopCount, fadeOut, fadeDelay, fadeTime, startEndLoop);
                case VGMStreamProcessTypes.Streaming:
                    return new ProcessStartInfo(App.VGMStreamPath)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments = $"-P {(startEndLoop ? " -E" : String.Empty)} -l {loopCount} {(fadeOut ? $"-f {fadeTime.ToString(Literal.DecimalSeparatorPoint)} -d {fadeDelay.ToString(Literal.DecimalSeparatorPoint)}" : "-F")} \"{inFile}\""
                    };
                case VGMStreamProcessTypes.Metadata:
                    return new ProcessStartInfo(App.VGMStreamPath)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments = $"-m \"{inFile}\""
                    };
                default: return null;
            }
        }
    }

    public enum VGMStreamProcessTypes { Conversion, Streaming, Metadata }
}
