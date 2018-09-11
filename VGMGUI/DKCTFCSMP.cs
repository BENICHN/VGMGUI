using BenLib;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Z.Linq;

namespace VGMGUI
{
    public class DKCTFCSMP
    {
        public const int RFRM = 0x5246524D;
        public const int CSMP = 0x43534D50;
        public const int LABL = 0x4C41424C;

        public static int ScanningCount { get; private set; }
        public static int StreamingCount { get; private set; }
        public static int ConversionCount { get; private set; }

        /// <summary>
        /// À partir d'un nom de fichier, obtient un fichier analysé uniquement s'il possède l'en-tête RFRM CSMP.
        /// </summary>
        /// <param name="fileName">Nom du fichier.</param>
        /// <param name="outData">Données complémentaires pour le fichier.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le fichier analysé.</returns>
        public static async Task<Fichier> GetFile(string fileName, FichierOutData outData = default, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested || !File.Exists(fileName)) return null;

            IEnumerable<string> filesToDelete = null;

            try
            {
                ScanningCount++;
                var (dspFileNames, channelsCount) = await GetDSPFiles(fileName, VGMStreamProcessTypes.Metadata, 1, cancellationToken);
                if ((filesToDelete = dspFileNames) == null) return null;

                Process vgmstreamprocess = new Process() { StartInfo = VGMStream.StartInfo(dspFileNames[0], VGMStreamProcessTypes.Metadata) };
                VGMStream.RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Metadata);

                cancellationToken.Register(() =>
                {
                    vgmstreamprocess.TryKill();
                    if (VGMStream.RunningProcess.ContainsKey(vgmstreamprocess)) VGMStream.RunningProcess.Remove(vgmstreamprocess);
                });

                TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                if (VGMStream.RunningProcess.ContainsKey(vgmstreamprocess)) VGMStream.RunningProcess.Remove(vgmstreamprocess);

                if (cancellationToken.IsCancellationRequested) return null;

                string[] s = vgmstreamprocess.StandardOutput.ReadAllLines();

                if (s.IsNullOrEmpty()) return null;

                var fichier = VGMStream.GetFile(s, outData, false, false);

                if (cancellationToken.IsCancellationRequested) return null;
                else if (fichier.Invalid) return fichier;
                else
                {
                    fichier.Path = fileName;
                    fichier.OriginalFormat = "Retro Studios DKCTF CSMP";
                    fichier.Channels = channelsCount;
                    fichier.Bitrate *= channelsCount;
                    return fichier;
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested) await VGMStream.DeleteTempFilesByName(filesToDelete);
                ScanningCount--;
            }
        }

        /// <summary>
        /// Obtient le nom du fichier audio au format WAV à partir d'un fichier RFRM CSMP.
        /// </summary>
        /// <param name="fichier">Le fichier à décoder.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        public static async Task<Stream> GetStream(Fichier fichier, bool Out = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested || !File.Exists(fichier.Path)) return null;

            IEnumerable<string> filesToDelete = null;
            string fn = await VGMStream.CreateTempFileAsync("wav", VGMStreamProcessTypes.Streaming); //Nom du fichier temporaire

            try
            {
                StreamingCount++;
                var (dspFileNames, channelsCount) = await GetDSPFiles(fichier.Path, VGMStreamProcessTypes.Streaming, 0, cancellationToken);
                if ((filesToDelete = dspFileNames) == null) return null;

                var wavFileNames = (await dspFileNames.SelectAsync(dspFile => VGMStream.CreateTempFile("wav", VGMStreamProcessTypes.Streaming), cancellationToken)).ToArray();
                filesToDelete = dspFileNames.Concat(wavFileNames);

                var vgmstreamTmpInfos = new ProcessStartInfo[channelsCount];
                var vgmstreamTmpProcess = new Process[channelsCount];

                if (cancellationToken.IsCancellationRequested || !File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

                for (int i = 0; i < channelsCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await Task.Run(() =>
                    {
                        vgmstreamTmpInfos[i] = Out ? VGMStream.StartInfo(dspFileNames[i], wavFileNames[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : VGMStream.StartInfo(dspFileNames[i], wavFileNames[i], 1, false);
                        vgmstreamTmpProcess[i] = Process.Start(vgmstreamTmpInfos[i]);
                        VGMStream.RunningProcess.Add(vgmstreamTmpProcess[i], VGMStreamProcessTypes.Streaming);
                    });
                }

                cancellationToken.Register(() =>
                {
                    foreach (Process process in vgmstreamTmpProcess)
                    {
                        process.TryKill();
                        if (VGMStream.RunningProcess.ContainsKey(process)) VGMStream.RunningProcess.Remove(process);
                    }
                });

                for (int i = 0; i < vgmstreamTmpProcess.Length; i++)
                {
                    await vgmstreamTmpProcess[i].WaitForExitAsync(cancellationToken);
                    if (vgmstreamTmpProcess[i].ExitCode != 0) return null;
                }

                if (cancellationToken.IsCancellationRequested) return null;

                try
                {
                    await Task.Run(() =>
                    {
                        var wfrs = wavFileNames.Select(file => new WaveFileReader(file)).ToArray();
                        var waveProvider = new MultiplexingWaveProvider(wfrs);
                        WaveFileWriter.CreateWaveFile(fn, waveProvider);

                        foreach (var wfr in wfrs) wfr.Close();
                    }, cancellationToken);

                    return cancellationToken.IsCancellationRequested ? null : File.OpenRead(fn);
                }
                catch (OperationCanceledException) { return null; }
                catch
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
            finally
            {
                if (!cancellationToken.IsCancellationRequested) await VGMStream.DeleteTempFilesByName(filesToDelete);
                StreamingCount--;
            }
        }

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>true si la conversion a réussi; sinon false.</returns>
        public static async Task<string[]> ConvertFile(Fichier fichier, bool finalize = true, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            bool success = false;
            IEnumerable<string> filesToDelete = null;

            try
            {
                ConversionCount++;
                await pauseToken.WaitWhilePausedAsync();

                if (cancellationToken.IsCancellationRequested || !File.Exists(fichier.Path)) return null;

                fichier.OriginalState = "FSTATE_Conversion";

                var (dspFileNames, channelsCount) = await GetDSPFiles(fichier.Path, VGMStreamProcessTypes.Conversion, 0, cancellationToken, pauseToken);
                if ((filesToDelete = dspFileNames) == null) return null;

                var wavFileNames = (await dspFileNames.SelectAsync(dspFile => VGMStream.CreateTempFile("wav", VGMStreamProcessTypes.Conversion), cancellationToken)).ToArray();
                filesToDelete = dspFileNames.Concat(wavFileNames);

                var vgmstreamTmpInfos = new ProcessStartInfo[channelsCount];
                var vgmstreamTmpProcess = new Process[channelsCount];

                await pauseToken.WaitWhilePausedAsync();

                if (cancellationToken.IsCancellationRequested || !File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

                for (int i = 0; i < channelsCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await Task.Run(() =>
                    {
                        pauseToken.WaitWhilePausedAsync();
                        vgmstreamTmpInfos[i] = VGMStream.StartInfo(dspFileNames[i], wavFileNames[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop);
                        vgmstreamTmpProcess[i] = Process.Start(vgmstreamTmpInfos[i]);
                        VGMStream.RunningProcess.Add(vgmstreamTmpProcess[i], VGMStreamProcessTypes.Conversion);
                    });
                }

                for (int i = 0; i < vgmstreamTmpProcess.Length; i++)
                {
                    await vgmstreamTmpProcess[i].WaitForExitAsync(cancellationToken);
                    if (vgmstreamTmpProcess[i].ExitCode != 0) return null;
                }

                if (cancellationToken.IsCancellationRequested) return null;

                try
                {
                    await Task.Run(() =>
                    {
                        var wfrs = wavFileNames.Select(file => new WaveFileReader(file)).ToArray();
                        var waveProvider = new MultiplexingWaveProvider(wfrs);
                        WaveFileWriter.CreateWaveFile(fichier.FinalDestination, waveProvider);

                        foreach (var wfr in wfrs) wfr.Close();
                    }, cancellationToken);

                    await pauseToken.WaitWhilePausedAsync();

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        success = true;
                        var data = await vgmstreamTmpProcess[0].StandardOutput.ReadAllLinesAsync().WithCancellation(cancellationToken);

                        var indexOfPath = data.IndexOf(data.FirstOrDefault(s => s.Contains(dspFileNames[0])));
                        var indexOfChannels = data.IndexOf(data.FirstOrDefault(s => s.Contains("channels")));
                        var indexOfFormat = data.IndexOf(data.FirstOrDefault(s => s.Contains("metadata from")));
                        var indexOfBitrate = data.IndexOf(data.FirstOrDefault(s => s.Contains("bitrate")));

                        data[indexOfPath] = data[indexOfPath].Replace(dspFileNames[0], fichier.Path);
                        data[indexOfChannels] = data[indexOfChannels].Replace("1", channelsCount.ToString());
                        data[indexOfFormat] = data[indexOfFormat].Replace("Standard Nintendo DSP header", "Retro Studios DKCTF CSMP");

                        var brp = data[indexOfBitrate].Split(':');
                        if (brp.Length == 2)
                        {
                            var brs = brp[1].Replace("kbps", string.Empty);
                            var br = brs.ToInt();
                            if (br != null)
                            {
                                var bitrate = br * channelsCount;
                                data[indexOfBitrate] = data[indexOfBitrate].Replace(br.ToString(), bitrate.ToString());
                            }
                        }

                        return data;
                    }
                    else return null;
                }
                catch { return null; }
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                await VGMStream.DeleteTempFilesByName(filesToDelete);

                if (success) fichier.OriginalState = "FSTATE_Completed";
                else if (!cancellationToken.IsCancellationRequested && finalize) fichier.SetInvalid();

                ConversionCount--;
            }
        }

        /// <summary>
        /// Écrit les sous-fichiers DSP d'un fichier DKCTF CSMP.
        /// </summary>
        /// <param name="csmpFileName">Chemin du fichier DKCTF CSMP.</param>
        /// <param name="count">Nombre maximal de fichiers à écrire (0 = illimité).</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Liste des noms des fichiers écrits.</returns>
        private static async Task<(string[] DSPFileNames, int ChannelsCount)> GetDSPFiles(string csmpFileName, VGMStreamProcessTypes type, byte count = 0, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return default;

            CancellationTokenRegistration registration = default;

            try
            {
                using (var stream = File.Open(csmpFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Read32Bit(0x00, false) != RFRM || stream.Read32Bit(0x14, false) != CSMP) return default; //Check Magic

                    int size = stream.Read32Bit(0x08, false) - 0x38;
                    long fmtaIndex = 0x20;

                    if (stream.Read32Bit(fmtaIndex, false) == LABL)
                    {
                        int lablSize = 0x18 + stream.Read32Bit(0x28, false);
                        fmtaIndex += lablSize;
                        size -= lablSize;
                    }

                    long headerIndex = fmtaIndex + 0x38;

                    int chanCount = stream.Read32Bit(fmtaIndex + 0x15, false); //Get channels count
                    int realCount = count == 0 ? chanCount : Math.Min(count, chanCount); //Number of DSP files to create

                    int interleave = size / chanCount; //Get length of a DSP file. Also check if chanCount == 0 (catch a DivideByZeroException)

                    stream.Seek(headerIndex, SeekOrigin.Begin); //Seek to the first DSP file

                    var dspFiles = Enumerable.Range(0, realCount).Select(i => VGMStream.CreateTempFile("dsp", type)).ToArray(); //Get DSP files names list
                    registration = cancellationToken.Register(async () => await VGMStream.DeleteTempFilesByName(dspFiles.ToArray()));

                    for (int i = 0; i < realCount; i++)
                    {
                        using (var fs = File.Create(dspFiles[i]))
                        {
                            await pauseToken.WaitWhilePausedAsync();
                            await stream.CopyToAsync(fs, 0, interleave, false, cancellationToken); //Write the DSP files
                        }
                    }

                    return cancellationToken.IsCancellationRequested ? default : (dspFiles, chanCount);
                }
            }
            catch { return default; }
            finally { registration.TryDispose(); }
        }
    }
}
