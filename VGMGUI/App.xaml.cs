using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IniParser.Model;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using BenLib;
using Vlc.DotNet.Core;
using static VGMGUI.Settings;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow VGMainWindow { get; private set; }
        private static bool ShowMainWindow { get; set; }

        public static string[] InvalidFileNameChars => Path.GetInvalidFileNameChars().Select(c => c.ToString()).ToArray();

        public static string AppPath => AppDomain.CurrentDomain.BaseDirectory;
        public static string VGMStreamPath => Path.Combine(AppPath, "vgmstream\\test.exe");
        public static string VGMStreamFolder => Path.Combine(AppPath, "vgmstream");
        public static VlcMediaPlayer VlcMediaPlayer { get; private set; }
        public static string VLCFolder => Path.Combine(AppPath, "vlc");
        public static Version VLCVersion => VlcMediaPlayer.Manager.VlcVersionNumber;
        public static string VersionString => Version.ToString(3);
        public static Version Version => Assembly.GetName().Version;
        public static Assembly Assembly => Assembly.GetExecutingAssembly();
        public static Process Process => Process.GetCurrentProcess();

        public static bool AutoCulture { get; set; }
        private static CultureInfo s_currentCulture = new CultureInfo("fr-FR");
        public static CultureInfo CurrentCulture
        {
            get => s_currentCulture;
            set => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = s_currentCulture = value;
        }

        public static string[] Args { get; set; }

        public static ItemCollection FileListMItems => (Current.Resources["FileListCM"] as ContextMenu).Items;
        public static ItemCollection FileListItemCMItems => (Current.Resources["FileListItemCM"] as ContextMenu).Items;
        public static ItemCollection AskItemCMItems => (Current.Resources["AskItemCM"] as ContextMenu).Items;
        public static ItemCollection ErrorItemCMItems => (Current.Resources["ErrorItemCM"] as ContextMenu).Items;

        public static string[] AllowedStbxTxt => new[] { Str("MW_Multiple") };

        public static event PropertyChangedExtendedEventHandler<string> LanguageChanged;

        public App()
        {
            #if DEBUG
            DispatcherUnhandledException += (sender, e) => throw e.Exception;
            #else
            DispatcherUnhandledException += async (sender, e) =>
            {
                MessageBox.Show($"{e.Exception.Message}{Environment.NewLine}StackTrace : {e.Exception.StackTrace}", e.Exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
                await VGMStream.DeleteTempFiles(false);
                MainWindow?.Close();
                Shutdown(1);
            };
            #endif

            try { SettingsData = File.Exists(AppPath + "VGMGUI.ini") ? Parser.ReadFile(AppPath + "VGMGUI.ini") : new IniData(); }
            catch { SettingsData = new IniData(); }
        }

        public static async Task<bool> AskVGMStream() => MessageBox.Show(Str("ERR_VGMStreamNotFound"), Str("TT_Error"), MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes ? await VGMStream.DownloadVGMStream() : false;

        public static void SetLanguage(string culture)
        {
            if (culture == "Auto")
            {
                var installedCulture = CultureInfo.InstalledUICulture.ToString();
                switch (installedCulture.Split('-')[0])
                {
                    case "fr":
                        culture = "fr-FR";
                        break;
                    case "en":
                        culture = "en-US";
                        break;
                    default:
                        culture = "en-US";
                        break;
                }
                AutoCulture = true;
            }
            else AutoCulture = false;

            ResourceDictionary dict = new ResourceDictionary();
            var tmp = CurrentCulture.ToString();

            try { dict.Source = new Uri("..\\Lang\\" + culture + ".xaml", UriKind.Relative); }
            catch (IOException) { dict.Source = new Uri("..\\Lang\\en-US.xaml", UriKind.Relative); }
            try { CurrentCulture = new CultureInfo(culture); }
            catch (CultureNotFoundException) { CurrentCulture = new CultureInfo("en-US"); }

            Current.Resources.MergedDictionaries.RemoveAt(1);
            Current.Resources.MergedDictionaries.Add(dict);
            LanguageChanged?.Invoke(null, new PropertyChangedExtendedEventArgs<string>("Culture", tmp, culture));

            MessageBoxManager.Unregister();

            if (!AutoCulture)
            {
                MessageBoxManager.Yes = Str("TT_Yes");
                MessageBoxManager.No = Str("TT_No");
                MessageBoxManager.OK = Str("TT_OK");
                MessageBoxManager.Cancel = Str("TT_Cancel");
                MessageBoxManager.Retry = Str("TT_Retry");
                MessageBoxManager.Abort = Str("TT_Abort");
                MessageBoxManager.Ignore = Str("TT_Ignore");
                MessageBoxManager.Register();
            }
        }

        public static string Str(string resource) => !resource.IsNullOrEmpty() ? Current.Resources[resource] as string : null;
        public static string Str(string resource, string culture) => !resource.IsNullOrEmpty() ? new ResourceDictionary() { Source = new Uri("..\\Lang\\" + culture + ".xaml", UriKind.Relative) }[resource] as string : null;

        public static string Res(string str, bool findByCulture = true, string indice = "") => findByCulture ? Res(str, CurrentCulture.ToString(), indice) : Current.Resources.ToDictionary().FirstOrDefault(kvp => kvp.Value.Equals(str)).Key as string;
        public static string Res(string str, Dictionary<object, object> dictionary, string indice = "") => dictionary.FirstOrDefault(kvp => kvp.Value.Equals(str) && (kvp.Key as string).Contains(indice)).Key as string;
        public static string Res(string str, string culture, string indice = "")
        {
            var res = new ResourceDictionary() { Source = new Uri("..\\Lang\\" + culture + ".xaml", UriKind.Relative) }.OfType<DictionaryEntry>().Where(entry => entry.Value.Equals(str) && (entry.Key as string).Contains(indice)).ToArray();
            return res.Length == 1 ? res[0].Key as string : null;
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Args = e.Args;

            switch (Args.Length)
            {
                case 1:
                    if (Args[0] == "vlc") await VGMStream.DownloadVLC(true);
                    else ShowMainWindow = true;
                    break;
                case 2:
                    if (Args[0] == "vlc" && File.Exists(Args[1])) await VGMStream.DownloadVLC(true, Args[1]);
                    else ShowMainWindow = true;
                    break;
                default:
                    ShowMainWindow = true;
                    break;
            }

            if (ShowMainWindow)
            {
                object o;
                if ((o = SettingsData.Global["VLCC"]) != null)
                {
                    switch (o)
                    {
                        case "Memory":
                            VLCC = VLCCType.Memory;
                            break;
                        case "File":
                            VLCC = VLCCType.File;
                            break;
                        case "Never":
                            VLCC = VLCCType.Never;
                            break;
                    }
                }

                void ResetVLCCache()
                {
                    new VlcMediaPlayer(new DirectoryInfo(VLCFolder), new[] { "--reset-plugins-cache" });
                    Process.Start(Assembly.Location, "vlcc");
                    Shutdown(2);
                }

                try
                {
                    if (!Args.Contains("vlcc"))
                    {
                        bool vlccreset = (VLCC == VLCCType.Memory || VLCC == VLCCType.File) && !File.Exists(Path.Combine(VLCFolder, @"plugins\plugins.dat"));

                        if (vlccreset)
                        {
                            ResetVLCCache();
                            return;
                        }
                        else if (VLCC == VLCCType.Memory)
                        {
                            VlcMediaPlayer = new VlcMediaPlayer(new DirectoryInfo(VLCFolder));
                            if (Process.PrivateMemorySize64 > 60000000)
                            {
                                VlcMediaPlayer.TryDispose();
                                ResetVLCCache();
                                return;
                            }
                        }
                        else VlcMediaPlayer = new VlcMediaPlayer(new DirectoryInfo(VLCFolder));
                    }
                    else VlcMediaPlayer = new VlcMediaPlayer(new DirectoryInfo(VLCFolder));
                }
                catch
                {
                    if (MessageBox.Show(Str("ERR_VLC"), Str("TT_Error"), MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        Process.Start(Assembly.Location, "vlc");
                        Shutdown(2);
                        return;
                    }
                    else
                    {
                        Shutdown(1);
                        return;
                    }
                }

                VGMainWindow = new MainWindow();
                VGMainWindow.Closed += (sndr, args) => Shutdown();
                VGMainWindow.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var path = Assembly.Location;
            if (ShowMainWindow && File.Exists(VGMStream.VLCArcPath)) Process.Start(path, $"vlc {VGMStream.VLCArcPath}");
        }
    }
}
