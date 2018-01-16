using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Shell;
using System.IO;
using BenLib;
using CSCore.SoundOut;
using Clipboard = System.Windows.Forms.Clipboard;
using static VGMGUI.Settings;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Champs & Propriétés

        #region Playing

        /// <summary>
        /// Indique si un fichier est actuellement en train d'être décodé pour la lecture.
        /// </summary>
        public bool Buffering { get; set; }

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
        public List<Fichier> FilesToConvertTMP { get; set; } = new List<Fichier>();

        /// <summary>
        /// Contient les fichiers cochés et valides à convertir.
        /// </summary>
        public Queue<Fichier> FilesToConvert { get; set; } = new Queue<Fichier>();

        /// <summary>
        /// Contient les fichiers nécessitant une demande (écraser/ignorer/numéroter)
        /// </summary>
        public ObservableCollection<AskingFile> FilesToAsk = new ObservableCollection<AskingFile>();

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

            DataContext = this;

            ApplySettings(Settings.SettingsData, true);
            PasteButton.IsEnabled = Clipboard.ContainsData("VGMGUIOutData");

            for (int i = 0; i < Infos.Length; i++) Infos[i] = new Dictionary<object, int>(); //Création des dictionnaires dans "Infos"

            #region Images

            tbi_previous.ImageSource = Properties.Resources.Previous.ToSource();
            tbi_playpause.ImageSource = Properties.Resources.Play.ToSource();
            tbi_stop.ImageSource = Properties.Resources.Stop.ToSource();
            tbi_next.ImageSource = Properties.Resources.Next.ToSource();

            #endregion

            #region EventHandlers

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
            AP.StreamFinished += AP_StreamFinished;
            AP.Stopped += AP_Stopped;

            tasklist.FILEList.SelectionChanged += FILEList_SelectionChanged;
            tasklist.Files.CollectionChanged += Files_CollectionChanged;
            tasklist.Files.ItemChangedEvent += Files_ItemChangedEvent;
            tasklist.FilterChanged += Tasklist_FilterChanged;

            App.FileListItemCMItems.FindCollectionItem<MenuItem>("PlayInMI").Click += PlayInMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("PlayOutMI").Click += PlayOutMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI").Click += ConvertMI;

            App.LanguageChanged += App_LanguageChanged;

            ClipboardNotification.ClipboardUpdate += ClipboardNotification_ClipboardUpdate;

            #endregion
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Obtient le chemin du futur fichier converti.
        /// </summary>
        /// <param name="fichier">Le fichier dont on doit obtenir le chemin.</param>
        /// <returns>Le chemin du futur fichier.</returns>
        string GetOrCreateDestinationFile(Fichier fichier, bool showexception = false)
        {
            try
            {
                if (!File.Exists(fichier.Path))
                {
                    fichier.SetInvalid("ERR_FileNotFound");
                    return null;
                }
                string destination = String.Empty;
                if (fichier.Destination == App.Str("DEST_SourceFolder")) destination = Path.GetDirectoryName(fichier.Path);
                else if (fichier.Destination == App.Str("DEST_Principal"))
                {
                    if (MainDestCB.Text == App.Str("DEST_SourceFolder")) destination = Path.Combine(Path.GetDirectoryName(fichier.Path), MainDestTB.Text);
                    else destination = Path.Combine(MainDestCB.Text, MainDestTB.Text);
                }
                else destination = fichier.Destination;
                if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
                else if (!IO.WriteAccess(destination)) throw new UnauthorizedAccessException(App.Str("ERR_UnauthorizedAccess1") + destination + App.Str("ERR_UnauthorizedAccess2"));
                destination = Path.Combine(destination, Path.GetFileNameWithoutExtension(fichier.Path)) + ".wav";
                if (File.Exists(destination)) FilesToAsk.Add(new AskingFile(fichier));
                return destination;
            }
            catch (Exception ex)
            {
                if (showexception) MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                else fichier.SetInvalid(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Obtient le chemin du futur fichier converti.
        /// </summary>
        /// <param name="fichier">Le fichier dont on doit obtenir le chemin.</param>
        /// <returns>Le chemin du futur fichier.</returns>
        Task<string> GetOrCreateDestinationFileAsync(Fichier fichier, bool showexception = false)
        {
            string CBText = MainDestCB.Text, TBText = MainDestTB.Text;
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(fichier.Path))
                    {
                        Dispatcher.Invoke(() => fichier.SetInvalid("ERR_FileNotFound"));
                        return null;
                    }
                    string destination = String.Empty;
                    if (fichier.Destination == App.Str("DEST_SourceFolder")) destination = Path.GetDirectoryName(fichier.Path);
                    else if (fichier.Destination == App.Str("DEST_Principal"))
                    {
                        if (CBText == App.Str("DEST_SourceFolder")) destination = Path.Combine(Path.GetDirectoryName(fichier.Path), TBText);
                        else destination = Path.Combine(CBText, TBText);
                    }
                    else destination = fichier.Destination;
                    if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
                    else if (!IO.WriteAccess(destination)) throw new UnauthorizedAccessException(App.Str("ERR_UnauthorizedAccess1") + destination + App.Str("ERR_UnauthorizedAccess2"));
                    destination = Path.Combine(destination, Path.GetFileNameWithoutExtension(fichier.Path)) + ".wav";
                    if (File.Exists(destination)) FilesToAsk.Add(new AskingFile(fichier));
                    return destination;
                }
                catch (Exception ex)
                {
                    if (showexception) MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    else Dispatcher.Invoke(() => fichier.SetInvalid(ex.Message));
                    return null;
                }
            });
        }

        /// <summary>
        /// Ouvre une <see cref="SettingsWindow"/> et applique les paramètres retournés.
        /// </summary>
        void OpenSettingsWindow() { if (new SettingsWindow().ShowDialog()) ApplySettings(SettingsData); }

        #region Apply

        /// <summary>
        /// Effectue certaines actions en fonction de des arguments de l'application.
        /// </summary>
        void ApplyArgs()
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

            void playfilecallback(object sender, EventArgs e)
            {
                var ftp = tasklist.Files.Where(f => f.Path == filetoplay).ToList();
                if (ftp.Count > 0) PlayFile(ftp[0], playout);

                tasklist.AddingCompleted -= playfilecallback;
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

            if (FilesToAdd.Count > 0) tasklist.AddFiles(FilesToAdd);
            if (filetoplay != null) tasklist.AddingCompleted += playfilecallback;
        }

        /// <summary>
        /// Modifie certains éléments graphiques en fonction de <see cref="Keyboard.Modifiers"/>.
        /// </summary>
        void ApplyKeyboardModifiers()
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Alt:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_MashUp");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_MashDown");
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
                    break;
                case ModifierKeys.Alt | ModifierKeys.Control:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_CustomUpMash");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_CustomDownMash");
                    break;
                case ModifierKeys.Alt | ModifierKeys.Shift:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_FirstMash");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_LastMash");
                    break;
                default:
                    AP.UpButton.SetResourceReference(ToolTipProperty, "AP_Up");
                    AP.DownButton.SetResourceReference(ToolTipProperty, "AP_Down");
                    AP.RemButton.SetResourceReference(ToolTipProperty, "AP_Delete");
                    AP.AddButton.SetResourceReference(ToolTipProperty, "AP_AddFiles");
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
        /// <param name="useFile">true si un fichier temporaire doit être utilisé; false pour utiliser un <see cref="MemoryStream"/>.</param>
        /// <returns>Le Stream contenant les données audio.</returns>
        async Task<Stream> VGFileToStream(Fichier fichier, bool? Out = null, CancellationToken cancellationToken = default, bool useFile = true)
        {
            Stream stream = await VGMStream.GetStream(fichier, Out ?? (bool)ALSRadioButton.IsChecked, useFile, PlayingCTS.Token);

            if (stream == null) PlayingCTS.Cancel();
            else if (!fichier.Analyzed)
            {
                tasklist.AnalyzeFiles(new[] { fichier }, false);
                fichier.SetValid();
            }

            return stream;
        }

        /// <summary>
        /// Joue un fichier dans le lecteur audio.
        /// </summary>
        /// <param name="file">Le fichier à jouer.</param>
        /// <param name="Out">true si la sortie doit être lue; false si l'entrée doit être lue; null pour utiliser les boutons "Apperçu dans le lecteur".</param>
        /// <param name="force">true pour ignorer <see cref="Buffering"/>; sinon false.</param>
        /// <returns>Tâche qui représente l'opération de lecture asynchrone.</returns>
        async Task PlayFile(Fichier file, bool? Out = null, bool force = false)
        {
            if ((!Buffering || force) && file != null && (AP.PlaybackState == PlaybackState.Stopped || force))
            {
                if (File.Exists(file.Path))
                {
                    try
                    {
                        Buffering = true;

                        PlayingCTS = new CancellationTokenSource();
                        if (AP.PlaybackState != PlaybackState.Stopped) AP.Stop();
                        AP.CurrentPlaying = file;

                        Canvas loadingcircle = (Application.Current.Resources["LoadingCircleAnimated"] as Canvas);
                        (loadingcircle.Children[0] as ContentPresenter).Content = Application.Current.Resources["LoadingCircle10"];
                        AP.PlayButton.Content = loadingcircle;
                        Stream st = await VGFileToStream(file, Out, PlayingCTS.Token, UseFileForPlaying);
                        if (st != null)
                        {
                            AP.SetAudio(st);
                            if (tasklist.FILEList.Items.Contains(AP.CurrentPlaying)) AP.Playlist = (from Fichier fichier in tasklist.FILEList.Items select fichier).ToList();
                            else if (tasklist.Files.Contains(AP.CurrentPlaying)) AP.Playlist = tasklist.Files.ToList();
                            await AP.PlayAsync(PlayingCTS.Token);
                            file.Played = true;
                        }
                        else PlayingCTS.Cancel();

                        Buffering = false;
                    }
                    finally
                    {
                        if (PlayingCTS.IsCancellationRequested)
                        {
                            Buffering = false;
                            AP.Stop(true);
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
                if (tasklist.FILEList.Items.Contains(AP.CurrentPlaying)) AP.Playlist = (from Fichier fichier in tasklist.FILEList.Items select fichier).ToList();
                else if (!tasklist.Files.Contains(AP.CurrentPlaying)) AP.Playlist = null;
            }
        }

        #region Controls

        /// <summary>
        /// Lit le fichier précédent dans la liste.
        /// </summary>
        void Previous()
        {
            if (AP.Playlist != null && AP.CurrentPlaying != null)
            {
                int cp = AP.Playlist.IndexOf(AP.CurrentPlaying);
                if (cp > -1)
                {
                    if (cp == 0) cp = AP.Playlist.Count; //Permet de jouer le dernier fichier de la liste si le premier est en cours de lecture

                    AP.Stop();

                    PlayFile(AP.Playlist[cp - 1], force: true);
                }
            }
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste.
        /// </summary>
        void Next()
        {
            if (AP.Playlist != null && AP.CurrentPlaying != null)
            {
                int cp = AP.Playlist.IndexOf(AP.CurrentPlaying);
                if (cp > -1)
                {
                    if (cp == AP.Playlist.Count - 1) cp = -1; //Permet de jouer le premier fichier de la liste si le dernier est en cours de lecture

                    AP.Stop();

                    PlayFile(AP.Playlist[cp + 1], force: true);
                }
            }
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste ou un fichier aléatoire.
        /// </summary>
        void NextWithRandom()
        {
            switch (AP.LoopType)
            {
                case LoopTypes.Random:
                    if (AP.Playlist != null && AP.Playlist.Count > 0 && AP.CurrentPlaying != null)
                    {
                        var playlist = AP.Playlist.Where(f => !f.Played).ToList();
                        if (playlist.Count == 0)
                        {
                            foreach (Fichier fichier in AP.Playlist) fichier.Played = fichier == AP.CurrentPlaying;
                            playlist = AP.Playlist.Where(f => !f.Played).ToList();
                        }
                        if (playlist.Count != 0)
                        {
                            AP.Stop();
                            PlayFile(playlist[new Random().Next(0, playlist.Count - 1)], force: true);
                        }
                    }
                    break;
                default:
                    Next();
                    break;
            }
        }

        /// <summary>
        /// Lit le fichier suivant dans la liste ou un fichier aléatoire.
        /// </summary>
        void PreviousWithRandom()
        {
            switch (AP.LoopType)
            {
                case LoopTypes.Random:
                    if (AP.Playlist != null && AP.Playlist.Count > 0 && AP.CurrentPlaying != null)
                    {
                        var playlist = AP.Playlist.Where(f => !f.Played).ToList();
                        if (playlist.Count == 0)
                        {
                            foreach (Fichier fichier in AP.Playlist) fichier.Played = fichier == AP.CurrentPlaying;
                            playlist = AP.Playlist.Where(f => !f.Played).ToList();
                        }
                        if (playlist.Count != 0)
                        {
                            AP.Stop();
                            PlayFile(playlist[new Random().Next(0, playlist.Count - 1)], force: true);
                        }
                    }
                    break;
                default:
                    Previous();
                    break;
            }
        }

        /// <summary>
        /// Annule <see cref="PlayingCTS"/> et arrête la lecture.
        /// </summary>
        void CancelAndStop()
        {
            if (Buffering) PlayingCTS.Cancel();
            AP.Stop(true);
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
        async Task ConvertFile(Fichier fichier)
        {
            try
            {
                CurrentConverting++;

                await ConversionPTS.Token.WaitWhilePausedAsync();

                var data = await VGMStream.ConvertFile(fichier, ConversionCTS.Token).WithCancellation(ConversionCTS.Token);

                if (data != null && !fichier.Analyzed) tasklist.AnalyzeFile(fichier, data);

                await ConversionPTS.Token.WaitWhilePausedAsync();

                if ((!ConversionMultithreading || ConversionMaxProcessCount > 0) && FilesToConvert.Count > 0 && CurrentConverting < 1000)
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

                if (CurrentConverting == 0 && (ConversionCTS.IsCancellationRequested || FilesToConvert.Count == 0)) //S'exécute à la toute fin de la conversion
                {
                    Finish();
                }
            }
        }

        /// <summary>
        /// Démarre la conversoin.
        /// </summary>
        void StartConversion()
        {
            if (FilesToAsk.Count > 0)
            {
                AskWindow askWindow = new AskWindow(FilesToAsk);
                var files = askWindow.ShowDialog();
                if (files == null) FilesToConvertTMP = new List<Fichier>();
                else
                {
                    foreach (AskingFile file in files)
                    {
                        switch (file.Action)
                        {
                            case FileActions.Overwrite:
                                if (file.File.FinalDestination == file.File.Path)
                                {
                                    file.File.SetInvalid("ERR_SameSourceAndDest");
                                    FilesToConvertTMP.Remove(file.File);
                                }
                                break;
                            case FileActions.Number:
                                for (int i = 1; true; i++)
                                {
                                    var dest = file.File.FinalDestination;
                                    if (!File.Exists(file.File.FinalDestination = Path.Combine(
                                        Path.GetDirectoryName(file.File.FinalDestination),
                                        Path.GetFileNameWithoutExtension(file.File.FinalDestination) + " (" + i + ")" +
                                        Path.GetExtension(file.File.FinalDestination))))
                                    {
                                        break;
                                    }
                                    else file.File.FinalDestination = dest;
                                }
                                break;
                            case FileActions.Ignore:
                                FilesToConvertTMP.Remove(file.File);
                                break;
                        }
                    }
                }
                FilesToAsk = new ObservableCollection<AskingFile>();
            }

            FilesToConvert = new Queue<Fichier>(FilesToConvertTMP);

            if ((ConversionCount = FilesToConvert.Count) > 0)
            {
                App.FileListItemCMItems.FindCollectionItem<MenuItem>("DeleteMI").IsEnabled = App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI").IsEnabled = tasklist.CanRemove = tasklist.CanAdd = false;
                ConversionCTS = new CancellationTokenSource();
                ConversionPTS = new PauseTokenSource();
                MainProgress.Maximum = ConversionCount;
                tii_main.ProgressState = TaskbarItemProgressState.Normal;
                StartButton.SetResourceReference(ContentProperty, Keyboard.Modifiers == ModifierKeys.Control ? "MW_Cancel" : ConversionPTS.IsPaused ? "MW_Resume" : "MW_Pause");
                StartButton.SetResourceReference(ToolTipProperty, Keyboard.Modifiers == ModifierKeys.Control ? "MW_CancelToolTip" : ConversionPTS.IsPaused ? "MW_ResumeToolTip" : "MW_PauseToolTip");

                Preconversion = false;

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
            else Preconversion = false;
        }

        /// <summary>
        /// Se produit une fois la conversion terminée.
        /// </summary>
        void Finish()
        {
            if (ConversionCTS.IsCancellationRequested)
            {
                if (MessageBox.Show(App.Str("Q_DeleteCanceled"), String.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    foreach (Fichier f in FilesToConvertTMP)
                    {
                        if (f.OriginalState == "FSTATE_Canceled") File.Delete(f.FinalDestination);
                    }
                }
            }
            else MessageBox.Show(App.Str("INFO_ConversionCompleted"), App.Str("INFO_Info"), MessageBoxButton.OK, MessageBoxImage.Information);

            ConversionCount = 0;
            MainProgress.Value = tii_main.ProgressValue = 0;
            FilesToConvertTMP = new List<Fichier>();
            FilesToConvert = new Queue<Fichier>();
            tii_main.ProgressState = TaskbarItemProgressState.None;
            MainProgress.Maximum = 100;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("DeleteMI").IsEnabled = App.FileListItemCMItems.FindCollectionItem<MenuItem>("ConvertMI").IsEnabled = tasklist.CanRemove = tasklist.CanAdd = true;
            StartButton.SetResourceReference(ContentProperty, "MW_StartConversion");
            StartButton.SetResourceReference(ToolTipProperty, "MW_StartConversionToolTip");
            GC.Collect();
        }

        #region Controls

        /// <summary>
        /// Suspend la conversion.
        /// </summary>
        void PauseConversion(bool editUI = true)
        {
            ConversionPTS.IsPaused = true;

            foreach (Process process in (from KeyValuePair<Process, VGMStreamProcessTypes> kvp in VGMStream.RunningProcess where kvp.Value == VGMStreamProcessTypes.Conversion select kvp.Key))
            {
                process.TrySuspend();
            }

            if (editUI)
            {
                foreach (Fichier fichier in tasklist.Files.Where(f => f.OriginalState == "FSTATE_Conversion"))
                {
                    fichier.OriginalState = "FSTATE_Suspended";
                }

                StartButton.SetResourceReference(ContentProperty, "MW_Resume");
                StartButton.SetResourceReference(ToolTipProperty, "MW_ResumeToolTip");
            }
        }

        /// <summary>
        /// Reprend la conversion.
        /// </summary>
        void ResumeConversion(bool editUI = true)
        {
            ConversionPTS.IsPaused = false;

            foreach (Process process in (from KeyValuePair<Process, VGMStreamProcessTypes> kvp in VGMStream.RunningProcess where kvp.Value == VGMStreamProcessTypes.Conversion select kvp.Key))
            {
                process.TryResume();
            }

            if (editUI)
            {
                foreach (Fichier fichier in tasklist.Files.Where(f => f.OriginalState == "FSTATE_Suspended"))
                {
                    fichier.OriginalState = "FSTATE_Conversion";
                }

                StartButton.SetResourceReference(ContentProperty, "MW_Pause");
                StartButton.SetResourceReference(ToolTipProperty, "MW_PauseToolTip");
            }
        }

        /// <summary>
        /// Annule la conversion.
        /// </summary>
        void StopConversion()
        {
            if (MessageBox.Show(App.Str("Q_Cancel"), String.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                ResumeConversion(false);
                ConversionCTS.Cancel();
            }
        }

        /// <summary>
        /// Suspend ou reprend la conversion.
        /// </summary>
        void PauseOrResumeConversion()
        {
            if (!ConversionPTS.IsPaused) PauseConversion();
            else ResumeConversion();
        }

        #endregion

        #endregion

        #endregion

        #region Events

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
                        case "FL_COL_Channels":
                            columnsindexes[7] = columns.IndexOf(column);
                            columnswhiths[7] = column.Width;
                            break;
                        case "FL_COL_Loop":
                            columnsindexes[8] = columns.IndexOf(column);
                            columnswhiths[8] = column.Width;
                            break;
                        case "FL_COL_Layout":
                            columnsindexes[9] = columns.IndexOf(column);
                            columnswhiths[9] = column.Width;
                            break;
                        case "FL_COL_Interleave":
                            columnsindexes[10] = columns.IndexOf(column);
                            columnswhiths[10] = column.Width;
                            break;
                        case "FL_COL_Folder":
                            columnsindexes[11] = columns.IndexOf(column);
                            columnswhiths[11] = column.Width;
                            break;
                        case "FL_COL_Size":
                            columnsindexes[12] = columns.IndexOf(column);
                            columnswhiths[12] = column.Width;
                            break;
                        case "FL_COL_Date":
                            columnsindexes[13] = columns.IndexOf(column);
                            columnswhiths[13] = column.Width;
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

            await TryWriteSettings();

            CancelAndStop();
            MessageBoxManager.Unregister();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayRecentFiles();
            LoopCountBox.AllowedStrings = FadeDelayBox.AllowedStrings = FadeTimeBox.AllowedStrings = App.AllowedStbxTxt;
            ApplyArgs();
            LoopCountLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeDelayLabel.GetBindingExpression(ContentProperty).UpdateTarget();
            FadeTimeLabel.GetBindingExpression(ContentProperty).UpdateTarget();
        }

        private void Window_Activated(object sender, EventArgs e) => ApplyKeyboardModifiers();

        private void App_LanguageChanged(object sender, PropertyChangedExtendedEventArgs<string> e)
        {
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

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Preconversion)
            {
                if (CurrentConverting <= 0)
                {
                    Preconversion = true;

                    if (File.Exists(App.VGMStreamPath) || await App.AskVGMStream())
                    {
                        if (!MainDestTB.Text.ContainsAny(Literal.ForbiddenPathNameCharacters))
                        {
                            foreach (Fichier fichier in tasklist.Files)
                            {
                                if (fichier.Checked)
                                {
                                    if ((fichier.FinalDestination = await GetOrCreateDestinationFileAsync(fichier)) != null)
                                    {
                                        FilesToConvertTMP.Add(fichier);
                                        fichier.SetValid(); // <=> fichier.State = "En attente"
                                    }
                                }
                            } //Remplissage de "FilesToConvert"

                            StartConversion();
                        }
                        else MessageBox.Show(App.Str("ERR_UnauthorizedChars"), App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else Preconversion = false;
                }
                else
                {
                    switch (Keyboard.Modifiers)
                    {
                        case ModifierKeys.Control:
                            StopConversion();
                            break;
                        default:
                            PauseOrResumeConversion();
                            break;
                    }
                }
            }
        }

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (StopPlayingWhenDeleteFile && e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems.Contains(AP.CurrentPlaying)) CancelAndStop();
            }

            UpdatePlaylist();
        }

        private void Tasklist_FilterChanged(object sender, EventArgs e) => UpdatePlaylist();

        #region AudioPlayer

        private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayFile(tasklist.FILEList.SelectedItem as Fichier);

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

        private void AddButton_Click(object sender, RoutedEventArgs e) => tasklist.OpenFileDialog(Keyboard.Modifiers == ModifierKeys.Control);

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
                default:
                    tasklist.RemoveSelectedItems();
                    break;
            }

        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => PreviousWithRandom();

        private void NextButton_Click(object sender, RoutedEventArgs e) => NextWithRandom();

        private void StopButton_Click(object sender, RoutedEventArgs e) { if (Buffering) PlayingCTS.Cancel(); }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        private void DownloadButton_Click(object sender, RoutedEventArgs e) => VGMStream.DownloadVGMStream();

        private void AP_StreamFinished(object sender, EventArgs e)
        {
            if (AP.LoopType != LoopTypes.None) NextWithRandom();
        }

        private void AP_LoopTypeChanged(object sender, PropertyChangedExtendedEventArgs<LoopTypes> e)
        {
            foreach (Fichier fichier in tasklist.Files) fichier.Played = fichier == AP.CurrentPlaying;
        }

        private void AP_Stopped(object sender, AudioPlayerStopEventArgs e)
        {
            if (e.End)
                foreach (Fichier fichier in tasklist.Files) fichier.Played = false;
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

        public ICommand TBI_Previous => new RelayCommand(() => Previous());
        public ICommand TBI_Stop => new RelayCommand(CancelAndStop);
        public ICommand TBI_Next => new RelayCommand(() => NextWithRandom());
        public ICommand TBI_PlayPause => new RelayCommand(AP.PlayPause);

        #endregion
    }
}
