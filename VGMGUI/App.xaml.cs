using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IniParser;
using IniParser.Model;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Drawing;
using System.Resources;
using BenLib;
using System.Windows.Input;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static MainWindow MainWindow { get; private set; }
        private static bool ShowMainWindow { get; set; }

        public static string AppPath => AppDomain.CurrentDomain.BaseDirectory;
        public static string VGMStreamPath => Path.Combine(AppPath, "vgmstream\\test.exe");
        public static string VGMStreamFolder => Path.Combine(AppPath, "vgmstream");
        public static string FFmpegPath => Path.Combine(AppPath, "ffmpeg\\ffmpeg.exe");
        public static string FFmpegFolder => Path.Combine(AppPath, "ffmpeg");
        public static string VLCFolder => Path.Combine(AppPath, "vlc");
        public static string VersionString => Version.ToString(3);
        public static Version Version => Assembly.GetName().Version;
        public static Assembly Assembly => Assembly.GetExecutingAssembly();
        public static Process Process => Process.GetCurrentProcess();

        public static bool AutoCulture { get; set; }
        public static CultureInfo CurrentCulture { get; set; } = new CultureInfo("fr-FR");
        public static CultureInfo DefaultThreadCurrentCulture { get => CultureInfo.DefaultThreadCurrentCulture; set => CultureInfo.DefaultThreadCurrentCulture = value; }
        public static CultureInfo DefaultThreadCurrentUICulture { get => CultureInfo.DefaultThreadCurrentUICulture; set => CultureInfo.DefaultThreadCurrentUICulture = value; }

        public static string[] Args { get; set; }

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
                await VGMStream.DeleteTMPFiles();
                MainWindow?.Close();

                if (e.Exception.InnerException is VLCException vlcEx)
                {
                    if (MessageBox.Show(Str("ERR_VLCNotFound"), Str("TT_Error"), MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        var path = Assembly.Location;
                        Process.Start(path, "vlc");
                    }
                }
                else MessageBox.Show(e.Exception.Message, e.Exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);

                Shutdown(1);
            };
            #endif

            try { Settings.SettingsData = File.Exists(AppPath + "VGMGUI.ini") ? Settings.Parser.ReadFile(AppPath + "VGMGUI.ini") : new IniData(); }
            catch { Settings.SettingsData = new IniData(); }
        }

        public static async Task<bool> AskVGMStream()
        {
            if (MessageBox.Show(Str("ERR_VGMStreamNotFound"), Str("TT_Error"), MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                return await VGMStream.DownloadVGMStream();
            }
            else return false;
        }

        public static async Task<bool> AskFFmepg()
        {
            if (MessageBox.Show(Str("ERR_FFNotFound"), Str("TT_Error"), MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                return await VGMStream.DownloadFFmpeg();
            }
            else return false;
        }

        public static void SetLanguage(string culture)
        {
            if (culture == "Auto")
            {
                culture = CultureInfo.InstalledUICulture.ToString();
                AutoCulture = true;
            }
            else AutoCulture = false;

            ResourceDictionary dict = new ResourceDictionary();
            var tmp = CurrentCulture.ToString();

            try { dict.Source = new Uri("..\\Lang\\" + culture + ".xaml", UriKind.Relative); }
            catch (IOException) { dict.Source = new Uri("..\\Lang\\en-US.xaml", UriKind.Relative); }
            try { CurrentCulture = DefaultThreadCurrentCulture = DefaultThreadCurrentUICulture = new CultureInfo(culture); }
            catch (CultureNotFoundException) { CurrentCulture = DefaultThreadCurrentCulture = DefaultThreadCurrentUICulture = new CultureInfo("en-US"); }

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

        public static string Res(string str, bool findByCulture = true, string indice = "")
        {
            if (findByCulture)
            {
                var res = (from DictionaryEntry entry in new ResourceDictionary() { Source = new Uri("..\\Lang\\" + CurrentCulture + ".xaml", UriKind.Relative) } where entry.Value.Equals(str) && (entry.Key as string).Contains(indice) select entry).ToList();
                return res.Count == 1 ? res[0].Key as String : null;
            }
            return Current.Resources.Dictionary().FirstOrDefault(kvp => kvp.Value.Equals(str)).Key as string;
        }
        public static string Res(string str, Dictionary<object, object> dictionary, string indice = "") => dictionary.FirstOrDefault(kvp => kvp.Value.Equals(str) && (kvp.Key as string).Contains(indice)).Key as string;
        public static string Res(string str, string culture, string indice = "")
        {
            var res = (from DictionaryEntry entry in new ResourceDictionary() { Source = new Uri("..\\Lang\\" + culture + ".xaml", UriKind.Relative) } where entry.Value.Equals(str) && (entry.Key as string).Contains(indice) select entry).ToList();
            return res.Count == 1 ? res[0].Key as String : null;
        }

        public static async Task FreeMemory()
        {
            await VGMStream.DeleteTMPFiles();
            GC.Collect();
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
                MainWindow = new MainWindow();
                MainWindow.Closed += (sndr, args) => Shutdown();
                MainWindow.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var path = Assembly.Location;
            if (ShowMainWindow && File.Exists(VGMStream.VLCArcPath)) Process.Start(path, $"vlc {VGMStream.VLCArcPath}");
        }
    }
}
