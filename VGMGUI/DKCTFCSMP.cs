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
using Z.Linq;
using AsyncIO.FileSystem;

namespace VGMGUI
{
    public class DKCTFCSMP
    {
        public static readonly byte[] RFRM = { 82, 70, 82, 77 };
        public static readonly byte[] CSMP = { 67, 83, 77, 80 };
        public static readonly byte[] DATA = { 68, 65, 84, 65 };

        /// <summary>
        /// À partir d'un nom de fichier, obtient un fichier analysé uniquement s'il possède l'en-tête RFRM CSMP.
        /// </summary>
        /// <param name="fileName">Nom du fichier.</param>
        /// <param name="outData">Données complémentaires pour le fichier.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le fichier analysé.</returns>
        public static async Task<Fichier> GetFile(string fileName, FichierOutData outData = default, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(fileName)) return null;

            try
            {
                var dspCount = 0;
                var dspFile = Path.ChangeExtension(Path.GetTempFileName(), ".dsp"); //Temp DSP file name

                using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!(await (await stream.PeekBytesAsync(0x00, 4)).SequenceEqualAsync(RFRM) && await (await stream.PeekBytesAsync(0x14, 4)).SequenceEqualAsync(CSMP))) return null; //Check Magic

                    var csmpFile = await stream.ReadAllBytesAsync(cancellationToken);
                    var dataIndex = await csmpFile.IndexOfAsync(DATA, cancellationToken);

                    if (dataIndex == -1) return null; //DATA index

                    var data = csmpFile.SubArray(dataIndex + 27, csmpFile.Length - dataIndex - 27); //Get DATA subArray
                    var header = data.SubArray(0, 28); //Get header subArray

                    var dspIndexes = await data.AllIndexesOfAsync(header, cancellationToken); //Get indexes of subDSP files in string version of DATA
                    if (dspIndexes.IsNullOrEmpty()) return null;
                    else dspCount = dspIndexes.Count; //Get channels count

                    await AsyncFile.WriteAllBytesAsync(dspFile, data.SubArray(0, dspCount > 1 ? dspIndexes[1] : data.Length), cancellationToken); //Write temp file
                }
                if (dspCount == 0) return null;

                Process vgmstreamprocess = new Process() { StartInfo = VGMStream.StartInfo(dspFile, VGMStreamProcessTypes.Metadata) };
                VGMStream.RunningProcess.Add(vgmstreamprocess, VGMStreamProcessTypes.Metadata);

                try
                {
                    TryResult StartResult = await vgmstreamprocess.TryStartAsync(cancellationToken); //Start

                    if (!StartResult.Result) //N'a pas pu être démarré
                    {
                        if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    await vgmstreamprocess.WaitForExitAsync(cancellationToken); //WaitForExit

                    VGMStream.RunningProcess.Remove(vgmstreamprocess);

                    string[] s = vgmstreamprocess.StandardOutput.ReadAllLines();

                    if (s.IsNullOrEmpty()) return null;

                    var fichier = VGMStream.GetFile(s, outData, false, false);
                    if (fichier.Invalid) return fichier;
                    else
                    {
                        fichier.Path = fileName;
                        fichier.OriginalFormat = "Retro Studios DKCTF CSMP";
                        fichier.Channels = dspCount;
                        fichier.Bitrate *= dspCount;
                        return fichier;
                    }
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
                    if (VGMStream.RunningProcess.ContainsKey(vgmstreamprocess)) VGMStream.RunningProcess.Remove(vgmstreamprocess);
                    if (File.Exists(dspFile)) FileAsync.TryAndRetryDeleteAsync(dspFile, throwEx: false);
                }

            }
            catch (ArgumentException) { return null; }
        }

        /// <summary>
        /// Obtient le nom du fichier audio au format WAV à partir d'un fichier RFRM CSMP.
        /// </summary>
        /// <param name="fichier">Le fichier à décoder.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        public static async Task<string> GetStream(Fichier fichier, bool Out = false, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(fichier.Path)) return null;

            var dspCount = 0;
            var dsps = new List<byte[]>();
            var dspFiles = new List<string>();
            var wavFiles = new List<string>();

            try
            {
                try
                {
                    using (var stream = File.Open(fichier.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (!(await (await stream.PeekBytesAsync(0x00, 4)).SequenceEqualAsync(RFRM) && await (await stream.PeekBytesAsync(0x14, 4)).SequenceEqualAsync(CSMP))) return null; //Check Magic

                        var csmpFile = await stream.ReadAllBytesAsync(cancellationToken);
                        var dataIndex = await csmpFile.IndexOfAsync(DATA, cancellationToken);

                        if (dataIndex == -1) return null; //DATA index

                        var data = csmpFile.SubArray(dataIndex + 27, csmpFile.Length - dataIndex - 27); //Get DATA subArray
                        var header = data.SubArray(0, 28); //Get header subArray

                        var dspIndexes = await data.AllIndexesOfAsync(header, cancellationToken); //Get indexes of subDSP files in string version of DATA
                        if (dspIndexes.IsNullOrEmpty()) return null;
                        else dspCount = dspIndexes.Count; //Get channels count

                        dspFiles = (await dspIndexes.SelectAsync(index => Path.ChangeExtension(Path.GetTempFileName(), ".dsp"), cancellationToken)).ToList(); //Temp DSP files names
                        wavFiles = (await dspFiles.SelectAsync(dspFile => Path.ChangeExtension(dspFile, ".wav"), cancellationToken)).ToList(); //Temp WAV files names

                        await Task.Run(() =>
                        {
                            for (int i = 0; i < dspCount; i++) //Fill DSPs list
                            {
                                var length = i == dspCount - 1 ? data.Length - dspIndexes[i] : dspIndexes[i + 1] - dspIndexes[i];
                                dsps.Add(data.SubArray(dspIndexes[i], length));
                            }
                        }, cancellationToken);

                        for (int i = 0; i < dspCount; i++) await AsyncFile.WriteAllBytesAsync(dspFiles[i], dsps[i], cancellationToken); //Write temp files
                    }
                }
                catch (ArgumentException) { return null; }

                if (dspCount == 0) return null;

                var vgmstreamTmpInfos = new ProcessStartInfo[dspCount];
                var vgmstreamTmpProcess = new Process[dspCount];

                if (!File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

                await Task.Run(() =>
                {
                    for (int i = 0; i < dspCount; i++)
                    {
                        vgmstreamTmpInfos[i] = Out ? VGMStream.StartInfo(dspFiles[i], wavFiles[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop) : VGMStream.StartInfo(dspFiles[i], wavFiles[i], 1, false);
                        vgmstreamTmpProcess[i] = Process.Start(vgmstreamTmpInfos[i]);
                        VGMStream.RunningProcess.Add(vgmstreamTmpProcess[i], VGMStreamProcessTypes.Streaming);
                    }
                }, cancellationToken);

                for (int i = 0; i < dspCount; i++)
                {
                    await vgmstreamTmpProcess[i].WaitForExitAsync();
                }

                if (!File.Exists(App.FFmpegPath) && !await App.AskFFmepg()) return null; //Check FFmepg

                string fn = Path.ChangeExtension(Path.GetTempFileName(), ".wav"); //Nom du fichier temporaire
                await Task.Run(() => { while (File.Exists(fn)) fn = Path.ChangeExtension(Path.GetTempFileName(), ".wav"); });

                ProcessStartInfo FFInfo = new ProcessStartInfo(App.FFmpegPath, await GetFFArgsAsync(wavFiles, fn))
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process FFProcess = new Process() { StartInfo = FFInfo };

                VGMStream.RunningProcess.Add(FFProcess, VGMStreamProcessTypes.Streaming);

                TryResult StartResult = await FFProcess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                try
                {
                    await FFProcess.WaitForExitAsync(cancellationToken); //WaitForExit

                    VGMStream.RunningProcess.Remove(FFProcess);

                    foreach (string file in wavFiles) await FileAsync.TryAndRetryDeleteAsync(file, throwEx: false);

                    if (FFProcess.ExitCode == 0)
                    {
                        VGMStream.TempFiles.Enqueue(fn);
                        return fn;
                    }
                    else
                    {
                        fichier.SetInvalid();
                        return null;
                    }
                }
                catch (OperationCanceledException) { FFProcess.TryKill(); }
                finally { if (VGMStream.RunningProcess.ContainsKey(FFProcess)) VGMStream.RunningProcess.Remove(FFProcess); }

                return null;
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                for (int i = 0; i < dspCount; i++)
                {
                    VGMStream.TempFiles.Enqueue(dspFiles[i]);
                    VGMStream.TempFiles.Enqueue(wavFiles[i]);
                }
            }
        }

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>true si la conversion a réussi; sinon false.</returns>
        public static async Task<IEnumerable<string>> ConvertFile(Fichier fichier, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(fichier.Path)) return null;

            fichier.OriginalState = "FSTATE_Conversion";

            bool success = false;

            var dspCount = 0;
            var dsps = new List<byte[]>();
            var dspFiles = new List<string>();
            var wavFiles = new List<string>();

            try
            {
                using (var stream = File.Open(fichier.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!(await (await stream.PeekBytesAsync(0x00, 4)).SequenceEqualAsync(RFRM) && await (await stream.PeekBytesAsync(0x14, 4)).SequenceEqualAsync(CSMP))) return null; //Check Magic

                    var csmpFile = await stream.ReadAllBytesAsync(cancellationToken);
                    var dataIndex = await csmpFile.IndexOfAsync(DATA, cancellationToken);

                    if (dataIndex == -1) return null; //DATA index

                    var data = csmpFile.SubArray(dataIndex + 27, csmpFile.Length - dataIndex - 27); //Get DATA subArray
                    var header = data.SubArray(0, 28); //Get header subArray

                    var dspIndexes = await data.AllIndexesOfAsync(header, cancellationToken); //Get indexes of subDSP files in string version of DATA
                    if (dspIndexes.IsNullOrEmpty()) return null;
                    else dspCount = dspIndexes.Count; //Get channels count

                    dspFiles = (await dspIndexes.SelectAsync(index => Path.ChangeExtension(Path.GetTempFileName(), ".dsp"), cancellationToken)).ToList(); //Temp DSP files names
                    wavFiles = (await dspFiles.SelectAsync(dspFile => Path.ChangeExtension(dspFile, ".wav"), cancellationToken)).ToList(); //Temp WAV files names

                    await Task.Run(() =>
                    {
                        for (int i = 0; i < dspCount; i++) //Fill DSPs list
                        {
                            var length = i == dspCount - 1 ? data.Length - dspIndexes[i] : dspIndexes[i + 1] - dspIndexes[i];
                            dsps.Add(data.SubArray(dspIndexes[i], length));
                        }
                    }, cancellationToken);

                    for (int i = 0; i < dspCount; i++) await AsyncFile.WriteAllBytesAsync(dspFiles[i], dsps[i], cancellationToken); //Write temp files
                }
            }
            catch (ArgumentException) { return null; }

            if (dspCount == 0) return null;

            var vgmstreamTmpInfos = new ProcessStartInfo[dspCount];
            var vgmstreamTmpProcess = new Process[dspCount];

            if (!File.Exists(App.VGMStreamPath) && !await App.AskVGMStream()) return null; //Check VGMStream

            for (int i = 0; i < dspCount; i++)
            {
                await Task.Run(() =>
                {
                    vgmstreamTmpInfos[i] = VGMStream.StartInfo(dspFiles[i], wavFiles[i], fichier.LoopCount, fichier.FadeOut, fichier.FadeDelay, fichier.FadeTime, fichier.StartEndLoop);
                    vgmstreamTmpProcess[i] = Process.Start(vgmstreamTmpInfos[i]);
                    VGMStream.RunningProcess.Add(vgmstreamTmpProcess[i], VGMStreamProcessTypes.Conversion);
                });
            }

            for (int i = 0; i < vgmstreamTmpProcess.Length; i++) await vgmstreamTmpProcess[i].WaitForExitAsync();

            if (!File.Exists(App.FFmpegPath) && !await App.AskFFmepg()) return null; //Check FFmepg

            if (File.Exists(fichier.FinalDestination)) await FileAsync.TryAndRetryDeleteAsync(fichier.FinalDestination, throwEx: false);

            ProcessStartInfo FFInfo = new ProcessStartInfo(App.FFmpegPath, await GetFFArgsAsync(wavFiles, fichier.FinalDestination))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process FFProcess = new Process() { StartInfo = FFInfo };

            VGMStream.RunningProcess.Add(FFProcess, VGMStreamProcessTypes.Conversion);

            try
            {
                TryResult StartResult = await FFProcess.TryStartAsync(cancellationToken); //Start

                if (!StartResult.Result) //N'a pas pu être démarré
                {
                    if (!(StartResult.Exception is OperationCanceledException)) MessageBox.Show(StartResult.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                await FFProcess.WaitForExitAsync(cancellationToken); //WaitForExit

                VGMStream.RunningProcess.Remove(FFProcess);

                foreach (string file in wavFiles) await FileAsync.TryAndRetryDeleteAsync(file, throwEx: false);

                if (FFProcess.ExitCode == 0)
                {
                    success = true;
                    var data = (await vgmstreamTmpProcess[0].StandardOutput.ReadAllLinesAsync()).ToList();

                    var indexOfPath = data.IndexOf(data.FirstOrDefault(s => s.Contains(dspFiles[0])));
                    var indexOfChannels = data.IndexOf(data.FirstOrDefault(s => s.Contains("channels")));
                    var indexOfFormat = data.IndexOf(data.FirstOrDefault(s => s.Contains("metadata from")));
                    var indexOfBitrate = data.IndexOf(data.FirstOrDefault(s => s.Contains("bitrate")));

                    data[indexOfPath] = data[indexOfPath].Replace(dspFiles[0], fichier.Path);
                    data[indexOfChannels] = data[indexOfChannels].Replace("1", dspCount.ToString());
                    data[indexOfFormat] = data[indexOfFormat].Replace("Standard Nintendo DSP header", "Retro Studios DKCTF CSMP");

                    var brp = data[indexOfBitrate].Split(':');
                    if (brp.Length == 2)
                    {
                        var brs = brp[1].Replace("kbps", "");
                        var br = brs.ToInt();
                        if (br != null)
                        {
                            var bitrate = br * dspCount;
                            data[indexOfBitrate] = data[indexOfBitrate].Replace(br.ToString(), bitrate.ToString());
                        }
                    }

                    return data;
                }
                else return null;
            }
            catch (OperationCanceledException)
            {
                FFProcess.TryKill();
                return null;
            }
            finally
            {
                for (int i = 0; i < dspCount; i++)
                {
                    VGMStream.TempFiles.Enqueue(dspFiles[i]);
                    VGMStream.TempFiles.Enqueue(wavFiles[i]);
                }

                if (VGMStream.RunningProcess.ContainsKey(FFProcess)) VGMStream.RunningProcess.Remove(FFProcess);

                if (!cancellationToken.IsCancellationRequested)
                {
                    if (success) fichier.OriginalState = "FSTATE_Completed";
                    else fichier.SetInvalid();
                }
                else if (!success) fichier.OriginalState = "FSTATE_Canceled";
            }
        }

        private static string GetFFArgs(IEnumerable<string> channels, string outputPath)
        {
            var channelsCount = channels.Count();
            StringBuilder sb = new StringBuilder();
            foreach (string channel in channels) sb.Append(" -i \"" + channel + "\"");
            sb.Append(" -filter_complex \"");
            for (int i = 0; i < channelsCount; i++) sb.Append($"[{i}:a]");
            sb.Append($"amerge=inputs={channelsCount}[aout]\" -map \"[aout]\" -y \"{outputPath}\"");
            return sb.ToString().TrimStart(' ');
        }

        private static Task<string> GetFFArgsAsync(IEnumerable<string> channels, string outputPath) => Task.Run(() => GetFFArgs(channels, outputPath));
    }
}
