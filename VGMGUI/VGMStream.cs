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
using Vlc.DotNet.Core;

namespace VGMGUI
{
    public class VGMStream
    {
        public static readonly byte[] RIFF = { 82, 73, 70, 70 };
        public static readonly byte[] WAVE = { 87, 65, 86, 69 };

        public static int ScanningCount { get; private set; }
        public static int StreamingCount { get; private set; }
        public static int ConversionCount { get; private set; }
        public static int DownloadCount { get; private set; }

        public static bool IsScanning => ScanningCount + DKCTFCSMP.ScanningCount > 0;
        public static bool IsStreaming => StreamingCount + DKCTFCSMP.StreamingCount > 0;
        public static bool IsConverting => ConversionCount + DKCTFCSMP.ConversionCount > 0;
        public static bool IsDownloading => DownloadCount > 0;

        public static CancellationTokenSource VLCCTS { get; private set; }
        public static CancellationTokenSource VGMStreamCTS { get; private set; }

        /// <summary>
        /// Liste des fichiers temporaires.
        /// </summary>
        public static Queue<KeyValuePair<string, VGMStreamProcessTypes?>> TempFiles { get; private set; } = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>();

        public static string CreateTempFile(string extension, VGMStreamProcessTypes? type, bool enqueue = true)
        {
            string fn;
            while (File.Exists(fn = Path.ChangeExtension(Path.GetTempFileName(), extension))) continue;
            if (enqueue) TempFiles.Enqueue(new KeyValuePair<string, VGMStreamProcessTypes?>(fn, type));
            return fn;
        }

        /// <summary>
        /// Supprime les fichiers de <see cref="TempFiles"/> si possible. En cas d'erreur, le fichier est remis dans la file.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> DeleteTempFiles(bool cache)
        {
            var filesToEnqueue = new List<KeyValuePair<string, VGMStreamProcessTypes?>>();
            var baseQueue = cache ? new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(TempFiles) : TempFiles;
            var queue = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(baseQueue);

            while (queue.Count > 0)
            {
                KeyValuePair<string, VGMStreamProcessTypes?> kvp = default;
                if (!(await FileAsync.TryDeleteAsync((kvp = queue.Dequeue()).Key)).Result) filesToEnqueue.Add(kvp);
            }

            if (cache) TempFiles = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(kvp => baseQueue.Contains(kvp) == filesToEnqueue.Contains(kvp)));
            else foreach (var file in filesToEnqueue) queue.Enqueue(file);

            return filesToEnqueue.IsNullOrEmpty();
        }

        public static Task<bool> DeleteTempFilesIfNotUsed() => DeleteTempFilesByTypeIfNotUsed(VGMStreamProcessTypes.Conversion, VGMStreamProcessTypes.Streaming, VGMStreamProcessTypes.Metadata, null);

        public static async Task<bool> DeleteTempFilesByName(params string[] files)
        {
            bool result = true;
            var filesToEnqueue = new List<string>();

            foreach (string file in files)
            {
                if (!(await FileAsync.TryDeleteAsync(file)).Result)
                {
                    filesToEnqueue.Add(file);
                    result = false;
                }
            }

            TempFiles = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(kvp => files.Contains(kvp.Key) == filesToEnqueue.Contains(kvp.Key)));

            return result;
        }

        /// <summary>
        /// Supprime les fichiers de <see cref="TempFiles"/> si possible. En cas d'erreur, le fichier est remis dans la file.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> DeleteTempFilesByExtension(params string[] extensions)
        {
            var filesToEnqueue = new List<KeyValuePair<string, VGMStreamProcessTypes?>>();
            var baseQueue = extensions.IsNullOrEmpty() ? new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(TempFiles) : new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(async kvp => await extensions.AnyAsync(extension => Path.GetExtension(kvp.Key).TrimStart('.').Equals(extension.TrimStart('.')))));
            var queue = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(baseQueue);

            while (queue.Count > 0)
            {
                KeyValuePair<string, VGMStreamProcessTypes?> kvp = default;
                if (!(await FileAsync.TryDeleteAsync((kvp = queue.Dequeue()).Key)).Result) filesToEnqueue.Add(kvp);
            }

            TempFiles = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(kvp => baseQueue.Contains(kvp) == filesToEnqueue.Contains(kvp)));

            return filesToEnqueue.IsNullOrEmpty();
        }

        public static async Task<bool> DeleteTempFilesByType(params VGMStreamProcessTypes?[] types)
        {
            var filesToEnqueue = new List<KeyValuePair<string, VGMStreamProcessTypes?>>();
            var baseQueue = types.IsNullOrEmpty() ? new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(TempFiles) : new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(async kvp => await types.AnyAsync(type => kvp.Value.Equals(type))));
            var queue = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(baseQueue);

            while (queue.Count > 0)
            {
                KeyValuePair<string, VGMStreamProcessTypes?> kvp = default;
                if (!(await FileAsync.TryDeleteAsync((kvp = queue.Dequeue()).Key)).Result) filesToEnqueue.Add(kvp);
            }

            TempFiles = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(kvp => baseQueue.Contains(kvp) == filesToEnqueue.Contains(kvp)));

            return filesToEnqueue.IsNullOrEmpty();
        }

        public static async Task<bool> DeleteTempFilesByTypeIfNotUsed(params VGMStreamProcessTypes?[] types)
        {
            var filesToEnqueue = new List<KeyValuePair<string, VGMStreamProcessTypes?>>();
            var baseQueue = types.IsNullOrEmpty() ?
                new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(TempFiles) :
                new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(async kvp => await types.AnyAsync(type =>
            {
                if (kvp.Value.Equals(type))
                {
                    switch (type)
                    {
                        case VGMStreamProcessTypes.Conversion:
                            return !IsConverting;
                        case VGMStreamProcessTypes.Metadata:
                            return !IsScanning;
                        case VGMStreamProcessTypes.Streaming:
                            return !IsStreaming;
                        case null:
                            return !IsDownloading;
                    }
                }
                return false;
            })));
            var queue = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(baseQueue);

            while (queue.Count > 0)
            {
                KeyValuePair<string, VGMStreamProcessTypes?> kvp = default;
                if (!(await FileAsync.TryDeleteAsync((kvp = queue.Dequeue()).Key)).Result) filesToEnqueue.Add(kvp);
            }

            TempFiles = new Queue<KeyValuePair<string, VGMStreamProcessTypes?>>(await TempFiles.WhereAsync(kvp => baseQueue.Contains(kvp) == filesToEnqueue.Contains(kvp)));

            return filesToEnqueue.IsNullOrEmpty();
        }

        /// <summary>
        /// Emplacement de l'archive zip où se trouve VLC.
        /// </summary>
        public static string VLCArcPath { get; set; }

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
        public static async Task<Stream> GetStream(Fichier fichier, bool useFile, bool Out = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested || !File.Exists(fichier.Path)) return null;

            string fn = useFile ? CreateTempFile("wav", VGMStreamProcessTypes.Streaming) : null; //Nom du fichier temporaire

            try
            {
                StreamingCount++;
                Process vgmstreamprocess = new Process() { StartInfo = useFile ? (Out ? StartInfo(fichier.Path, fn, fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : StartInfo(fichier.Path, fn, 1, false)) : (Out ? StartInfo(fichier.Path, VGMStreamProcessTypes.Streaming, fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : StartInfo(fichier.Path, VGMStreamProcessTypes.Streaming, 1, false)) };
                RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Streaming);

                if (cancellationToken.IsCancellationRequested || !File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null;

                cancellationToken.Register(() =>
                {
                    vgmstreamprocess.TryKill();
                    if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess);
                });

                var StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                if (useFile) await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess);

                if (cancellationToken.IsCancellationRequested) return null;

                if (!useFile) return vgmstreamprocess.StandardOutput.BaseStream;
                else if (vgmstreamprocess.ExitCode == 0) return File.OpenRead(fn);
                else
                {
                    fichier.SetInvalid();
                    return null;
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally { StreamingCount--; }
        }

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>true si la conversion a réussi; sinon false.</returns>
        public static async Task<string[]> ConvertFile(Fichier fichier, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            await pauseToken.WaitWhilePausedAsync();

            if (cancellationToken.IsCancellationRequested || !File.Exists(fichier.Path)) return null;

            bool success = false;

            Process vgmstreamprocess = new Process() { StartInfo = StartInfo(fichier.Path, fichier.FinalDestination, fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) };
            RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Conversion);

            try
            {
                ConversionCount++;
                fichier.OriginalState = "FSTATE_Conversion";

                await pauseToken.WaitWhilePausedAsync();

                TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                RunningProcess.Remove(vgmstreamprocess);

                await pauseToken.WaitWhilePausedAsync();

                if (!cancellationToken.IsCancellationRequested && vgmstreamprocess.ExitCode == 0)
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

                if (success) fichier.OriginalState = "FSTATE_Completed";
                else if (!cancellationToken.IsCancellationRequested) fichier.SetInvalid();

                ConversionCount--;
            }
        }

        public static async Task<Fichier> GetFileWithOtherFormats(string fileName, FichierOutData outData = default, CancellationToken cancellationToken = default)
        {
            Fichier result = null;
            try
            {
                if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.GetFile(fileName, outData, cancellationToken)) != null || cancellationToken.IsCancellationRequested) return result;
                else return result = await GetFile(fileName, outData, cancellationToken);
            }
            finally
            {
                if (result != null && result.SamplesToPlay == -1)
                {
                    result.FadeDelay = 0;
                    result.FadeTime = 10;
                    result.LoopCount = 2;
                    result.StartEndLoop = false;
                    Fichier.Overflow = true;
                }
            }
        }

        public static async Task<Stream> GetStreamWithOtherFormats(Fichier fichier, bool useFile, bool Out = false, CancellationToken cancellationToken = default)
        {
            Stream result = null;
            if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.GetStream(fichier, Out, cancellationToken)) != null || cancellationToken.IsCancellationRequested) return result;
            else return result = await GetStream(fichier, useFile, Out, cancellationToken);
        }

        public static async Task<IEnumerable<string>> ConvertFileWithOtherFormats(Fichier fichier, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            CancellationTokenRegistration registration = cancellationToken.Register(fichier.Cancel);
            IEnumerable<string> result = null;

            try
            {
                if (AdditionalFormats.DKCTFCSMP && (result = await DKCTFCSMP.ConvertFile(fichier, false, fichier.CancellationToken, pauseToken)) != null || fichier.CancellationToken.IsCancellationRequested) return result;
                else return result = await ConvertFile(fichier, fichier.CancellationToken, pauseToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested) fichier.OriginalState = "FSTATE_Canceled";
                else if (fichier.CancellationToken.IsCancellationRequested) fichier.OriginalState = "FSTATE_Skipped";
                fichier.ResetCancellation();
                registration.TryDispose();
            }
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
                ScanningCount++;
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
            finally
            {
                if (RunningProcess.ContainsKey(vgmstreamprocess)) RunningProcess.Remove(vgmstreamprocess);
                ScanningCount--;
            }
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
            VGMStreamCTS = new CancellationTokenSource();
            var result = false;
            var tempPath = IO.GetTempDirectory();
            var arcFile = CreateTempFile("zip", null);
            var address = @"https://raw.githubusercontent.com/bnnm/vgmstream-builds/master/bin/vgmstream-latest-test-u.zip";
            var waitingWindow = new WaitingWindow();
            var client = new WebClient();

            client.DownloadProgressChanged += (sndr, args) =>
            {
                waitingWindow.IsIndeterminate = false;
                waitingWindow.Value = args.ProgressPercentage;
                waitingWindow.State = $"{IO.GetFileSize(args.BytesReceived, "0.00")} / {IO.GetFileSize(args.TotalBytesToReceive, "0.00")} - {(100 * (double)args.BytesReceived / args.TotalBytesToReceive).ToString("00.00")} %";
            };

            waitingWindow.SetResourceReference(Window.TitleProperty, "WW_DownloadVGMStream");
            waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
            waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });
            waitingWindow.IsIndeterminate = true;
            waitingWindow.Closing += (sndr, args) => VGMStreamCTS.Cancel();
            Task.Run(() => waitingWindow.Dispatcher.Invoke(waitingWindow.ShowDialog));

            try
            {
                DownloadCount++;
                await client.DownloadFileTaskAsync(address, arcFile).WithCancellation(VGMStreamCTS.Token);

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
                            await arc.ExtractToDirectoryAsync(tempPath, true, VGMStreamCTS.Token);
                            await DirectoryAsync.CopyAsync(tempPath, App.VGMStreamFolder, true, VGMStreamCTS.Token);
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
                await DeleteTempFilesByName(arcFile);
                await DirectoryAsync.TryAndRetryDeleteAsync(tempPath, throwEx: false);
                DownloadCount--;
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
            VLCCTS = new CancellationTokenSource();
            var downloadResult = false;
            var extractResult = false;
            var tempPath = IO.GetTempDirectory();
            var arcFile = CreateTempFile("zip", null, false);
            var dirAddress = Environment.Is64BitProcess ? @"http://download.videolan.org/vlc/last/win64/" : @"http://download.videolan.org/vlc/last/win32/";
            var client = new WebClient();

            var waitingWindow = new WaitingWindow();
            waitingWindow.Closing += (sndr, args) => VLCCTS.Cancel();

            if (download)
            {
                client.DownloadProgressChanged += (sndr, args) =>
                {
                    waitingWindow.IsIndeterminate = false;
                    waitingWindow.Value = args.ProgressPercentage;
                    waitingWindow.State = $"{IO.GetFileSize(args.BytesReceived, "0.00")} / {IO.GetFileSize(args.TotalBytesToReceive, "0.00")} - {(100 * (double)args.BytesReceived / args.TotalBytesToReceive).ToString("00.00")} %";
                };

                waitingWindow.SetResourceReference(Window.TitleProperty, "WW_DownloadVLC");
                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Search");
                waitingWindow.IsIndeterminate = true;
            }

            waitingWindow.Show();

            try
            {
                DownloadCount++;
                if (download)
                {
                    var files = await new WebClient().DownloadStringTaskAsync(new Uri(dirAddress));
                    var addresses = new Regex("<a href=\".+\\.zip\">").Matches(files);

                    if (addresses.Count == 0) return false;

                    var address = dirAddress + addresses[0].Value.Replace(new[] { "<a href=\"", "\">" }, String.Empty);

                    waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_DownloadOf");
                    waitingWindow.Labels.Children.Add(new TextBox() { IsReadOnly = true, BorderThickness = new Thickness(0), Text = address });

                    await client.DownloadFileTaskAsync(address, arcFile).WithCancellation(VLCCTS.Token);

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

                                var pluginsFolder = await arc.Entries.FirstOrDefaultAsync(entry => Regex.IsMatch(entry.FullName, "^vlc-(\\d|\\.)+/plugins/$"), VLCCTS.Token);
                                var libvlcFile = await arc.Entries.FirstOrDefaultAsync(entry => Regex.IsMatch(entry.FullName, "^vlc-(\\d|\\.)+/libvlc.dll$"), VLCCTS.Token);
                                var libvlccoreFile = await arc.Entries.FirstOrDefaultAsync(entry => Regex.IsMatch(entry.FullName, "^vlc-(\\d|\\.)+/libvlccore.dll$"), VLCCTS.Token);

                                await pluginsFolder.ExtractToDirectoryAsync(pluginsTMPFolder, true, VLCCTS.Token);
                                await libvlcFile.ExtractToFileAsync(libvlcTMPFile, true, VLCCTS.Token);
                                await libvlccoreFile.ExtractToFileAsync(libvlccoreTMPFile, true, VLCCTS.Token);

                                if (Directory.Exists(App.VLCFolder)) await DirectoryAsync.TryAndRetryDeleteAsync(App.VLCFolder);
                                Directory.CreateDirectory(App.VLCFolder);

                                await DirectoryAsync.CopyAsync(pluginsTMPFolder, Path.Combine(App.VLCFolder, "plugins"), true, VLCCTS.Token);
                                await FileAsync.CopyAsync(libvlcTMPFile, Path.Combine(App.VLCFolder, "libvlc.dll"), true, VLCCTS.Token);
                                await FileAsync.CopyAsync(libvlccoreTMPFile, Path.Combine(App.VLCFolder, "libvlccore.dll"), true, VLCCTS.Token);

                                waitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_VLCPluginsCaching");
                                await Task.Run(() => new VlcMediaPlayer(new DirectoryInfo(App.VLCFolder), new[] { "--reset-plugins-cache" }).TryDispose()); //Create plugins cache

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
                DownloadCount--;
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
            Arguments = $"-o \"{(Uri.IsWellFormedUriString(new Uri(outFile).AbsoluteUri, UriKind.RelativeOrAbsolute) ? outFile : Path.ChangeExtension(inFile, "wav"))}\"{(startEndLoop ? " -E" : String.Empty)} -l {loopCount} {(fadeOut ? $"-f {fadeTime.ToString(Literal.DecimalSeparatorPoint)} -d {fadeDelay.ToString(Literal.DecimalSeparatorPoint)}" : "-F")} \"{inFile}\""
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
                        Arguments = $"-P{(startEndLoop ? " -E" : String.Empty)} -l {loopCount} {(fadeOut ? $"-f {fadeTime.ToString(Literal.DecimalSeparatorPoint)} -d {fadeDelay.ToString(Literal.DecimalSeparatorPoint)}" : "-F")} \"{inFile}\""
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
