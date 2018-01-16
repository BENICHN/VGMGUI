using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.IO;
using BenLib;
using BenLib.WPF;
using System.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs.Controls;
using System.Windows.Input;
using System.Globalization;
using static VGMGUI.Settings;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour FileList.xaml
    /// </summary>
    public partial class FileList : UserControl
    {
        #region Champs & Propriétés

        #region List

        /// <summary>
        /// Liste des fichiers.
        /// </summary>
        ItemsChangeObservableCollection<Fichier, object> filesCollection = new ItemsChangeObservableCollection<Fichier, object>();

        /// <summary>
        /// Liste des fichiers.
        /// </summary>
        public ItemsChangeObservableCollection<Fichier, object> Files => filesCollection;

        public event EventHandler FilterChanged;

        #region CheckBoxes

        /// <summary>
        /// Indique si <see cref="SelectAll_Checked"/> ou <see cref="SelectAll_UnChecked"/> a été appelé par <see cref="SelectOne_Checked"/> ou <see cref="SelectOne_UnChecked"/>
        /// </summary>
        bool m_selectone;

        /// <summary>
        /// Indique si <see cref="SelectOne_UnChecked"/> ou <see cref="SelectOne_Checked"/> a été appelé par <see cref="SelectAll_Checked"/> ou <see cref="SelectAll_UnChecked"/>
        /// </summary>
        bool m_selectall;

        #endregion

        #endregion

        #region UI

        /// <summary>
        /// Propriété <see cref="ScrollViewer.ComputedVerticalScrollBarVisibilityProperty"/> de <see cref="ScrollViewer"/> mise à jour manuellement.
        /// </summary>
        Visibility? m_verticalScrollBarVisibility = null;

        /// <summary>
        /// Obtient la vue par défaut de <see cref="FILEList"/>
        /// </summary>
        public ICollectionView View => CollectionViewSource.GetDefaultView(FILEList.ItemsSource);

        /// <summary>
        /// Représente le ScrollViewer de <see cref="FILEList"/>.
        /// </summary>
        public ScrollViewer ScrollViewer => BenLib.WPF.Misc.FindVisualChild<ScrollViewer>(FILEList);

        #endregion

        #region AddRemoveFiles

        /// <summary>
        /// Indique si l'on peut supprimer des fichiers de la liste.
        /// </summary>
        public bool CanRemove { get; set; } = true;

        /// <summary>
        /// Indique si l'on peut ajouter des fichiers dans la liste.
        /// </summary>
        public bool CanAdd { get; set; } = true;

        #endregion

        #region VGMStream

        /// <summary>
        /// Fenêtre montrant la progression de l'ajout de fichiers.
        /// </summary>
        WaitingWindow WaitingWindow { get; set; }

        /// <summary>
        /// Annule l'ajout ou l'analyse.
        /// </summary>
        CancellationTokenSource AddingCTS { get; set; } = new CancellationTokenSource();

        #region Adding

        /// <summary>
        /// Indique si un ajout est en cours.
        /// </summary>
        bool Adding => !(FilesToAdd.Count == 0 && CurrentAdding == 0);

        /// <summary>
        /// Nombre d'ajouts en cours.
        /// </summary>
        int CurrentAdding { get; set; }

        /// <summary>
        /// Nombre de fichiers à ajouter.
        /// </summary>
        int AddingCount { get; set; }

        /// <summary>
        /// Nombre de fichiers ajoutés.
        /// </summary>
        int AddingProgress => AddingCount - FilesToAdd.Count;

        /// <summary>
        /// Fichiers à ajouter.
        /// </summary>
        Queue<KeyValuePair<string, FichierOutData>> FilesToAdd { get; set; } = new Queue<KeyValuePair<string, FichierOutData>>();

        /// <summary>
        /// Se produit une fois l'ajout terminé.
        /// </summary>
        public event EventHandler AddingCompleted;

        #endregion

        #region Analyze

        /// <summary>
        /// Indique si une analyse est en cours.
        /// </summary>
        bool Analyzing => !(FilesToAnalyze.Count == 0 && CurrentAnalyzing == 0);

        /// <summary>
        /// Nombre d'analyses en cours.
        /// </summary>
        int CurrentAnalyzing { get; set; }

        /// <summary>
        /// Nombre de fichiers à analyser.
        /// </summary>
        int AnalyzingCount { get; set; }

        /// <summary>
        /// Nombre de fichiers analysés.
        /// </summary>
        int AnalyzeProgress => AnalyzingCount - FilesToAnalyze.Count;

        /// <summary>
        /// Fichiers à analyser.
        /// </summary>
        Queue<KeyValuePair<Fichier, IEnumerable<string>>> FilesToAnalyze { get; set; } = new Queue<KeyValuePair<Fichier, IEnumerable<string>>>();

        #endregion

        #endregion

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="FileList"/>.
        /// </summary>
        public FileList()
        {
            InitializeComponent();

            App.LanguageChanged += App_LanguageChanged;

            FILEList.ItemsSource = filesCollection;
            View.Filter = new Predicate<object>((o) =>
            {
                if (o is Fichier fichier)
                {
                    string property = String.Empty;
                    switch (SearchColumn)
                    {
                        case FileListColumn.Name:
                            property = fichier.Name;
                            break;
                        case FileListColumn.State:
                            property = fichier.State;
                            break;
                        case FileListColumn.Duration:
                            property = fichier.DurationString;
                            break;
                        case FileListColumn.Format:
                            property = fichier.Format;
                            break;
                        case FileListColumn.Encoding:
                            property = fichier.Encoding;
                            break;
                        case FileListColumn.SampleRate:
                            property = fichier.SampleRateString;
                            break;
                        case FileListColumn.Channels:
                            property = fichier.Channels.ToString();
                            break;
                        case FileListColumn.Loop:
                            property = fichier.LoopFlagString;
                            break;
                        case FileListColumn.Layout:
                            property = fichier.Layout;
                            break;
                        case FileListColumn.Interleave:
                            property = fichier.InterleaveString;
                            break;
                        case FileListColumn.Folder:
                            property = fichier.Folder;
                            break;
                        case FileListColumn.Size:
                            property = fichier.SizeString;
                            break;
                        case FileListColumn.Date:
                            property = fichier.DateString;
                            break;
                    }
                    if (property.Contains(SearchFilter, SearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) return true; else return false;
                }
                else return false;
            });

            #region EventHandlers

            filesCollection.CollectionChanged += filesCollection_CollectionChanged;

            App.FileListItemCMItems.FindCollectionItem<MenuItem>("DeleteMI").Click += DeleteMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("CheckMI").Click += CheckMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("UnCheckMI").Click += UnCheckMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("AnalyseMI").Click += AnalyzeMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("OpenFPMI").Click += OpenFPMI;
            App.FileListItemCMItems.FindCollectionItem<MenuItem>("CopyFPMI").Click += CopyFPMI;

            StaticPropertyChanged += Settings_StaticPropertyChanged;

            #endregion
        }

        #endregion

        #region Méthodes

        #region VGMStream

        #region AddFiles

        /// <summary>
        /// Ajoute un fichier dans la liste.
        /// </summary>
        /// <param name="filename">Fichier à ajouter.</param>
        public void AddFile(string filename, FichierOutData outData) => AddFiles(new Dictionary<string, FichierOutData>() { { filename, outData } });

        /// <summary>
        /// Ajoute des fichiers dans la liste.
        /// </summary>
        /// <param name="filenames">Fichiers à ajouter.</param>
        /// <param name="outData">Données supplémentaires pour les fichiers.</param>
        public void AddFiles(IEnumerable<string> filenames, FichierOutData outData = default)
        {
            if (filenames != null) AddFiles(filenames.ToDictionary(s => s, s => outData));
        }

        /// <summary>
        /// Ajoute des fichiers dans la liste.
        /// </summary>
        /// <param name="filesToAdd">Fichiers à ajouter.</param>
        public async Task AddFiles(IDictionary<string, FichierOutData> filesToAdd)
        {
            if (filesToAdd != null)
            {
                if (!PreAnalyse || File.Exists(App.VGMStreamPath) || await App.AskVGMStream()) //VGMStream ?
                {
                    FilesToAdd = new Queue<KeyValuePair<string, FichierOutData>>(filesToAdd.Where(kvp => File.Exists(kvp.Key))); //Création de la Queue

                    if ((AddingCount = FilesToAdd.Count) > 0) //Vide ?
                    {
                        #region WaitingWindow

                        WaitingWindow = new WaitingWindow { Maximum = AddingCount };
                        WaitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Running");
                        WaitingWindow.CancelButton.Click += CancelAdding;
                        WaitingWindow.Closing += CancelAdding;
                        Task.Run(() => Dispatcher.Invoke(() =>
                        {
                            try { WaitingWindow.ShowDialog(); }
                            catch { }
                        }));

                        #endregion

                        if (AddingMultithreading) //Start
                        {
                            if (AddingMaxProcessCount <= 0) //Illimité
                            {
                                while (FilesToAdd.Count > 0) AddNewFile(FilesToAdd.Dequeue());
                            }
                            else //Maximum
                            {
                                for (int i = 0; i < AddingMaxProcessCount && FilesToAdd.Count > 0; i++)
                                {
                                    AddNewFile(FilesToAdd.Dequeue());
                                }
                            }
                        }
                        else AddNewFile(FilesToAdd.Dequeue()); //Singlethreading
                    }
                }
            }
        }

        #region Core

        /// <summary>
        /// Ajoute un fichier dans la liste.
        /// </summary>
        /// <param name="fileName">Nom du fichier à ajouter.</param>
        /// <param name="outData">Données complémentaires pour le fichier</param>
        private async Task AddNewFile(string fileName, FichierOutData outData = default)
        {
            try
            {
                CurrentAdding++;

                if (PreAnalyse) //Analyser et ajouter
                {
                    Fichier f = await VGMStream.GetFile(fileName, outData, AddingCTS.Token).WithCancellation(AddingCTS.Token);

                    if (f != null)
                    {
                        f.Index = filesCollection.Count;
                        filesCollection.Add(f);
                    }
                    else ErrorWindow.BadFiles.Add(fileName);
                }
                else //Ajouter uniquement
                {
                    FileInfo fi = new FileInfo(fileName);
                    filesCollection.Add(new Fichier(fileName, outData)
                    {
                        Index = filesCollection.Count,
                        Stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)
                    });
                }

                if ((!AddingMultithreading || AddingMaxProcessCount > 0) && FilesToAdd.Count > 0 && CurrentAdding < 1000) //Start new
                {
                    AddNewFile(FilesToAdd.Dequeue());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CurrentAdding--;

                WaitingWindow.Value = AddingProgress;

                if (CurrentAdding == 0 && (AddingCTS.IsCancellationRequested || FilesToAdd.Count == 0)) //S'exécute à la toute fin de l'ajout
                {
                    Finish(false);
                }
            }
        }

        /// <summary>
        /// Ajoute un fichier dans la liste.
        /// <paramref name="kvp">Données du fichier à ajouter.</paramref>
        /// </summary>
        private Task AddNewFile(KeyValuePair<string, FichierOutData> kvp) => AddNewFile(kvp.Key, kvp.Value);

        #endregion

        #endregion

        #region AnalyzeFiles

        /// <summary>
        /// Analyse des fichiers.
        /// </summary>
        /// <param name="files">Fichiers à analyser.</param>
        /// <param name="displayWaitingWindow">true si la fenêtre d'attente doit être affichée pendant l'analyse; sinon, false.</param>
        public async Task AnalyzeFiles(IDictionary<Fichier, IEnumerable<string>> files, bool displayWaitingWindow = true)
        {
            if (files != null)
            {
                if (File.Exists(App.VGMStreamPath) || await App.AskVGMStream()) //VGMStream ?
                {
                    FilesToAnalyze = new Queue<KeyValuePair<Fichier, IEnumerable<string>>>(files); //Création de la Queue

                    if ((AnalyzingCount = FilesToAnalyze.Count) > 0) //Vide ?
                    {
                        #region WaitingWindow

                        if (displayWaitingWindow)
                        {
                            WaitingWindow = new WaitingWindow { Maximum = AnalyzingCount };
                            WaitingWindow.SetResourceReference(WaitingWindow.TextProperty, "WW_Running");
                            WaitingWindow.CancelButton.Click += CancelAdding;
                            WaitingWindow.Closing += CancelAdding;
                            Task.Run(() => Dispatcher.Invoke(() =>
                            {
                                try { WaitingWindow.ShowDialog(); }
                                catch { }
                            }));
                        }

                        #endregion

                        if (AddingMultithreading) //Start
                        {
                            if (AddingMaxProcessCount <= 0) //Illimité
                            {
                                while (FilesToAnalyze.Count > 0) AnalyzeFileCore(FilesToAnalyze.Dequeue());
                            }
                            else //Maximum
                            {
                                for (int i = 0; i < AddingMaxProcessCount && FilesToAnalyze.Count > 0; i++)
                                {
                                    AnalyzeFileCore(FilesToAnalyze.Dequeue());
                                }
                            }
                        }
                        else AnalyzeFileCore(FilesToAnalyze.Dequeue()); //Singlethreading
                    }
                }
            }
        }

        /// <summary>
        /// Analyse des fichiers.
        /// </summary>
        /// <param name="files">Fichiers à analyser.</param>
        /// <param name="displayWaitingWindow">true si la fenêtre d'attente doit être affichée pendant l'analyse; sinon, false.</param>
        public void AnalyzeFiles(IEnumerable<Fichier> files, bool displayWaitingWindow = true) => AnalyzeFiles(files.ToDictionary(f => f, f => default(IEnumerable<string>)), displayWaitingWindow);

        /// <summary>
        /// Analyse un fichier.
        /// </summary>
        /// <param name="file">Fichier à analyser.</param>
        /// <param name="data">Données vgmstream du fichier.</param>
        /// <param name="displayWaitingWindow">true si la fenêtre d'attente doit être affichée pendant l'analyse; sinon, false.</param>
        public void AnalyzeFile(Fichier file, IEnumerable<string> data, bool displayWaitingWindow = false) => AnalyzeFiles(new Dictionary<Fichier, IEnumerable<string>>() { { file, data } }, displayWaitingWindow);

        /// <summary>
        /// Analyse un fichier.
        /// </summary>
        /// <param name="file">Fichier à analyser.</param>
        /// <param name="displayWaitingWindow">true si la fenêtre d'attente doit être affichée pendant l'analyse; sinon, false.</param>
        public void AnalyzeFile(Fichier file, bool displayWaitingWindow = false) => AnalyzeFiles(new[] { file }, displayWaitingWindow);

        #region Core

        /// <summary>
        /// Analyse un fichier.
        /// </summary>
        /// <param name="file">Fichier à analyser.</param>
        /// <param name="outData">Données complémentaires pour le fichier</param>
        private async Task AnalyzeFileCore(Fichier file, IEnumerable<string> data = null)
        {
            try
            {
                CurrentAnalyzing++;

                Fichier f = data == null ? await VGMStream.GetFile(file.Path, cancellationToken: AddingCTS.Token).WithCancellation(AddingCTS.Token) : VGMStream.GetFile(data, needMetadataFor: false);
                Fichier fc = filesCollection[filesCollection.IndexOf(file)];

                if (f != null)
                {
                    if (!f.Invalid)
                    {
                        bool sel = f.Selected;

                        fc.Analyzed = true;
                        fc.Channels = f.Channels;
                        fc.Encoding = f.Encoding;
                        fc.OriginalFormat = f.OriginalFormat;
                        fc.LoopFlag = f.LoopFlag;
                        fc.LoopStart = f.LoopStart;
                        fc.LoopEnd = f.LoopEnd;
                        fc.SampleRate = f.SampleRate;
                        fc.TotalSamples = f.TotalSamples;
                        fc.NotifyPropertyChanged(String.Empty);

                        //if (sel) FILEList.SelectedItems.Add(f);
                    }
                    else if (f.OriginalState != "FSTATE_Queued") fc.SetInvalid(f.OriginalState);
                }
                else fc.SetInvalid();

                if ((!AddingMultithreading || AddingMaxProcessCount > 0) && FilesToAnalyze.Count > 0 && CurrentAnalyzing < 1000) //Start new
                {
                    AnalyzeFileCore(FilesToAnalyze.Dequeue());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CurrentAnalyzing--;

                WaitingWindow.Value = AnalyzeProgress;

                if (CurrentAnalyzing == 0 && (AddingCTS.IsCancellationRequested || FilesToAnalyze.Count == 0)) //S'exécute à la toute fin de l'analyse
                {
                    Finish(true);
                }
            }
        }

        /// <summary>
        /// Analyse un fichier.
        /// </summary>
        /// <param name="file">Fichier à analyser.</param>
        /// <param name="outData">Données complémentaires pour le fichier</param>
        private Task AnalyzeFileCore(KeyValuePair<Fichier, IEnumerable<string>> kvp) => AnalyzeFileCore(kvp.Key, kvp.Value);

        #endregion

        #endregion

        /// <summary>
        /// Se produit une fois l'ajout terminé.
        /// </summary>
        private void Finish(bool isAnalyse)
        {
            if (ErrorWindow.BadFiles.Count > 0) //Show errors
            {
                ErrorWindow.ShowErrors();
                ErrorWindow.BadFiles = new System.Collections.ObjectModel.ObservableCollection<string>();
            }

            AddingCount = AnalyzingCount = 0;
            AddingCTS = new CancellationTokenSource();

            FilesToAdd = new Queue<KeyValuePair<string, FichierOutData>>();
            FilesToAnalyze = new Queue<KeyValuePair<Fichier, IEnumerable<string>>>();

            try //Close WaitingWindow
            {
                WaitingWindow.CancelButton.Click -= CancelAdding;
                WaitingWindow.Closing -= CancelAdding;
                WaitingWindow.Close();
            }
            catch { }

            GC.Collect();

            if (!isAnalyse) AddingCompleted?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region List

        #region EditList

        #region RemoveItems

        /// <summary>
        /// Supprime tous les fichiers de la liste si <see cref="CanRemove"/> est true.
        /// </summary>
        public void RemoveAll()
        {
            foreach (Fichier fichier in filesCollection.ToList()) filesCollection.Remove(fichier);
        }

        /// <summary>
        /// Supprime les fichiers sélectionnés si <see cref="CanRemove"/> est true.
        /// </summary>
        public void RemoveSelectedItems()
        {
            if (CanRemove)
            {
                try
                {
                    var SI = GetSelectedFiles();
                    foreach (Fichier fichier in SI) filesCollection.Remove(fichier);
                }
                finally { UpdateIndexes(); }
            }
        }

        /// <summary>
        /// Supprime les fichiers erronés si <see cref="CanRemove"/> est true.
        /// </summary>
        public void RemoveInvalidItems()
        {
            if (CanRemove)
            {
                try
                {
                    var wrongfiles = filesCollection.Where(f => f.Invalid).ToList();
                    foreach (Fichier fichier in wrongfiles) filesCollection.Remove(fichier);
                }
                finally { UpdateIndexes(); }
            }
        }

        #endregion

        /// <summary>
        /// Désélectionne, puis resélectionne tous les fichiers sélectionnés.
        /// </summary>
        public void Reselect()
        {
            var selectedfiles = GetSelectedFiles();

            FILEList.SelectedItems.Clear();

            foreach (Fichier fichier in selectedfiles) FILEList.SelectedItems.Add(fichier);
        }

        /// <summary>
        /// Met à jour la propriété <see cref="Fichier.Index"/>.
        /// </summary>
        private void UpdateIndexes()
        {
            foreach (Fichier fichier in filesCollection) fichier.Index = filesCollection.IndexOf(fichier);
        }

        /// <summary>
        /// Déplace les fichiers sélectionnés à un endroit spécifié de la liste
        /// </summary>
        /// <param name="direction">Position désirée.</param>
        public void MoveListViewItems(MoveDirection direction)
        {
            try
            {
                int dir = 0;

                bool valid = FILEList.SelectedItems.Count > 0 &&
                    ((direction == MoveDirection.Down || direction == MoveDirection.Last || direction == MoveDirection.CustomDown) && !(filesCollection[filesCollection.Count - 1].Selected)
                    || (direction == MoveDirection.Up || direction == MoveDirection.First || direction == MoveDirection.CustomUp) && !filesCollection[0].Selected
                    || direction == MoveDirection.FirstMash || direction == MoveDirection.LastMash || direction == MoveDirection.CustomUpMash || direction == MoveDirection.CustomDownMash || direction == MoveDirection.MashUp || direction == MoveDirection.MashDown);

                if (valid)
                {
                    SortedDictionary<int, Fichier> SIDict = direction == MoveDirection.Up || direction == MoveDirection.First || direction == MoveDirection.CustomUp || direction == MoveDirection.MashUp || direction == MoveDirection.FirstMash || direction == MoveDirection.CustomUpMash ? new SortedDictionary<int, Fichier>() : new SortedDictionary<int, Fichier>(new DescendingComparer<int>());

                    foreach (Fichier fichier in FILEList.SelectedItems) SIDict.Add(fichier.Index, fichier);

                    switch (direction)
                    {
                        case MoveDirection.Up:
                            {
                                dir = -1;
                            }
                            break;
                        case MoveDirection.Down:
                            {
                                dir = 1;
                            }
                            break;
                        case MoveDirection.Last:
                            {
                                dir = filesCollection.Count - filesCollection.IndexOf(SIDict.Values.First()) - 1;
                            }
                            break;
                        case MoveDirection.First:
                            {
                                dir = 0 - filesCollection.IndexOf(SIDict.Values.First());
                            }
                            break;
                        case MoveDirection.CustomUp:
                            {
                                InputBoxResult ibr = InputBox.Show(App.Str("INPBOX_RiseOf"), String.Empty, ContentTypes.UnsignedIntegrer, new SolidColorBrush(Color.FromRgb(250, 250, 250)));
                                if (ibr.Result == InputBoxDialogResult.OK)
                                {
                                    try { dir = int.Parse(ibr.Text); }
                                    catch (OverflowException) { dir = int.MaxValue; }
                                    if (dir > SIDict.First().Key) dir = -1 - filesCollection.IndexOf(SIDict.Values.First());
                                    else dir *= -1;
                                }
                                else dir = 0;
                            }
                            break;
                        case MoveDirection.CustomDown:
                            {
                                InputBoxResult ibr = InputBox.Show(App.Str("INPBOX_DescendOf"), String.Empty, ContentTypes.UnsignedIntegrer, new SolidColorBrush(Color.FromRgb(250, 250, 250)));
                                if (ibr.Result == InputBoxDialogResult.OK)
                                {
                                    try { dir = int.Parse(ibr.Text); }
                                    catch (OverflowException) { dir = int.MaxValue; }
                                    if (dir > filesCollection.Count - SIDict.First().Key - 1) dir = filesCollection.Count - filesCollection.IndexOf(SIDict.Values.First());
                                }
                                else dir = 0;
                            }
                            break;
                        case MoveDirection.MashUp:
                        case MoveDirection.CustomUpMash:
                        case MoveDirection.FirstMash:
                            {
                                int i = 0;

                                if (direction == MoveDirection.CustomUpMash)
                                {
                                    InputBoxResult ibr = InputBox.Show(App.Str("INPBOX_MashRiseOf"), String.Empty, ContentTypes.UnsignedIntegrer, new SolidColorBrush(Color.FromRgb(250, 250, 250)));
                                    if (ibr.Result == InputBoxDialogResult.OK)
                                    {
                                        try { i = int.Parse(ibr.Text); }
                                        catch (OverflowException) { i = int.MaxValue; }
                                        if (i > SIDict.First().Key) i = 0;
                                        else i = SIDict.First().Key - i;
                                    }
                                }
                                else if (direction == MoveDirection.MashUp) i = SIDict.First().Key;

                                foreach (int key in SIDict.Keys)
                                {
                                    Fichier f = SIDict[key];
                                    filesCollection.Move(filesCollection.IndexOf(f), i);
                                    f.Selected = true;
                                    i++;
                                }
                                return;
                            }
                        case MoveDirection.MashDown:
                        case MoveDirection.CustomDownMash:
                        case MoveDirection.LastMash:
                            {
                                int i = filesCollection.Count - 1;

                                if (direction == MoveDirection.CustomDownMash)
                                {
                                    InputBoxResult ibr = InputBox.Show(App.Str("INPBOX_MashDescendOf"), String.Empty, ContentTypes.UnsignedIntegrer, new SolidColorBrush(Color.FromRgb(250, 250, 250)));
                                    if (ibr.Result == InputBoxDialogResult.OK)
                                    {
                                        try { i = int.Parse(ibr.Text); }
                                        catch (OverflowException) { i = int.MaxValue; }
                                        if (i > filesCollection.Count - SIDict.First().Key - 1) i = filesCollection.Count - 1;
                                        else i = SIDict.First().Key + i;
                                    }
                                }
                                else if (direction == MoveDirection.MashDown) i = SIDict.First().Key;

                                foreach (int key in SIDict.Keys)
                                {
                                    Fichier f = SIDict[key];
                                    filesCollection.Move(filesCollection.IndexOf(f), i);
                                    f.Selected = true;
                                    i--;
                                }
                                return;
                            }
                    }

                    if (dir != 0)
                    {
                        foreach (int key in SIDict.Keys)
                        {
                            int index = key + dir;
                            Fichier f = SIDict[key];
                            filesCollection.Move(filesCollection.IndexOf(f), index);
                            f.Selected = true;
                        }
                    }

                    if (direction == MoveDirection.Up || direction == MoveDirection.First || direction == MoveDirection.CustomUp || direction == MoveDirection.MashUp || direction == MoveDirection.FirstMash || direction == MoveDirection.CustomUpMash)
                    {
                        FILEList.ScrollIntoView(SIDict.First().Value);
                    }
                    else
                    {
                        FILEList.ScrollIntoView(SIDict.Last().Value);
                    }
                }
            }
            catch { return; }
            finally { UpdateIndexes(); }
        }

        #endregion

        /// <summary>
        /// Ouvre une nouvelle <see cref="CommonOpenFileDialog"/> si <see cref="CanAdd"/> est true.
        /// </summary>
        public void OpenFileDialog(bool folderSelect)
        {
            if (CanAdd)
            {
                var fileDialog = new CommonOpenFileDialog() { Multiselect = true };
                if (!App.AutoCulture)
                {
                    MessageBoxManager.Unregister();
                    MessageBoxManager.Yes = App.Str("TT_Yes");
                    MessageBoxManager.No = App.Str("TT_No");
                    MessageBoxManager.OK = folderSelect ? App.Str("TT_SelectFolders") : App.Str("TT_Open");
                    MessageBoxManager.Cancel = App.Str("TT_Cancel");
                    MessageBoxManager.Retry = App.Str("TT_Retry");
                    MessageBoxManager.Abort = App.Str("TT_Abort");
                    MessageBoxManager.Ignore = App.Str("TT_Ignore");
                    MessageBoxManager.Register();

                    fileDialog.Title = App.Str("TT_Open");
                }
                if (folderSelect)
                {
                    fileDialog.IsFolderPicker = true;
                    fileDialog.Controls.Add(new CommonFileDialogCheckBox("ISFChbx", App.Str("TT_IncludeSubFolders")));
                }
                if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    if (fileDialog.IsFolderPicker)
                    {
                        bool subFolders = (fileDialog.Controls.FirstOrDefault(control => control.Name == "ISFChbx") as CommonFileDialogCheckBox).IsChecked;
                        var files = new List<string>();
                        var folders = fileDialog.FileNames.Where(folder => Directory.Exists(folder)).ToList();
                        foreach (string folder in folders)
                        {
                            try
                            {
                                if (subFolders) files.AddRange(IO.DirSearch(folder, (ex) => ErrorWindow.BadFiles.Add(ex.Message)));
                                else files.AddRange(Directory.GetFiles(folder));
                            }
                            catch { ErrorWindow.BadFiles.Add(folder); }
                        }
                        if (ErrorWindow.BadFiles.Count > 0) //Show errors
                        {
                            ErrorWindow.ShowErrors("TITLE_ErrorWindow2");
                            ErrorWindow.BadFiles = new System.Collections.ObjectModel.ObservableCollection<string>();
                        }
                        AddFiles(files);
                    }
                    else AddFiles(fileDialog.FileNames);
                }
                if (!App.AutoCulture)
                {
                    MessageBoxManager.Unregister();
                    MessageBoxManager.Yes = App.Str("TT_Yes");
                    MessageBoxManager.No = App.Str("TT_No");
                    MessageBoxManager.OK = App.Str("TT_OK");
                    MessageBoxManager.Cancel = App.Str("TT_Cancel");
                    MessageBoxManager.Retry = App.Str("TT_Retry");
                    MessageBoxManager.Abort = App.Str("TT_Abort");
                    MessageBoxManager.Ignore = App.Str("TT_Ignore");
                    MessageBoxManager.Register();
                }
            }
        }

        /// <summary>
        /// Obtient une liste à partir des fichiers sélectionnés.
        /// </summary>
        /// <returns>Liste contenant les fichiers sélectionnés.</returns>
        public List<Fichier> GetSelectedFiles() => (from Fichier fichier in FILEList.SelectedItems select fichier).ToList();

        /// <summary>
        /// Applique le filtre de chaque fichier de <see cref="filesCollection"/> et rafraîchit <see cref="View"/>.
        /// </summary>
        private async Task Search()
        {
            await Task.Delay(SearchDelay);
            View.Refresh();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public static string FileListColumnToFLCOL(FileListColumn column)
        {
            switch (column)
            {
                case FileListColumn.Name:
                    return "FL_COL_Name";
                case FileListColumn.State:
                    return "FL_COL_State";
                case FileListColumn.Duration:
                    return "FL_COL_Duration";
                case FileListColumn.Format:
                    return "FL_COL_Format";
                case FileListColumn.Encoding:
                    return "FL_COL_Encoding";
                case FileListColumn.SampleRate:
                    return "FL_COL_SampleRate";
                case FileListColumn.Channels:
                    return "FL_COL_Channels";
                case FileListColumn.Loop:
                    return "FL_COL_Loop";
                case FileListColumn.Layout:
                    return "FL_COL_Layout";
                case FileListColumn.Interleave:
                    return "FL_COL_Interleave";
                case FileListColumn.Folder:
                    return "FL_COL_Folder";
                case FileListColumn.Size:
                    return "FL_COL_Size";
                case FileListColumn.Date:
                    return "FL_COL_Date";
                default: return null;
            }
        }

        public static FileListColumn? FLCOLToFileListColumn(string res)
        {
            switch (res)
            {
                case "FL_COL_Name":
                    return FileListColumn.Name;
                case "FL_COL_Folder":
                    return FileListColumn.Folder;
                case "FL_COL_Date":
                    return FileListColumn.Date;
                case "FL_COL_Size":
                    return FileListColumn.Size;
                case "FL_COL_Duration":
                    return FileListColumn.Duration;
                case "FL_COL_SampleRate":
                    return FileListColumn.SampleRate;
                case "FL_COL_Format":
                    return FileListColumn.Format;
                case "FL_COL_Encoding":
                    return FileListColumn.Encoding;
                case "FL_COL_Channels":
                    return FileListColumn.Channels;
                case "FL_COL_Loop":
                    return FileListColumn.Loop;
                case "FL_COL_Interleave":
                    return FileListColumn.Interleave;
                case "FL_COL_Layout":
                    return FileListColumn.Layout;
                case "FL_COL_State":
                    return FileListColumn.State;
                default: return null;
            }
        }

        #endregion

        #endregion

        #region Events

        private void App_LanguageChanged(object sender, PropertyChangedExtendedEventArgs<string> e) => Dispatcher.Thread.CurrentUICulture = App.CurrentCulture;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DependencyPropertyDescriptor.FromProperty(ScrollViewer.ComputedVerticalScrollBarVisibilityProperty, typeof(ScrollViewer)).AddValueChanged(ScrollViewer, ScrollViewer_ComputedVerticalScrollBarVisibilityChanged);
            SearchFilter = SearchBox.Visibility == Visibility.Visible ? RestoreSearchFilter : String.Empty;
        }

        private void filesCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (Fichier fichier in e.OldItems) fichier.Stream?.Close();
            }
        }

        private void ScrollViewer_ComputedVerticalScrollBarVisibilityChanged(object sender, EventArgs e)
        {
            switch (ScrollViewer.ComputedVerticalScrollBarVisibility)
            {
                case Visibility.Visible:
                    if (SearchBox.Margin.Right == 0) SearchBox.Margin = new Thickness(SearchBox.Margin.Left - 18, SearchBox.Margin.Top, SearchBox.Margin.Right + 18, SearchBox.Margin.Bottom);
                    break;
                case Visibility.Collapsed:
                    if (SearchBox.Margin.Right == 18) SearchBox.Margin = new Thickness(SearchBox.Margin.Left + 18, SearchBox.Margin.Top, SearchBox.Margin.Right - 18, SearchBox.Margin.Bottom);
                    break;
            }
            m_verticalScrollBarVisibility = ScrollViewer.ComputedVerticalScrollBarVisibility;
        }

        private void SearchBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SearchFilter = (bool)e.NewValue ? RestoreSearchFilter : String.Empty;
            Search();
        }

        private void Settings_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SearchCaseSensitive":
                case "SearchColumn":
                    Search();
                    break;
                case "RestoreSearchFilter":
                    SearchFilter = RestoreSearchFilter;
                    Search();
                    break;
            }
        }

        #region CheckBoxes

        private void SelectOne_Checked(object sender, RoutedEventArgs e)
        {
            if (!m_selectall && filesCollection.Where(f => f.Checked).Count() == filesCollection.Count)
            {
                m_selectone = true;
                SelectAll.IsChecked = true;
                m_selectone = false;
            }
        }

        private void SelectOne_UnChecked(object sender, RoutedEventArgs e)
        {
            if (!m_selectall)
            {
                m_selectone = true;
                SelectAll.IsChecked = false;
                m_selectone = false;
            }
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (!m_selectone)
            {
                m_selectall = true;
                foreach (Fichier fichier in filesCollection) fichier.Checked = true;
                m_selectall = false;
            }
        }

        private void SelectAll_UnChecked(object sender, RoutedEventArgs e)
        {
            if (!m_selectone)
            {
                m_selectall = true;
                foreach (Fichier fichier in filesCollection) fichier.Checked = false;
                m_selectall = false;
            }
        }

        #endregion        

        #region FILEList

        private void FILEListHeader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is GridViewColumnHeader headerClicked && headerClicked.Content != null)
                {
                    if (headerClicked.Content is string s)
                    {
                        switch (App.Res(s, indice: "FL_COL_"))
                        {
                            case "FL_COL_Name":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.Name))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.Name);
                                    }
                                    else filesCollection.OrderByVoid(f => f.Name);
                                }
                                break;
                            case "FL_COL_Folder":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.Folder))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.Folder);
                                    }
                                    else filesCollection.OrderByVoid(f => f.Folder);
                                }
                                break;
                            case "FL_COL_Date":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.Date))
                                    {
                                        filesCollection.OrderByVoid(f => f.Date);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.Date);
                                }
                                break;
                            case "FL_COL_Size":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.Size))
                                    {
                                        filesCollection.OrderByVoid(f => f.Size);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.Size);
                                }
                                break;
                            case "FL_COL_Duration":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.Duration))
                                    {
                                        filesCollection.OrderByVoid(f => f.Duration);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.Duration);
                                }
                                break;
                            case "FL_COL_SampleRate":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.SampleRate))
                                    {
                                        filesCollection.OrderByVoid(f => f.SampleRate);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.SampleRate);
                                }
                                break;
                            case "FL_COL_Format":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.Format))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.Format);
                                    }
                                    else filesCollection.OrderByVoid(f => f.Format);
                                }
                                break;
                            case "FL_COL_Encoding":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.Encoding))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.Encoding);
                                    }
                                    else filesCollection.OrderByVoid(f => f.Encoding);
                                }
                                break;
                            case "FL_COL_Channels":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.Channels))
                                    {
                                        filesCollection.OrderByVoid(f => f.Channels);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.Channels);
                                }
                                break;
                            case "FL_COL_Loop":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.LoopFlag))
                                    {
                                        filesCollection.OrderByVoid(f => f.LoopFlag);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.LoopFlag);
                                }
                                break;
                            case "FL_COL_Interleave":
                                {
                                    if (filesCollection.IsOrderedByDescending(f => f.Interleave))
                                    {
                                        filesCollection.OrderByVoid(f => f.Interleave);
                                    }
                                    else filesCollection.OrderByDescendingVoid(f => f.Interleave);
                                }
                                break;
                            case "FL_COL_Layout":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.Layout))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.Layout);
                                    }
                                    else filesCollection.OrderByVoid(f => f.Layout);
                                }
                                break;
                            case "FL_COL_State":
                                {
                                    if (filesCollection.IsOrderedBy(f => f.State))
                                    {
                                        filesCollection.OrderByDescendingVoid(f => f.State);
                                    }
                                    else filesCollection.OrderByVoid(f => f.State);
                                }
                                break;
                        }
                    }
                    else if (headerClicked.Content is CheckBox chbx)
                    {
                        if (filesCollection.IsOrderedBy(f => f.Checked))
                        {
                            filesCollection.OrderByDescendingVoid(f => f.Checked);
                        }
                        else filesCollection.OrderByVoid(f => f.Checked);
                    }
                }
            }
            catch { return; }
            finally { UpdateIndexes(); }
        }

        private void FILEList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { //Le Binding fonctionne mal à cause de la virtualisation.
            foreach (Fichier fichier in e.AddedItems) fichier.Selected = true;
            foreach (Fichier fichier in e.RemovedItems) fichier.Selected = false;
        }

        #endregion

        #region DragNDrop

        private void FILEList_DragEnterOverLeave(object sender, DragEventArgs e)
        {
            if (Adding || Analyzing)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void FILEList_Drop(object sender, DragEventArgs e)
        {
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop, false)).ToList();
            foreach (string folder in files.Where(folder => Directory.Exists(folder)).ToList())
            {
                files.Remove(folder);
                if (Keyboard.Modifiers == ModifierKeys.Shift) files.AddRange(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
                else files.AddRange(Directory.GetFiles(folder));
            }
            AddFiles(files);
        }

        #endregion

        private void CancelAdding(object sender, EventArgs e) => AddingCTS.Cancel();

        #endregion
    }

    public class FilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var column = parameter.ToString().ToEnum<FileListColumn>();
            return SearchColumn == column ? SearchFilter : String.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    public enum MoveDirection { Up, Down, First, Last, CustomUp, CustomDown, MashUp, MashDown, FirstMash, LastMash, CustomUpMash, CustomDownMash }

    public enum FileListColumn { Check, Name, State, Duration, Format, Encoding, SampleRate, Channels, Loop, Layout, Interleave, Folder, Size, Date }
}
