using BenLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using Z.Linq;

namespace VGMGUI
{
    public class DKCTFCSMP
    {
        public static readonly byte[] RFRM = { 82, 70, 82, 77 };
        public static readonly byte[] CSMP = { 67, 83, 77, 80 };
        public static readonly byte[] FMTA = { 70, 77, 84, 65 };
        public static readonly byte[] LABL = { 76, 65, 66, 76 };

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

            byte chanCount = 0;
            string dspFile = String.Empty;

            try
            {
                ScanningCount++;
                var dsps = await GetDSPFiles(fileName, VGMStreamProcessTypes.Metadata, 1, cancellationToken);

                if (dsps == null) return null;

                dspFile = dsps.Item1[0];
                chanCount = dsps.Item2;

                Process vgmstreamprocess = new Process() { StartInfo = VGMStream.StartInfo(dspFile, VGMStreamProcessTypes.Metadata) };
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
                    fichier.Channels = chanCount;
                    fichier.Bitrate *= chanCount;
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
                if (!cancellationToken.IsCancellationRequested) await VGMStream.DeleteTempFilesByName(dspFile);
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

            byte chanCount = 0;
            var dspFiles = new List<string>();
            var wavFiles = new List<string>();
            string fn = VGMStream.CreateTempFile("wav", VGMStreamProcessTypes.Streaming); //Nom du fichier temporaire

            try
            {
                StreamingCount++;
                var dsps = await GetDSPFiles(fichier.Path, VGMStreamProcessTypes.Streaming, 0, cancellationToken);

                if (dsps == null) return null;

                dspFiles = dsps.Item1;
                chanCount = dsps.Item2;

                wavFiles = (await dspFiles.SelectAsync(dspFile => VGMStream.CreateTempFile("wav", VGMStreamProcessTypes.Streaming), cancellationToken)).ToList();

                var vgmstreamTmpInfos = new ProcessStartInfo[chanCount];
                var vgmstreamTmpProcess = new Process[chanCount];

                if (cancellationToken.IsCancellationRequested || !File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

                for (int i = 0; i < chanCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await Task.Run(() =>
                    {
                        vgmstreamTmpInfos[i] = Out ? VGMStream.StartInfo(dspFiles[i], wavFiles[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : VGMStream.StartInfo(dspFiles[i], wavFiles[i], 1, false);
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
                        var waveProvider = new MultiplexingWaveProvider(wavFiles.Select(file => new WaveFileReader(file)));
                        WaveFileWriter.CreateWaveFile(fn, waveProvider);
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
                if (!cancellationToken.IsCancellationRequested) await VGMStream.DeleteTempFilesByName((await dspFiles.ConcatAsync(wavFiles)).ToArray());
                StreamingCount--;
            }
        }

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>true si la conversion a réussi; sinon false.</returns>
        public static async Task<List<string>> ConvertFile(Fichier fichier, bool finalize = true, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            bool success = false;

            var chanCount = 0;
            var dspFiles = new List<string>();
            var wavFiles = new List<string>();

            try
            {
                ConversionCount++;
                await pauseToken.WaitWhilePausedAsync();

                if (cancellationToken.IsCancellationRequested || !File.Exists(fichier.Path)) return null;

                fichier.OriginalState = "FSTATE_Conversion";

                var dsps = await GetDSPFiles(fichier.Path, VGMStreamProcessTypes.Conversion, 0, cancellationToken, pauseToken);

                if (dsps == null) return null;

                dspFiles = dsps.Item1;
                chanCount = dsps.Item2;

                wavFiles = (await dspFiles.SelectAsync(dspFile => VGMStream.CreateTempFile("wav", VGMStreamProcessTypes.Conversion), cancellationToken)).ToList();

                var vgmstreamTmpInfos = new ProcessStartInfo[chanCount];
                var vgmstreamTmpProcess = new Process[chanCount];

                await pauseToken.WaitWhilePausedAsync();

                if (cancellationToken.IsCancellationRequested || !File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

                for (int i = 0; i < chanCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await Task.Run(() =>
                    {
                        pauseToken.WaitWhilePausedAsync();
                        vgmstreamTmpInfos[i] = VGMStream.StartInfo(dspFiles[i], wavFiles[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop);
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
                    var waveProvider = new MultiplexingWaveProvider(wavFiles.Select(file => new WaveFileReader(file)));
                    WaveFileWriter.CreateWaveFile(fichier.FinalDestination, waveProvider);

                    await pauseToken.WaitWhilePausedAsync();

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        success = true;
                        var data = (await vgmstreamTmpProcess[0].StandardOutput.ReadAllLinesAsync().WithCancellation(cancellationToken)).ToList();

                        var indexOfPath = data.IndexOf(data.FirstOrDefault(s => s.Contains(dspFiles[0])));
                        var indexOfChannels = data.IndexOf(data.FirstOrDefault(s => s.Contains("channels")));
                        var indexOfFormat = data.IndexOf(data.FirstOrDefault(s => s.Contains("metadata from")));
                        var indexOfBitrate = data.IndexOf(data.FirstOrDefault(s => s.Contains("bitrate")));

                        data[indexOfPath] = data[indexOfPath].Replace(dspFiles[0], fichier.Path);
                        data[indexOfChannels] = data[indexOfChannels].Replace("1", chanCount.ToString());
                        data[indexOfFormat] = data[indexOfFormat].Replace("Standard Nintendo DSP header", "Retro Studios DKCTF CSMP");

                        var brp = data[indexOfBitrate].Split(':');
                        if (brp.Length == 2)
                        {
                            var brs = brp[1].Replace("kbps", String.Empty);
                            var br = brs.ToInt();
                            if (br != null)
                            {
                                var bitrate = br * chanCount;
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
                await VGMStream.DeleteTempFilesByName((await dspFiles.ConcatAsync(wavFiles)).ToArray());

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
        private static async Task<Tuple<List<string>, byte>> GetDSPFiles(string csmpFileName, VGMStreamProcessTypes type, byte count = 0, CancellationToken cancellationToken = default, PauseToken pauseToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return null;

            IEnumerable<string> GetDSPFileNames(int fileCount)
            {
                for (int i = 0; i < fileCount; i++) yield return VGMStream.CreateTempFile("dsp", type);
            }

            CancellationTokenRegistration registration = default;
            List<string> dspFiles = default;

            try
            {
                using (var stream = File.Open(csmpFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!(await (await stream.PeekBytesAsync(0x00, 4)).SequenceEqualAsync(RFRM) && await (await stream.PeekBytesAsync(0x14, 4)).SequenceEqualAsync(CSMP))) return null; //Check Magic

                    var size = Hexadecimal.DCBAEndianToInt(await stream.PeekBytesAsync(0x08, 4), false);

                    long fmtaIndex = 0;

                    var fmtalabl = await stream.PeekBytesAsync(0x20, 4);

                    if (await fmtalabl.SequenceEqualAsync(FMTA)) fmtaIndex = 0x20;
                    else if (await fmtalabl.SequenceEqualAsync(LABL))
                    {
                        var lablSize = 24 + Hexadecimal.DCBAEndianToInt(await stream.PeekBytesAsync(0x28, 4), false);
                        fmtaIndex = 0x20 + lablSize;
                        size -= lablSize;
                    }

                    var chanCount = stream.PeekByte(fmtaIndex + 24); //Get channels count
                    var realCount = count == 0 ? chanCount : Math.Min(count, chanCount); //Number of DSP files to create

                    int dspLength = (size - 56) / chanCount; //Get length of a DSP file. Also check if chanCount == 0 (catch a DivideByZeroException)

                    stream.Seek(fmtaIndex + 56, SeekOrigin.Begin); //Seek to the first DSP file

                    dspFiles = GetDSPFileNames(realCount).ToList(); //Get DSP files names list
                    registration = cancellationToken.Register(async () => await VGMStream.DeleteTempFilesByName(dspFiles.ToArray()));

                    for (int i = 0; i < realCount; i++)
                    {
                        using (var fs = File.Create(dspFiles[i]))
                        {
                            await pauseToken.WaitWhilePausedAsync();
                            await stream.CopyToAsync(fs, 0, dspLength, false, cancellationToken); //Write the DSP files
                        }
                    }

                    return cancellationToken.IsCancellationRequested ? null : Tuple.Create(dspFiles, chanCount);
                }
            }
            catch { return null; }
            finally { registration.TryDispose(); }
        }
    }
}
