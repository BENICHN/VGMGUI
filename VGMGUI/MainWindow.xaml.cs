using BenLib;
using BenLib.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Vlc.DotNet.Core.Interops.Signatures;
using Z.Linq;
using static BenLib.Animating;
using static VGMGUI.Settings;
using Clipboard = System.Windows.Forms.Clipboard;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Champs & Propriétés

        private DispatcherTimer RAMTimer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 1) };

        #region Playing

        /// <summary>
        /// Indique si un fichier est actuellement en train d'être décodé pour la lecture.
        /// </summary>
        public bool Buffering { get; set; }

        public bool Passing { get; set; }

        /// <summary>
        /// Annule la lecture.
        /// </summary>
        public CancellationTokenSource PlayingCTS { get; set; } = new CancellationTokenSource();

        #endregion

        #region Conversion

        /// <summary>
        /// Indique si le processus de préconversion est en cours.
        /// </summary>
        public bool Preconversion { get => m_preconversion; set => m_preconversion = MainProgress.IsIndeterminate = value; }
        private bool m_preconversion;

        private System.Windows.Shapes.Path[] m_conversionIcon;
        private int m_conversionErrorsCount;

        /// <summary>
        /// Annule la conversion.
        /// </summary>
        public CancellationTokenSource ConversionCTS { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// Suspend la conversion.
        /// </summary>
        public PauseTokenSource ConversionPTS { get; set; } = new PauseTokenSource();

        /// <summary>
        /// Contient les fichiers cochés et valides à convertir.
        /// </summary>
        public List<Fichier> FilesToConvertTMP { get; set; }

        /// <summary>
        /// Contient les fichiers cochés et valides à convertir.
        /// </summary>
        public Queue<Fichier> FilesToConvert { get; set; } = new Queue<Fichier>();

        /// <summary>
        /// Contient les fichiers nécessitant une demande (écraser/ignorer/numéroter)
        /// </summary>
        public IEnumerable<AskingFile> FilesToAsk { get; set; }

        /// <summary>
        /// Indique si une conversion est en cours.
        /// </summary>
        public bool Converting => !(FilesToConvert.Count == 0 && CurrentConverting == 0);

        /// <summary>
        /// Progression de la conversion.
        /// </summary>
        public int ConversionProgress => ConversionCount - FilesToConvert.Count;

        /// <summary>
        /// Nombre de fichiers convertis ou à convertir.
        /// </summary>
        public int ConversionCount { get; set; }

        public int ConversionErrorsCount
        {
            get => m_conversionErrorsCount;
            set
            {
                m_conversionErrorsCount = value;
                conversionErrorsLabel.Content = value > 0 ? $"|  {value} {App.Str(value == 1 ? "WW_Error" : "WW_Errors")}" : string.Empty;
            }
        }

        /// <summary>
        /// Nombre d'instances de <see cref="ConvertFile"/> en cours.
        /// </summary>
        public int CurrentConverting { get; set; }

        #endregion

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref='MainWindow'/>.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            ApplySettings((IniParser.Model.IniData)SettingsData.Clone(), true);

            DataContext = this;

            PasteButton.IsEnabled = Clipboard.ContainsData("VGMGUIOutData");

            for (int i = 0; i < Infos.Length; i++) Infos[i] = new Dictionary<object, int>(); //Création des dictionnaires dans "Infos"

            #region Images

            tbi_previous.ImageSource = Properties.Resources.Previous.ToSource();
            tbi_playpause.ImageSource = Properties.Resources.Play.ToSource();
            tbi_stop.ImageSource = Properties.Resources.Stop.ToSource();
            tbi_next.ImageSource = Properties.Resources.Next.ToSource();

            #endregion

            #region EventHandlers

            RAMTimer.Tick += (sender, e) => UpdateStatusBar(true);

            AP.AddButton.Click += AddButton_Click;
            AP.RemButton.Click += RemButton_Click;
            AP.UpButton.Click += UpButton_Click;
            AP.DownButton.Click += DownButton_Click;
            AP.PlayButton.Click += PlayButton_Click;
            AP.NextButton.Click += NextButton_Click;
            AP.PreviousButton.Click += PreviousButton_Click;
            AP.StopButton.Click += StopButton_Click;
            AP.SettingsButton.Click += SettingsButton_Click;
            AP.DownloadButton.Click += DownloadButton_Click;
            AP.LoopTypeChanged += AP_LoopTypeChanged;
            AP.EndReached += AP_EndReached;
            AP.Stopped += AP_Stopped;

            tasklist.FILEList.SelectionChanged += FILEList_SelectionChanged;
            tasklist.Files.CollectionChanged += Files_CollectionChanged;
            tasklist.Files.ItemChangedEvent += Files_ItemChangedEvent;
            tasklist.FilterChanged += Tasklist_FilterChanged;

            App.FileListItemCMItems.FindCollectionItem<MenuItem>("PlayInMI").Click += PlayInMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("PlayOutMI").Click += PlayOutMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI").Click += ConvertMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("SkipMI").Click += SkipMI;

            App.LanguageChanged += App_LanguageChanged;

            ClipboardNotification.ClipboardUpdate += ClipboardNotification_ClipboardUpdate;

            #endregion
        }

        #endregion

        #region Méthodes

        private void SetFinalDestinations(IEnumerable<Fichier> collection, string CBText, string TBText)
        {
            void CheckFolder(string folder)
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                else if (!IO.WriteAccess(folder)) throw new UnauthorizedAccessException(App.Str("ERR_UnauthorizedAccess1") + folder + App.Str("ERR_UnauthorizedAccess2"));
            }

            IEnumerable<Fichier> GetFilesToConvertTMP()
            {
                IEnumerable<Fichier> AddToFilesToConvertTMP(IEnumerable<Fichier> enumerable, string destFolder)
                {
                    CheckFolder(destFolder);
                    foreach (var fichier in enumerable)
                    {
                        fichier.FinalDestination = Path.Combine(destFolder, Path.GetFileNameWithoutExtension(fichier.Path)) + ".wav";
                        Dispatcher.Invoke(() => fichier.SetValid());
                        yield return fichier;
                    }
                }

                foreach (var group in collection.Where(fichier => File.Exists(fichier.Path)).GroupBy(fichier => fichier.Destination))
                {
                    switch (App.Res(group.Key))
                    {
                        case "DEST_Principal":
                            {
                                if (CBText == App.Str("DEST_SourceFolder"))
                                {
                                    var pgs = group.GroupBy(fichier => fichier.Folder);
                                    foreach (var fichier in pgs.SelectMany(grp => AddToFilesToConvertTMP(grp, Path.Combine(grp.Key, TBText)))) yield return fichier;
                                }
                                else foreach (var fichier in AddToFilesToConvertTMP(group, Path.Combine(CBText, TBText))) yield return fichier;
                            }
                            break;
                        case "DEST_SourceFolder":
                            {
                                var gs = group.GroupBy(fichier => fichier.Folder);
                                foreach (var fichier in gs.SelectMany(grp => AddToFilesToConvertTMP(grp, grp.Key))) yield return fichier;
                            }
                            break;
                        default:
                            foreach (var fichier in AddToFilesToConvertTMP(group, group.Key)) yield return fichier;
                            break;
                    }
                }
            }

            var ftc = GetFilesToConvertTMP().ToList();

            FilesToAsk = ftc.GroupBy(fichier => fichier.FinalDestination).Where(group =>
            {
                var enumerator = group.GetEnumerator();
                enumerator.MoveNext();
                var firstFile = enumerator.Current;

                if (!enumerator.MoveNext()) return File.Exists(group.Key);
                else
                {
                    firstFile.ToNumber = true;
                    do { enumerator.Current.ToNumber = true; } while (enumerator.MoveNext());
                    return false;
                }
            }).Select(group => new AskingFile(group.First()));

            foreach (var fichier in collection.Except(ftc)) Dispatcher.Invoke(() => fichier.SetInvalid("ERR_FileNotFound"));

            FilesToConvertTMP = ftc;
        }

        private Task SetFinalDestinationsAsync(IEnumerable<Fichier> collection)
        {
            string CBText = MainDestCB.Text, TBText = MainDestTB.Text;
            return Task.Run(() => SetFinalDestinations(collection, CBText, TBText));
        }

        /// <summary>
        /// Ouvre une <see cref="SettingsWindow"/> et applique les paramètres retournés.
        /// </summary>
        private void OpenSettingsWindow()
        {
            if (new SettingsWindow().ShowDialog()) ApplySettings(SettingsData);
        }

        #region StatusBar

        public void UpdateStatusBar(bool memory = false, bool samplesDisplay = false, bool streamingType = false)
        {
            if (StatusBar.Display)
            {
                if (StatusBar.Counter) listCountLabel.Content = tasklist.FILEList.SelectedItems.Count > 0 ? $"{tasklist.FILEList.SelectedItems.Count} {(tasklist.FILEList.SelectedItems.Count == 1 ? App.Str("ST_objectSelectedOutOf") : App.Str("ST_objectsSelectedOutOf"))} {tasklist.FILEList.Items.Count}" : $"{tasklist.FILEList.Items.Count} {(tasklist.FILEList.Items.Count == 1 ? App.Str("ST_object") : App.Str("ST_objects"))}";
                if (StatusBar.RAM && memory) RAMUsageLabel.Content = $"{App.Str("ST_RAMUsage")} {IO.GetFileSize(App.Process.PrivateMemorySize64, "0.00")}";
                if (StatusBar.StreamingType && streamingType) StreamingTypeButton.ToolTip = $"{App.Str("STGS_STSBAR_StreamingType")} : {(Settings.StreamingType == StreamingType.Live ? App.Str("STGS_STSBAR_StreamingTypeLive") : "Cache")}";
                if (samplesDisplay) samplesDisplayLabel.Content = HMSSamplesDisplay ? "xx:xx" : "x sec";
            }
        }

        public void ShowHideStatusBar(bool show)
        {
            if (show)
            {
                UpdateStatusBar();
                statusBar.Height = 25;
                grid.RowDefinitions[1].Height = new GridLength(90);
            }
            else
            {
                statusBar.Height = 0;
                grid.RowDefinitions[1].Height = new GridLength(65);
            }
        }

        #endregion

        #region Apply

        /// <summary>
        /// Effectue certaines actions en fonction de des arguments de l'application.
        /// </summary>
        private async Task ApplyArgs()
        {
            bool? playout = null;
            bool playnext = false;
            string filetoplay = null;

            bool context = false;
            bool gettingcontext = false;
            bool gotcontext = false;
            bool contexting = false;
            FichierOutData template = default;

            Dictionary<string, FichierOutData> FilesToAdd = new Dictionary<string, FichierOutData>();

            async void PlayFileCallback(object sender, EventArgs e)
            {
                await PlayFile(tasklist.Files.FirstOrDefault(f => f.Path == filetoplay), playout);
                tasklist.AddingCompleted -= PlayFileCallback;
            }

            foreach (string arg in App.Args)
            {
                if (context)
                {
                    if (gettingcontext)
                    {
                        string[] data = arg.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);

                        if (data.Length == 2)
                        {
                            string value = data[1];
                            object o;

                            switch (data[0])
                            {
                                case "loopcount":
                                    if ((o = value.ToInt()) != null) template.LoopCount = (int)o;
                                    break;
                                case "fadeout":
                                    if ((o = value.ToBool()) != null) template.FadeOut = (bool)o;
                                    break;
                                case "fadedelay":
                                    if ((o = value.ToDouble()) != null) template.FadeDelay = (double)o;
                                    break;
                                case "fadetime":
                                    if ((o = value.ToDouble()) != null) template.FadeTime = (double)o;
                                    break;
                                case "startendloop":
                                    if ((o = value.ToBool()) != null) template.StartEndLoop = (bool)o;
                                    break;
                                case "destination":
                                    if (File.Exists(value) || App.Str(value) != null) template.OriginalDestination = value;
                                    break;
                            }
                        }
                        else
                            switch (arg)
                            {
                                case ")":
                                    gettingcontext = false;
                                    gotcontext = true;
                                    break;
                            }
                    }
                    else
                        switch (arg)
                        {
                            case "(":
                                gettingcontext = true;
                                break;
                            case "{":
                                if (gotcontext) { contexting = true; context = false; }
                                break;
                        }
                }
                else if (playnext)
                {
                    switch (arg)
                    {
                        case "-out":
                            if (playout == null) playout = true;
                            break;
                        case "-in":
                            if (playout == null) playout = false;
                            break;
                        default:
                            if (filetoplay == null) filetoplay = arg;
                            playnext = false;
                            break;
                    }
                }
                else
                    switch (arg)
                    {
                        case "/play":
                            playnext = true;
                            break;
                        case "/context":
                            if (!contexting) context = true;
                            break;
                        case "}":
                            if (contexting)
                            {
                                context = false;
                                gettingcontext = false;
                                gotcontext = false;
                                contexting = false;
                                template = default;
                            }
                            break;
                    }
                if (File.Exists(arg)) FilesToAdd.Add(arg, template);
            }

            if (FilesToAdd.Count > 0) await tasklist.AddFiles(FilesToAdd);
            if (filetoplay != null) tasklist.AddingCompleted += PlayFileCallback;
        }

        /// <summary>
        /// Modifie certains éléments graphiques en fonction de <see cref="Keyboard.Modifiers"/>.
        /// </summary>
        private void ApplyKeyboardModifiers()
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Alt:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_MashUp");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_MashDown");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_DeleteDFiles");
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFilesSN");
                    break;
                case ModifierKeys.Control:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_CustomUp");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_CustomDown");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_DeleteInvalidFiles");
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFolders");
                    if (Converting)
                    {
                        StartButton.SetResourceReference(ContentProperty, "MW_Cancel");
                        StartButton.SetResourceReference(ToolTipProperty, "MW_CancelToolTip");
                    }
                    break;
                case ModifierKeys.Shift:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_First");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_Last");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_DeleteAll");
                    AP.StopButton.SetResourceReference(ToolTipProperty, "AP_DeleteTempFiles");
                    AP.DownloadButton.SetResourceReference(ToolTipProperty, "AP_DownloadVLC");
                    break;
                case ModifierKeys.Alt | ModifierKeys.Control:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_CustomUpMash");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_CustomDownMash");
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFoldersSN");
                    break;
                case ModifierKeys.Alt | ModifierKeys.Shift:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_FirstMash");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_LastMash");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_DeleteSNFiles");
                    break;
                case ModifierKeys.Control | ModifierKeys.Shift:
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFolders");
                    break;
                case ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift:
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFoldersSN");
                    break;
                default:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_Up");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_Down");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_Delete");
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFiles");
                    AP.DownloadButton.SetResourceReference(ToolTipProperty, "AP_DownloadVGMStream");
                    AP.StopButton.SetResourceReference(ToolTipProperty, "AP_Stop");
                    if (Converting)
                    {
                        StartButton.SetResourceReference(ContentProperty, ConversionPTS.IsPaused ? "MW_Resume" : "MW_Pause");
                        StartButton.SetResourceReference(ToolTipProperty, ConversionPTS.IsPaused ? "MW_ResumeToolTip" : "MW_PauseToolTip");
                    }
                    break;
            }
        }

        #endregion

        #region Playing

        /// <summary>
        /// À l'aide de vgmstream, obtient un Stream contenant des données audio au format WAV à partir d'un fichier.
        /// </summary>
        /// <param name="fichier">Le fichier à décoder.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue; null pour utiliser les boutons "Apperçu dans le lecteur".</param>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        private async Task<Stream> VGFileToStream(Fichier fichier, bool? Out = null)
        {
            var result = await VGMStream.GetStreamWithOtherFormats(fichier, Settings.StreamingType == StreamingType.Cache && App.VLCVersion.Major >= 3, Out ?? (bool)ALSRadioButton.IsChecked, PlayingCTS.Token);

            if (result == null)
            {
                if (!PlayingCTS.IsCancellationRequested) PlayingCTS.Cancel();
            }
            else if (!fichier.Analyzed)
            {
                await tasklist.AnalyzeFile(fichier, false);
                fichier.SetValid();
            }

            return result;
        }

        /// <summary>
        /// Joue un fichier dans le lecteur audio.
        /// </summary>
        /// <param name="file">Le fichier à jouer.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue; null pour utiliser les boutons "Apperçu dans le lecteur".</param>
        /// <param name="force">true pour ignorer <see cref="Buffering"/>; sinon false.</param>
        /// <returns>Tâche qui représente l'opération de lecture asynchrone.</returns>
        private async Task PlayFile(Fichier file, bool? Out = null, bool force = false)
        {
            if ((!Buffering || force) && file != null && (AP.Player.State == MediaStates.Stopped || AP.Player.State == MediaStates.NothingSpecial || force))
            {
                if (File.Exists(file.Path))
                {
                    try
                    {
                        Buffering = true;

                        PlayingCTS = new CancellationTokenSource();
                        if (AP.Player.State != MediaStates.Stopped && AP.Player.State != MediaStates.NothingSpecial && AP.Player.State != MediaStates.Ended) await AP.Stop();
                        AP.CurrentPlaying = file;

                        Canvas loadingcircle = (Application.Current.Resources["LoadingCircleAnimated"] as Canvas);
                        (loadingcircle.Children[0] as ContentPresenter).Content = Application.Current.Resources["LoadingCircle10"];
                        AP.PlayButton.Content = loadingcircle;
                        var stream = await VGFileToStream(file, Out);
                        if (stream != null)
                        {
                            AP.SetAudio(stream);
                            if (tasklist.FILEList.Items.Contains(AP.CurrentPlaying)) AP.Playlist = tasklist.FILEList.Items.OfType<Fichier>().ToArray();
                            else if (tasklist.Files.Contains(AP.CurrentPlaying)) AP.Playlist = tasklist.Files.ToArray();
                            await AP.Play();
                            file.Played = true;
                        }
                        else if (!PlayingCTS.IsCancellationRequested) PlayingCTS.Cancel();

                        Buffering = false;
                    }
                    finally
                    {
                        if (PlayingCTS.IsCancellationRequested)
                        {
                            Buffering = false;
                            await AP.Stop(true);
                        }
                    }
                }
                else file.SetInvalid("ERR_FileNotFound");
            }
        }

        public void UpdatePlaylist()
        {
            if (AP.CurrentPlaying != null && AP.Playlist != null)
            {
                if (tasklist.FILEList.Items.Contains(AP.CurrentPlaying)) AP.Playlist = tasklist.FILEList.Items.OfType<Fichier>().ToArray();
                else if (!tasklist.Files.Contains(AP.CurrentPlaying)) AP.Playlist = null;
            }
        }

        #region Controls

        /// <summary>
        /// Lit le fichier précédent dans la liste.
        /// </summary>
        private async Task Previous()
        {
            if (Passing || AP.Playlist == null || AP.CurrentPlaying == null) return;
            Passing = true;

            int cp = AP.Playlist.IndexOf(AP.CurrentPlaying);
            if (cp > -1)
            {
                if (cp == 0) cp = AP.Playlist.Count; //Permet de jouer le dernier fichier de la liste si le premier est en cours de lecture

                await PlayFile(AP.Playlist[cp - 1], force: true);
            }

            Passing = false;
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste.
        /// </summary>
        private async Task Next()
        {
            if (Passing || AP.Playlist == null || AP.CurrentPlaying == null) return;
            Passing = true;

            int cp = AP.Playlist.IndexOf(AP.CurrentPlaying);
            if (cp > -1)
            {
                if (cp == AP.Playlist.Count - 1) cp = -1; //Permet de jouer le premier fichier de la liste si le dernier est en cours de lecture

                await PlayFile(AP.Playlist[cp + 1], force: true);
            }

            Passing = false;
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste ou un fichier aléatoire.
        /// </summary>
        private async Task NextWithRandom()
        {
            switch (AP.LoopType)
            {
                case LoopTypes.Random:
                    if (Passing || AP.Playlist == null || AP.Playlist.Count <= 0 || AP.CurrentPlaying == null) return;
                    Passing = true;

                    var playlist = AP.Playlist.Where(f => !f.Played).ToArray();
                    if (playlist.Length == 0)
                    {
                        foreach (Fichier fichier in AP.Playlist) fichier.Played = fichier == AP.CurrentPlaying;
                        playlist = AP.Playlist.Where(f => !f.Played).ToArray();
                    }
                    if (playlist.Length != 0) await PlayFile(playlist[new Random().Next(0, playlist.Length - 1)], force: true);

                    Passing = false;
                    break;
                default:
                    await Next();
                    break;
            }
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste ou un fichier aléatoire.
        /// </summary>
        private async Task PreviousWithRandom()
        {
            switch (AP.LoopType)
            {
                case LoopTypes.Random:
                    if (Passing || AP.Playlist == null || AP.Playlist.Count <= 0 || AP.CurrentPlaying == null) return;
                    Passing = true;

                    var playlist = AP.Playlist.Where(f => !f.Played).ToArray();
                    if (playlist.Length == 0)
                    {
                        foreach (Fichier fichier in AP.Playlist) fichier.Played = fichier == AP.CurrentPlaying;
                        playlist = AP.Playlist.Where(f => !f.Played).ToArray();
                    }
                    if (playlist.Length != 0) await PlayFile(playlist[new Random().Next(0, playlist.Length - 1)], force: true);

                    Passing = false;
                    break;
                default:
                    await Previous();
                    break;
            }
        }

        /// <summary>
        /// Annule <see cref="PlayingCTS"/> et arrête la lecture.
        /// </summary>
        private async Task CancelAndStop()
        {
            if (Buffering) PlayingCTS.Cancel();
            else await AP.Stop(true);
        }

        #endregion

        #region UI

        /// <summary>
        /// Transforme <see cref="tbi_playpause"/> en bouton de lecture.
        /// </summary>
        public void TBISetPlay(bool resume)
        {
            tbi_playpause.Description = resume ? App.Str("TBI_Resume") : App.Str("TBI_Play");
            tbi_playpause.ImageSource = Properties.Resources.Play.ToSource();
        }

        /// <summary>
        /// Transforme <see cref="tbi_playpause"/> en bouton de pause.
        /// </summary>
        public void TBISetPause()
        {
            tbi_playpause.Description = App.Str("TBI_Pause");
            tbi_playpause.ImageSource = Properties.Resources.Pause.ToSource();
        }

        #endregion

        #endregion

        #region Conversion

        /// <summary>
        /// Convertit un fichier et lance le suivant dans <see cref='FilesToConvert'/>.
        /// </summary>
        /// <param name="fichier">Le fichier à convertir.</param>
        /// <returns>Tâche qui représente l'opération de conversion asynchrone.</returns>
        private async Task ConvertFile(Fichier fichier)
        {
            try
            {
                CurrentConverting++;

                await ConversionPTS.Token.WaitWhilePausedAsync().WithCancellation(ConversionCTS.Token);

                var data = await VGMStream.ConvertFileWithOtherFormats(fichier, ConversionCTS.Token, ConversionPTS.Token);
                if (data == null) ConversionErrorsCount++;
                else if (!fichier.Analyzed) await tasklist.AnalyzeFile(fichier, data);

                await ConversionPTS.Token.WaitWhilePausedAsync().WithCancellation(ConversionCTS.Token);

                if (!ConversionCTS.IsCancellationRequested && (!ConversionMultithreading || ConversionMaxProcessCount > 0) && FilesToConvert.Count > 0 && CurrentConverting < 1000)
                {
                    ConvertFile(FilesToConvert.Dequeue());
                }
            }
            catch (OperationCanceledException) { return; }
            finally
            {
                CurrentConverting--;

                MainProgress.Value = ConversionProgress;
                tii_main.ProgressValue = (double)ConversionProgress / ConversionCount;

                conversionInfosLabel.Content = $"{ConversionProgress} / {ConversionCount} - {(100 * (double)ConversionProgress / ConversionCount).ToString("00.00")} %";

                if (CurrentConverting == 0 && (ConversionCTS.IsCancellationRequested || FilesToConvert.Count == 0)) //S'exécute à la toute fin de la conversion
                {
                    await Finish();
                }
            }
        }

        /// <summary>
        /// Démarre la conversoin.
        /// </summary>
        private void StartConversion()
        {
            var fta = new ObservableCollection<AskingFile>(FilesToAsk);
            if (fta.Count > 0)
            {
                var askWindow = new AskWindow(fta);
                var files = askWindow.ShowDialog();
                if (files == null) FilesToConvertTMP = new List<Fichier>();
                else
                {
                    foreach (AskingFile file in files)
                    {
                        switch (file.Action)
                        {
                            case FileActions.Overwrite:
                                if (file.Fichier.FinalDestination == file.Fichier.Path)
                                {
                                    file.Fichier.SetInvalid("ERR_SameSourceAndDest");
                                    FilesToConvertTMP.Remove(file.Fichier);
                                }
                                else file.Fichier.ToNumber = false;
                                break;
                            case FileActions.Number:
                                file.Fichier.ToNumber = true;
                                break;
                            case FileActions.Ignore:
                                FilesToConvertTMP.Remove(file.Fichier);
                                break;
                        }
                    }
                }
                FilesToAsk = null;
            }

            FilesToConvert = new Queue<Fichier>(FilesToConvertTMP);

            if ((ConversionCount = FilesToConvert.Count) > 0)
            {
                var convertMI = App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI");
                App.FileListItemCMItems.FindCollectionItem<MenuItem>("SkipMI").Visibility = Visibility.Visible;
                convertMI.IsEnabled = App.FileListItemCMItems.FindCollectionItem<MenuItem>("DeleteMI").IsEnabled = tasklist.CanRemove = tasklist.CanAdd = false;
                convertMI.Visibility = Visibility.Collapsed;

                ConversionCTS = new CancellationTokenSource();
                ConversionPTS = new PauseTokenSource();

                MainProgress.Maximum = ConversionCount;
                tii_main.ProgressState = TaskbarItemProgressState.Normal;
                StartButton.SetResourceReference(ContentProperty, Keyboard.Modifiers == ModifierKeys.Control ? "MW_Cancel" : ConversionPTS.IsPaused ? "MW_Resume" : "MW_Pause");
                StartButton.SetResourceReference(ToolTipProperty, Keyboard.Modifiers == ModifierKeys.Control ? "MW_CancelToolTip" : ConversionPTS.IsPaused ? "MW_ResumeToolTip" : "MW_PauseToolTip");

                Preconversion = false;
                AnimateConversionIcon(false, false, true);

                if (ConversionMultithreading) //Start
                {
                    if (ConversionMaxProcessCount <= 0) //Illimité
                    {
                        while (FilesToConvert.Count > 0) ConvertFile(FilesToConvert.Dequeue());
                    }
                    else //Maximum
                    {
                        for (int i = 0; i < ConversionMaxProcessCount && FilesToConvert.Count > 0; i++)
                        {
                            ConvertFile(FilesToConvert.Dequeue());
                        }
                    }
                }
                else ConvertFile(FilesToConvert.Dequeue()); //Singlethreading
            }
            else
            {
                Preconversion = false;
                AnimateConversionIcon(true, true, true);
            }
        }

        /// <summary>
        /// Se produit une fois la conversion terminée.
        /// </summary>
        private async Task Finish()
        {
            if (ConversionCTS.IsCancellationRequested)
            {
                if (MessageBox.Show(App.Str("Q_DeleteCanceled"), string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    foreach (var fichier in FilesToConvertTMP) if (fichier.OriginalState == "FSTATE_Canceled") File.Delete(fichier.FinalDestination);
                }
            }
            else MessageBox.Show(App.Str("INFO_ConversionCompleted"), App.Str("INFO_Info"), MessageBoxButton.OK, MessageBoxImage.Information);

            ConversionCount = ConversionErrorsCount = 0;
            MainProgress.Value = tii_main.ProgressValue = 0;
            conversionInfosLabel.Content = App.Str("ST_Pending");

            foreach (var fichier in tasklist.Files) fichier.ToNumber = null;

            FilesToConvertTMP = null;
            FilesToConvert = new Queue<Fichier>();
            tii_main.ProgressState = TaskbarItemProgressState.None;
            MainProgress.Maximum = 100;

            var convertMI = App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI");
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("SkipMI").Visibility = Visibility.Collapsed;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("DeleteMI").IsEnabled = convertMI.IsEnabled = tasklist.CanRemove = tasklist.CanAdd = true;
            convertMI.Visibility = Visibility.Visible;

            StartButton.SetResourceReference(ContentProperty, "MW_StartConversion");
            StartButton.SetResourceReference(ToolTipProperty, "MW_StartConversionToolTip");

            AnimateConversionIcon(true, true, true);

            await VGMStream.DeleteTempFilesByType(VGMStreamProcessTypes.Conversion);
            GC.Collect();
        }

        #region Controls

        /// <summary>
        /// Suspend la conversion.
        /// </summary>
        private async Task PauseConversion(bool editUI = true)
        {
            ConversionPTS.IsPaused = true;

            foreach (var kvp in VGMStream.RunningProcess.Where(kvp => kvp.Value == VGMStreamProcessTypes.Conversion).ToArray()) kvp.Key.TrySuspend();

            if (editUI)
            {
                foreach (var fichier in tasklist.Files.Where(f => f.OriginalState == "FSTATE_Conversion").ToArray()) fichier.OriginalState = "FSTATE_Suspended";

                StartButton.SetResourceReference(ContentProperty, "MW_Resume");
                StartButton.SetResourceReference(ToolTipProperty, "MW_ResumeToolTip");

                await AnimateConversionIcon(true, true, false);
            }
        }

        /// <summary>
        /// Reprend la conversion.
        /// </summary>
        private async Task ResumeConversion(bool editUI = true)
        {
            ConversionPTS.IsPaused = false;

            foreach (var kvp in VGMStream.RunningProcess.Where(kvp => kvp.Value == VGMStreamProcessTypes.Conversion).ToArray()) kvp.Key.TryResume();

            if (editUI)
            {
                foreach (var fichier in tasklist.Files.Where(f => f.OriginalState == "FSTATE_Suspended").ToArray()) fichier.OriginalState = "FSTATE_Conversion";

                StartButton.SetResourceReference(ContentProperty, "MW_Pause");
                StartButton.SetResourceReference(ToolTipProperty, "MW_PauseToolTip");

                await AnimateConversionIcon(false, true, false);
            }
        }

        /// <summary>
        /// Annule la conversion.
        /// </summary>
        private void StopConversion()
        {
            if (MessageBox.Show(App.Str("Q_Cancel"), string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                ConversionCTS.Cancel();
                ResumeConversion(false);
            }
        }

        /// <summary>
        /// Suspend ou reprend la conversion.
        /// </summary>
        private async Task PauseOrResumeConversion()
        {
            if (!ConversionPTS.IsPaused) await PauseConversion();
            else await ResumeConversion();
        }

        #endregion

        #endregion

        #endregion

        #region Events

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            var columns = (tasklist.FILEList.View as GridView).Columns;
            int[] columnsindexes = new int[columns.Count];
            double[] columnswhiths = new double[columns.Count];
            StringBuilder sb = new StringBuilder();

            foreach (GridViewColumn column in columns)
            {
                if (column.Header is string s)
                {
                    switch (App.Res(s, indice: "FL_COL_"))
                    {
                        case "FL_COL_Name":
                            columnsindexes[1] = columns.IndexOf(column);
                            columnswhiths[1] = column.Width;
                            break;
                        case "FL_COL_State":
                            columnsindexes[2] = columns.IndexOf(column);
                            columnswhiths[2] = column.Width;
                            break;
                        case "FL_COL_Duration":
                            columnsindexes[3] = columns.IndexOf(column);
                            columnswhiths[3] = column.Width;
                            break;
                        case "FL_COL_Format":
                            columnsindexes[4] = columns.IndexOf(column);
                            columnswhiths[4] = column.Width;
                            break;
                        case "FL_COL_Encoding":
                            columnsindexes[5] = columns.IndexOf(column);
                            columnswhiths[5] = column.Width;
                            break;
                        case "FL_COL_SampleRate":
                            columnsindexes[6] = columns.IndexOf(column);
                            columnswhiths[6] = column.Width;
                            break;
                        case "FL_COL_Bitrate":
                            columnsindexes[7] = columns.IndexOf(column);
                            columnswhiths[7] = column.Width;
                            break;
                        case "FL_COL_Channels":
                            columnsindexes[8] = columns.IndexOf(column);
                            columnswhiths[8] = column.Width;
                            break;
                        case "FL_COL_Loop":
                            columnsindexes[9] = columns.IndexOf(column);
                            columnswhiths[9] = column.Width;
                            break;
                        case "FL_COL_Layout":
                            columnsindexes[10] = columns.IndexOf(column);
                            columnswhiths[10] = column.Width;
                            break;
                        case "FL_COL_Interleave":
                            columnsindexes[11] = columns.IndexOf(column);
                            columnswhiths[11] = column.Width;
                            break;
                        case "FL_COL_Folder":
                            columnsindexes[12] = columns.IndexOf(column);
                            columnswhiths[12] = column.Width;
                            break;
                        case "FL_COL_Size":
                            columnsindexes[13] = columns.IndexOf(column);
                            columnswhiths[13] = column.Width;
                            break;
                        case "FL_COL_Date":
                            columnsindexes[14] = columns.IndexOf(column);
                            columnswhiths[14] = column.Width;
                            break;
                    }
                }
                else if (column.Header is CheckBox chbx)
                {
                    columnsindexes[0] = columns.IndexOf(column);
                    columnswhiths[0] = column.Width;
                }
            }

            if (!columnsindexes.OrderBy(i => i).Select((i, j) => i - j).Distinct().Skip(1).Any())
            {
                for (int i = 0; i < columnsindexes.Length; i++)
                {
                    sb.Append(columnsindexes[i]);
                    sb.Append(" : ");
                    sb.Append(columnswhiths[i]);

                    if (i < columnsindexes.Length - 1) sb.Append(" | ");
                }
                SettingsData.Global["ColumnsInfo"] = sb.ToString();
            }

            SettingsData["Window"]["State"] = WindowState != WindowState.Maximized ? "Normal" : "Maximized";
            SettingsData["Window"]["Width"] = RestoreBounds.Width.ToString();
            SettingsData["Window"]["Height"] = RestoreBounds.Height.ToString();

            SettingsData["Grids"]["TopGrid"] = TopGrid.ColumnDefinitions[0].Width + " | " + TopGrid.ColumnDefinitions[1].Width;
            SettingsData["Grids"]["RightGrid"] = RightGrid.RowDefinitions[0].Height + " | " + RightGrid.RowDefinitions[1].Height;

            SettingsData.Global["ConversionFolderName"] = MainDestTB.Text;

            SettingsData["Search"]["SearchFilter"] = RestoreSearchFilter;
            SettingsData["Search"]["SearchColumn"] = SearchColumn.ToString();
            SettingsData["Search"]["CaseSensitive"] = SearchCaseSensitive.ToString();
            SettingsData["Search"]["Regex"] = SearchRegex.ToString();

            await TryWriteSettings();

            await CancelAndStop();
            VGMStream.VLCCTS?.Cancel();
            MessageBoxManager.Unregister();
            await VGMStream.DeleteTempFiles(false);

            e.Cancel = false;
            Closing -= Window_Closing;
            this.TryClose();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() => App.CurrentCulture = App.CurrentCulture); //Empêche les propriétés CultureInfo.CurrentCulture et CultureInfo.CurrentUICulture de se réinitialiser au démarrage
            UpdateStatusBar(streamingType: true);
            RAMTimer.Start();
            ShowHideStatusBar(StatusBar.Display);
            DisplayRecentFiles();
            LoopCountBox.AllowedStrings = FadeDelayBox.AllowedStrings = FadeTimeBox.AllowedStrings = App.AllowedStbxTxt;
            await ApplyArgs();
            LoopCountLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeDelayLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeTimeLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            m_conversionIcon = ((ConversionIcon.Content as Viewbox).Child as Canvas).Children.OfType<System.Windows.Shapes.Path>().ToArray();
        }

        private void Window_Activated(object sender, EventArgs e) => ApplyKeyboardModifiers();

        private void App_LanguageChanged(object sender, PropertyChangedExtendedEventArgs<string> e)
        {
            UpdateStatusBar(streamingType: true);
            LoopCountBox.AllowedStrings = FadeDelayBox.AllowedStrings = FadeTimeBox.AllowedStrings = App.AllowedStbxTxt;
            RefreshInfos();
            switch (App.Res(tbi_playpause.Description, e.OldValue, "TBI_"))
            {
                case "TBI_Play":
                    tbi_playpause.Description = App.Str("TBI_Play");
                    break;
                case "TBI_Resume":
                    tbi_playpause.Description = App.Str("TBI_Resume");
                    break;
                case "TBI_Pause":
                    tbi_playpause.Description = App.Str("TBI_Pause");
                    break;
            }

            LoopCountLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeDelayLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeTimeLabel.GetBindingExpression(ContentProperty).UpdateTarget();
        }

        private async void AP_EndReached(object sender, Vlc.DotNet.Core.VlcMediaPlayerEndReachedEventArgs e)
        {
            if (AP.LoopType != LoopTypes.None)
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await AP.Stop();
                    await NextWithRandom();
                });
            }
            else await Dispatcher.InvokeAsync(() => AP.Stop(true));
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Preconversion)
            {
                if (CurrentConverting <= 0) await LaunchConversion(await tasklist.Files.WhereAsync(fichier => fichier.Checked));
                else
                {
                    switch (Keyboard.Modifiers)
                    {
                        case ModifierKeys.Control:
                            StopConversion();
                            break;
                        default:
                            await PauseOrResumeConversion();
                            break;
                    }
                }
            }
        }

        public async Task LaunchConversion(IEnumerable<Fichier> files)
        {
            Preconversion = true;

            if ((File.Exists(App.VGMStreamPath) || await App.AskVGMStream()))
            {
                if (!MainDestTB.Text.ContainsAny(Path.GetInvalidFileNameChars()) && !IO.ReservedFilenames.Contains(MainDestTB.Text.ToUpper()))
                {
                    if (files.Any())
                    {
                        AnimateConversionIcon(false, true, false);
                        await SetFinalDestinationsAsync(files);
                        StartConversion();
                    }
                    else Preconversion = false;
                }
                else
                {
                    MessageBox.Show($"{App.Str("ERR_InvalidDestination")}{Environment.NewLine + Environment.NewLine}{App.Str("ERR_UnauthorizedCharacters")}{Environment.NewLine + Environment.NewLine}{App.Str("ERR_SystemReservedFilenames")}", App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    Preconversion = false;
                }
            }
            else Preconversion = false;
        }

        private async Task AnimateConversionIcon(bool stop, bool rotation, bool color)
        {
            if (stop)
            {
                var gear = m_conversionIcon[0];
                double angle = gear.RenderTransform is RotateTransform rotateTransform ? rotateTransform.Angle : 0;
                var arrow1 = m_conversionIcon[1];
                var arrow2 = m_conversionIcon[2];

                IEnumerable<Task> GetAnimations()
                {
                    if (rotation) yield return AnimateDouble("ConversionRotation", value => gear.RenderTransform = new RotateTransform(value), angle, 360, TimeSpan.FromSeconds(0.75), default, false, false, new QuadraticEase { EasingMode = EasingMode.EaseOut }, 60);
                    if (color) yield return AnimateColor("ConversionColor", value => arrow1.Fill = arrow2.Fill = new SolidColorBrush(value), (arrow1.Fill as SolidColorBrush).Color, (Application.Current.Resources["StatusBarIconBrush"] as SolidColorBrush).Color, TimeSpan.FromSeconds(1), default, false, false, null, 60);
                }

                await Task.WhenAll(GetAnimations());
            }
            else
            {
                if (rotation)
                {
                    var gear = m_conversionIcon[0];
                    await AnimateDouble("ConversionRotation", value => gear.RenderTransform = new RotateTransform(value), 0, 360, TimeSpan.FromSeconds(1.5), RepeatBehavior.Forever, false, false, null, 60);
                }
                if (color)
                {
                    var arrow1 = m_conversionIcon[1];
                    var arrow2 = m_conversionIcon[2];
                    await AnimateColor("ConversionColor", value => arrow1.Fill = arrow2.Fill = new SolidColorBrush(value), (arrow1.Fill as SolidColorBrush).Color, (Application.Current.Resources["ForegroundBrush"] as SolidColorBrush).Color, TimeSpan.FromSeconds(1), default, false, false, null, 60);
                    arrow1.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ForegroundBrush");
                    arrow2.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ForegroundBrush");
                }
            }
        }

        private async void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (StopPlayingWhenDeleteFile && e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems.Contains(AP.CurrentPlaying)) await CancelAndStop();
            }

            UpdateStatusBar();
            UpdatePlaylist();
        }

        private void Tasklist_FilterChanged(object sender, EventArgs e)
        {
            UpdateStatusBar();
            UpdatePlaylist();
        }

        private void statusBar_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => ShowHideStatusBar((bool)e.NewValue);

        private void SamplesDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            switch (samplesDisplayLabel.Content)
            {
                case "x sec":
                    HMSSamplesDisplay = true;
                    SettingsData.Global["SamplesDisplay"] = "HMS";
                    samplesDisplayLabel.Content = "xx:xx";
                    break;
                case "xx:xx":
                    HMSSamplesDisplay = false;
                    SettingsData.Global["SamplesDisplay"] = "S";
                    samplesDisplayLabel.Content = "x sec";
                    break;
                default: return;
            }

            RefreshInfos();
        }

        private void StreamingTypeButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Settings.StreamingType)
            {
                case StreamingType.Cache:
                    Settings.StreamingType = StreamingType.Live;
                    break;
                case StreamingType.Live:
                    Settings.StreamingType = StreamingType.Cache;
                    break;
            }
            UpdateStatusBar(streamingType: true);
        }

        private void SizeButton_Click(object sender, RoutedEventArgs e)
        {
            Height = 675;
            Width = 1100;

            var columns = (tasklist.FILEList.View as GridView).Columns;

            columns[0].Width = 27;
            columns[1].Width = 200;
            columns[2].Width = 112;
            columns[3].Width = 80;
            columns[4].Width = 200;
            columns[5].Width = 200;
            columns[6].Width = 70;
            columns[7].Width = 70;
            columns[8].Width = 60;
            columns[9].Width = 50;
            columns[10].Width = 180;
            columns[11].Width = 90;
            columns[12].Width = 300;
            columns[13].Width = 70;
            columns[14].Width = 150;

            TopGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            TopGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

            RightGrid.RowDefinitions[0].Height = new GridLength(2, GridUnitType.Star);
            RightGrid.RowDefinitions[1].Height = new GridLength(1.27, GridUnitType.Star);
        }

        #region AudioPlayer

        private async void PlayButton_Click(object sender, RoutedEventArgs e) => await PlayFile(tasklist.FILEList.SelectedItem as Fichier);

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    tasklist.MoveListViewItems(MoveDirection.First);
                    break;
                case ModifierKeys.Control:
                    tasklist.MoveListViewItems(MoveDirection.CustomUp);
                    break;
                case ModifierKeys.Alt:
                    e.Handled = true;
                    tasklist.MoveListViewItems(MoveDirection.MashUp);
                    break;
                case ModifierKeys.Alt | ModifierKeys.Shift:
                    e.Handled = true;
                    tasklist.MoveListViewItems(MoveDirection.FirstMash);
                    break;
                case ModifierKeys.Alt | ModifierKeys.Control:
                    tasklist.MoveListViewItems(MoveDirection.CustomUpMash);
                    break;
                default:
                    tasklist.MoveListViewItems(MoveDirection.Up);
                    break;
            }
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    tasklist.MoveListViewItems(MoveDirection.Last);
                    break;
                case ModifierKeys.Control:
                    tasklist.MoveListViewItems(MoveDirection.CustomDown);
                    break;
                case ModifierKeys.Alt:
                    e.Handled = true;
                    tasklist.MoveListViewItems(MoveDirection.MashDown);
                    break;
                case ModifierKeys.Alt | ModifierKeys.Shift:
                    e.Handled = true;
                    tasklist.MoveListViewItems(MoveDirection.LastMash);
                    break;
                case ModifierKeys.Alt | ModifierKeys.Control:
                    tasklist.MoveListViewItems(MoveDirection.CustomDownMash);
                    break;
                default:
                    tasklist.MoveListViewItems(MoveDirection.Down);
                    break;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => tasklist.OpenFileDialog((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt);

        private void RemButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Control:
                    tasklist.RemoveInvalidItems();
                    break;
                case ModifierKeys.Shift:
                    tasklist.RemoveAll();
                    break;
                case ModifierKeys.Alt:
                    tasklist.RemoveDFiles();
                    break;
                case ModifierKeys.Shift | ModifierKeys.Alt:
                    tasklist.RemoveSNFiles();
                    break;
                default:
                    tasklist.RemoveSelectedItems();
                    break;
            }

        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await PreviousWithRandom();

        private async void NextButton_Click(object sender, RoutedEventArgs e) => await NextWithRandom();

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    await VGMStream.DeleteTempFilesIfNotUsed();
                    GC.Collect();
                    break;
                default:
                    await CancelAndStop();
                    break;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    if (await VGMStream.DownloadVLC()) MessageBox.Show(App.Str("WW_VLCDownloaded"), string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    await VGMStream.DownloadVGMStream();
                    break;
            }
        }

        private void AP_LoopTypeChanged(object sender, PropertyChangedExtendedEventArgs<LoopTypes> e)
        {
            foreach (Fichier fichier in tasklist.Files) fichier.Played = fichier == AP.CurrentPlaying;
        }

        private void AP_Stopped(object sender, EventArgs<bool> e)
        {
            if (e.Param1) foreach (Fichier fichier in tasklist.Files) fichier.Played = false;
        }

        #endregion

        #region Clipboard

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            FichierOutData outData = new FichierOutData()
            {
                OriginalDestination = App.Res(DestCB.Text) ?? DestCB.Text,
                FadeDelay = FadeDelayBox.Text.ToDouble(),
                FadeOut = FadeOutCheckBox.IsChecked,
                FadeTime = FadeTimeBox.Text.ToDouble(),
                LoopCount = LoopCountBox.Text.ToInt(),
                StartEndLoop = StartEndLoopCheckBox.IsChecked
            };
            Threading.MultipleAttempts(() => Clipboard.SetData("VGMGUIOutData", outData), throwEx: false);
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.GetData("VGMGUIOutData") is FichierOutData outData)
            {
                CanEditFichier = false;

                LoopCountBox.Text = outData.LoopCount.ToString();
                StartEndLoopCheckBox.IsChecked = outData.StartEndLoop ?? false;
                FadeOutCheckBox.IsChecked = outData.FadeOut ?? false;
                FadeDelayBox.Text = outData.FadeDelay.ToString();
                FadeTimeBox.Text = outData.FadeTime.ToString();
                if (!outData.OriginalDestination.IsNullOrEmpty()) DestCB.Text = App.Str(outData.OriginalDestination) ?? outData.OriginalDestination;

                CanEditFichier = true;

                WriteInfo(MediaInfos.All);
            }
        }

        private void SetAsDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            DefaultOutData = new FichierOutData()
            {
                OriginalDestination = App.Res(DestCB.Text) ?? DestCB.Text,
                FadeDelay = FadeDelayBox.Text.ToDouble(),
                FadeOut = FadeOutCheckBox.IsChecked,
                FadeTime = FadeTimeBox.Text.ToDouble(),
                LoopCount = LoopCountBox.Text.ToInt(),
                StartEndLoop = StartEndLoopCheckBox.IsChecked
            };
        }

        private void ClipboardNotification_ClipboardUpdate(object sender, EventArgs e) => PasteButton.IsEnabled = Clipboard.ContainsData("VGMGUIOutData");

        #endregion

        public ICommand TBI_Previous => new RelayCommand(async () => await Previous());
        public ICommand TBI_Stop => new RelayCommand(async () => await CancelAndStop());
        public ICommand TBI_Next => new RelayCommand(async () => await NextWithRandom());
        public ICommand TBI_PlayPause => new RelayCommand(async () => await AP.PlayPause());

        #endregion
    }
}
