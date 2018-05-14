using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using BenLib;
using BenLib.WPF;
using System.Text;
using System.IO;

namespace VGMGUI
{
    public partial class MainWindow
    {
        #region Champs & Propriétés

        /// <summary>
        /// Indique si les fichiers sélectionnés peuvent être modifiés par les contrôles.
        /// </summary>
        public bool CanEditFichier { get; set; }

        private bool m_cancelCBSelection;

        /// <summary>
        /// Dictionnaires contennant le nombre d'instances de toutes les propriétés des fichiers sélectionnés.
        /// </summary>
        public Dictionary<object, int>[] Infos { get; set; } = new Dictionary<object, int>[18];

        #endregion

        #region Méthodes

        /// <summary>
        /// Retourne une image correspondant à un bool.
        /// </summary>
        /// <param name="boolean">La valeur du bool.</param>
        /// <returns>L'image "Checkicon" si le bool est true; sinon, L'image "Wrong".</returns>
        Viewbox BoolToImage(bool boolean)
        {
            if (boolean) return Application.Current.Resources["Checkicon"] as Viewbox;
            else return Application.Current.Resources["Wrong"] as Viewbox;
        }

        /// <summary>
        /// Efface toutes les informations affichées dans "Entrée" et "Sortie".
        /// </summary>
        void ClearDisplayedData()
        {
            FormatTB.Text = String.Empty;
            EncodingTB.Text = String.Empty;
            SampleRateTB.Text = String.Empty;
            TotalsamplesTB.Text = String.Empty;
            if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) LoopHeaderSP.Children.RemoveAt(0);
            LoopStartTB.Text = String.Empty;
            LoopEndTB.Text = String.Empty;
            LoopCountBox.Empty = true;
            StartEndLoopCheckBox.IsChecked = false;
            FadeOutCheckBox.IsChecked = true;
            FadeDelayBox.Empty = true;
            FadeTimeBox.Empty = true;
            ChannelsTB.Text = String.Empty;
            LayoutTB.Text = String.Empty;
            InterleaveTB.Text = String.Empty;
            BitrateTB.Text = String.Empty;
            DestCB.Text = String.Empty;
        }

        /// <summary>
        /// Affiche une information des fichiers sélectionnés.
        /// </summary>
        /// <param name="info">L'information à afficher.</param>
        void DisplayInfo(MediaInfos info)
        {
            switch (info)
            {
                case MediaInfos.Format:
                    if (Infos[0].Count == 0) FormatTB.Text = String.Empty;
                    else if (Infos[0].Count == 1) FormatTB.Text = Infos[0].Keys.First().ToString();
                    else FormatTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.Encoding:
                    if (Infos[1].Count == 0) EncodingTB.Text = String.Empty;
                    else if (Infos[1].Count == 1) EncodingTB.Text = Infos[1].Keys.First().ToString();
                    else EncodingTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.Channels:
                    if (Infos[2].Count == 0) ChannelsTB.Text = String.Empty;
                    else if (Infos[2].Count == 1) ChannelsTB.Text = Infos[2].Keys.First().ToString();
                    else ChannelsTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.SampleRate:
                    if (Infos[3].Count == 0) SampleRateTB.Text = String.Empty;
                    else if (Infos[3].Count == 1) SampleRateTB.Text = Infos[3].Keys.First().ToString();
                    else SampleRateTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.TotalSamples:
                    if (Infos[4].Count == 0) TotalsamplesTB.Text = String.Empty;
                    else if (Infos[4].Count == 1) TotalsamplesTB.Text = Infos[4].Keys.First().ToString();
                    else TotalsamplesTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.LoopFlag:
                    if (Infos[5].Count == 0) { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) LoopHeaderSP.Children.RemoveAt(0); }
                    else if (Infos[5].Count == 1) { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) { LoopHeaderSP.Children.RemoveAt(0); } LoopHeaderSP.Children.Insert(0, BoolToImage((bool)Infos[5].Keys.First())); }
                    else { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) { LoopHeaderSP.Children.RemoveAt(0); } LoopHeaderSP.Children.Insert(0, Application.Current.Resources["Circle"] as Viewbox); }
                    break;
                case MediaInfos.LoopStartString:
                    if (Infos[6].Count == 0) LoopStartTB.Text = String.Empty;
                    else if (Infos[6].Count == 1) LoopStartTB.Text = Infos[6].Keys.First().ToString();
                    else LoopStartTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.LoopEndString:
                    if (Infos[7].Count == 0) LoopEndTB.Text = String.Empty;
                    else if (Infos[7].Count == 1) LoopEndTB.Text = Infos[7].Keys.First().ToString();
                    else LoopEndTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.LoopCount:
                    if (Infos[8].Count == 0) LoopCountBox.Empty = true;
                    else if (Infos[8].Count == 1) LoopCountBox.Text = Infos[8].Keys.First().ToString();
                    else LoopCountBox.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.StartEndLoop:
                    if (Infos[9].Count == 0) StartEndLoopCheckBox.IsChecked = false;
                    else if (Infos[9].Count == 1) StartEndLoopCheckBox.IsChecked = (bool)Infos[9].Keys.First();
                    else StartEndLoopCheckBox.IsChecked = null;
                    break;
                case MediaInfos.FadeDelay:
                    if (Infos[10].Count == 0) FadeDelayBox.Empty = true;
                    else if (Infos[10].Count == 1) FadeDelayBox.Text = Infos[10].Keys.First().ToString();
                    else FadeDelayBox.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.FadeTime:
                    if (Infos[11].Count == 0) FadeTimeBox.Empty = true;
                    else if (Infos[11].Count == 1) FadeTimeBox.Text = Infos[11].Keys.First().ToString();
                    else FadeTimeBox.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.Destination:
                    if (Infos[12].Count == 0) DestCB.Text = String.Empty;
                    else if (Infos[12].Count == 1)
                    {
                        if (Directory.Exists((string)Infos[12].Keys.First()))
                        {
                            DestCB.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom").Content = Infos[12].Keys.First();
                            DestCB.SelectedItem = DestCB.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom");
                        }
                        else DestCB.Text = (string)Infos[12].Keys.First();
                    }
                    else DestCB.SelectedItem = DestCB.Items.FindCollectionItem<ComboBoxItem>("MultipleCBI");
                    break;
                case MediaInfos.FadeOut:
                    if (Infos[13].Count == 0) FadeOutCheckBox.IsChecked = false;
                    else if (Infos[13].Count == 1) FadeOutCheckBox.IsChecked = (bool)Infos[13].Keys.First();
                    else FadeOutCheckBox.IsChecked = null;
                    break;
                case MediaInfos.Layout:
                    if (Infos[14].Count == 0) LayoutTB.Text = String.Empty;
                    else if (Infos[14].Count == 1) LayoutTB.Text = Infos[14].Keys.First().ToString();
                    else LayoutTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.Interleave:
                    if (Infos[15].Count == 0) InterleaveTB.Text = String.Empty;
                    else if (Infos[15].Count == 1) InterleaveTB.Text = Infos[15].Keys.First().ToString();
                    else InterleaveTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.Bitrate:
                    if (Infos[16].Count == 0) BitrateTB.Text = String.Empty;
                    else if (Infos[16].Count == 1) BitrateTB.Text = Infos[16].Keys.First().ToString();
                    else BitrateTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.SamplesToPlay:
                    if (Infos[17].Count == 0) SamplesToPlayTB.Text = String.Empty;
                    else if (Infos[17].Count == 1) SamplesToPlayTB.Text = Infos[17].Keys.First().ToString();
                    else SamplesToPlayTB.Text = App.Str("MW_Multiple");
                    break;
                case MediaInfos.All:
                    {
                        //Format=====================================================================
                        if (Infos[0].Count == 0) FormatTB.Text = String.Empty;
                        else if (Infos[0].Count == 1) FormatTB.Text = Infos[0].Keys.First().ToString();
                        else FormatTB.Text = App.Str("MW_Multiple");
                        //Encoding=====================================================================
                        if (Infos[1].Count == 0) EncodingTB.Text = String.Empty;
                        else if (Infos[1].Count == 1) EncodingTB.Text = Infos[1].Keys.First().ToString();
                        else EncodingTB.Text = App.Str("MW_Multiple");
                        //Channels=====================================================================
                        if (Infos[2].Count == 0) ChannelsTB.Text = String.Empty;
                        else if (Infos[2].Count == 1) ChannelsTB.Text = Infos[2].Keys.First().ToString();
                        else ChannelsTB.Text = App.Str("MW_Multiple");
                        //SampleRate=====================================================================
                        if (Infos[3].Count == 0) SampleRateTB.Text = String.Empty;
                        else if (Infos[3].Count == 1) SampleRateTB.Text = Infos[3].Keys.First().ToString();
                        else SampleRateTB.Text = App.Str("MW_Multiple");
                        //TotalSamples=====================================================================
                        if (Infos[4].Count == 0) TotalsamplesTB.Text = String.Empty;
                        else if (Infos[4].Count == 1) TotalsamplesTB.Text = Infos[4].Keys.First().ToString();
                        else TotalsamplesTB.Text = App.Str("MW_Multiple");
                        //LoopFlag=====================================================================
                        if (Infos[5].Count == 0) { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) LoopHeaderSP.Children.RemoveAt(0); }
                        else if (Infos[5].Count == 1) { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) { LoopHeaderSP.Children.RemoveAt(0); } LoopHeaderSP.Children.Insert(0, BoolToImage((bool)Infos[5].Keys.First())); }
                        else { if (LoopHeaderSP.Children[0].GetType() == typeof(Viewbox)) { LoopHeaderSP.Children.RemoveAt(0); } LoopHeaderSP.Children.Insert(0, Application.Current.Resources["Circle"] as Viewbox); }
                        //LoopStartString=====================================================================
                        if (Infos[6].Count == 0) LoopStartTB.Text = String.Empty;
                        else if (Infos[6].Count == 1) LoopStartTB.Text = Infos[6].Keys.First().ToString();
                        else LoopStartTB.Text = App.Str("MW_Multiple");
                        //LoopEndString=====================================================================
                        if (Infos[7].Count == 0) LoopEndTB.Text = String.Empty;
                        else if (Infos[7].Count == 1) LoopEndTB.Text = Infos[7].Keys.First().ToString();
                        else LoopEndTB.Text = App.Str("MW_Multiple");
                        //LoopCount=====================================================================
                        if (Infos[8].Count == 0) LoopCountBox.Empty = true;
                        else if (Infos[8].Count == 1) LoopCountBox.Text = Infos[8].Keys.First().ToString();
                        else LoopCountBox.Text = App.Str("MW_Multiple");
                        //StartEndLoop=====================================================================
                        if (Infos[9].Count == 0) StartEndLoopCheckBox.IsChecked = false;
                        else if (Infos[9].Count == 1) StartEndLoopCheckBox.IsChecked = (bool)Infos[9].Keys.First();
                        else StartEndLoopCheckBox.IsChecked = null;
                        //FadeDelay=====================================================================
                        if (Infos[10].Count == 0) FadeDelayBox.Empty = true;
                        else if (Infos[10].Count == 1) FadeDelayBox.Text = Infos[10].Keys.First().ToString();
                        else FadeDelayBox.Text = App.Str("MW_Multiple");
                        //FadeTime=====================================================================
                        if (Infos[11].Count == 0) FadeTimeBox.Empty = true;
                        else if (Infos[11].Count == 1) FadeTimeBox.Text = Infos[11].Keys.First().ToString();
                        else FadeTimeBox.Text = App.Str("MW_Multiple");
                        //Destination=====================================================================
                        if (Infos[12].Count == 0) DestCB.Text = String.Empty;
                        else if (Infos[12].Count == 1)
                        {
                            if (Directory.Exists((string)Infos[12].Keys.First()))
                            {
                                DestCB.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom").Content = Infos[12].Keys.First();
                                DestCB.SelectedItem = DestCB.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom");
                            }
                            else DestCB.Text = (string)Infos[12].Keys.First();
                        }
                        else DestCB.SelectedItem = DestCB.Items.FindCollectionItem<ComboBoxItem>("MultipleCBI");
                        //FadeOut=====================================================================
                        if (Infos[13].Count == 0) FadeOutCheckBox.IsChecked = false;
                        else if (Infos[13].Count == 1) FadeOutCheckBox.IsChecked = (bool)Infos[13].Keys.First();
                        else FadeOutCheckBox.IsChecked = null;
                        //Layout=====================================================================
                        if (Infos[14].Count == 0) LayoutTB.Text = String.Empty;
                        else if (Infos[14].Count == 1) LayoutTB.Text = Infos[14].Keys.First().ToString();
                        else LayoutTB.Text = App.Str("MW_Multiple");
                        //Interleave=====================================================================
                        if (Infos[15].Count == 0) InterleaveTB.Text = String.Empty;
                        else if (Infos[15].Count == 1) InterleaveTB.Text = Infos[15].Keys.First().ToString();
                        else InterleaveTB.Text = App.Str("MW_Multiple");
                        //Bitrate=====================================================================
                        if (Infos[16].Count == 0) BitrateTB.Text = String.Empty;
                        else if (Infos[16].Count == 1) BitrateTB.Text = Infos[16].Keys.First().ToString();
                        else BitrateTB.Text = App.Str("MW_Multiple");
                        //SamplesToPlay=====================================================================
                        if (Infos[17].Count == 0) SamplesToPlayTB.Text = String.Empty;
                        else if (Infos[17].Count == 1) SamplesToPlayTB.Text = Infos[17].Keys.First().ToString();
                        else SamplesToPlayTB.Text = App.Str("MW_Multiple");
                        //=====================================================================
                    }
                    break;
            }

            bool valid = true;

            for (int i = 8; i <= 13; i++)
            {
                if (Infos[i].Count > 1) valid = false;
            }

            if (valid) CopyButton.IsEnabled = SetAsDefaultButton.IsEnabled = true;
            else CopyButton.IsEnabled = SetAsDefaultButton.IsEnabled = false;
        }

        /// <summary>
        /// Inscrit une information dans les fichiers sélectionnés.
        /// </summary>
        /// <param name="info">L'information à inscrire.</param>
        void WriteInfo(MediaInfos info)
        {
            object value;
            bool overflow = false;

            switch (info)
            {
                case MediaInfos.LoopCount:
                    if ((value = LoopCountBox.Text.ToInt()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            int oldValue = fichier.LoopCount;
                            fichier.LoopCount = (int)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.LoopCount = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    break;
                case MediaInfos.StartEndLoop:
                    if ((value = StartEndLoopCheckBox.IsChecked) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            bool oldValue = fichier.StartEndLoop;
                            fichier.StartEndLoop = (bool)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.StartEndLoop = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    break;
                case MediaInfos.FadeOut:
                    if ((value = FadeOutCheckBox.IsChecked) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            bool oldValue = fichier.FadeOut;
                            fichier.FadeOut = (bool)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeOut = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    break;
                case MediaInfos.FadeDelay:
                    if ((value = FadeDelayBox.Text.ToDouble()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            double oldValue = fichier.FadeDelay;
                            fichier.FadeDelay = (double)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeDelay = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    break;
                case MediaInfos.FadeTime:
                    if ((value = FadeTimeBox.Text.ToDouble()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            double oldValue = fichier.FadeTime;
                            fichier.FadeTime = (double)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeTime = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    break;
                case MediaInfos.Destination:
                    value = null;
                    if ((DestCB.SelectedItem as ComboBoxItem)?.Content is string sitxt) value = (sitxt != App.Str("MW_Multiple")) ? App.Res(sitxt, indice: "DEST_") ?? sitxt : null;
                    else if (!DestCB.Text.IsNullOrEmpty()) value = (DestCB.Text != App.Str("MW_Multiple")) ? App.Res(DestCB.Text, indice: "DEST_") ?? DestCB.Text : null;
                    if (value != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems) fichier.OriginalDestination = (string)value;
                    }
                    break;
                case MediaInfos.All:
                    //LoopCount=====================================================================
                    if ((value = LoopCountBox.Text.ToInt()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            int oldValue = fichier.LoopCount;
                            fichier.LoopCount = (int)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.LoopCount = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    //StartEndLoop=====================================================================
                    if ((value = StartEndLoopCheckBox.IsChecked) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            bool oldValue = fichier.StartEndLoop;
                            fichier.StartEndLoop = (bool)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.StartEndLoop = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    //FadeOut=====================================================================
                    if ((value = FadeOutCheckBox.IsChecked) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            bool oldValue = fichier.FadeOut;
                            fichier.FadeOut = (bool)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeOut = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    //FadeDelay=====================================================================
                    if ((value = FadeDelayBox.Text.ToDouble()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            double oldValue = fichier.FadeDelay;
                            fichier.FadeDelay = (double)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeDelay = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    //FadeTime=====================================================================
                    if ((value = FadeTimeBox.Text.ToDouble()) != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                        {
                            double oldValue = fichier.FadeTime;
                            fichier.FadeTime = (double)value;
                            if (fichier.SamplesToPlay == -1)
                            {
                                fichier.FadeTime = oldValue;
                                overflow = true;
                            }
                        }
                    }
                    //Destination=====================================================================
                    value = null;
                    if ((DestCB.SelectedItem as ComboBoxItem)?.Content is string allsitxt) value = (allsitxt != App.Str("MW_Multiple")) ? App.Res(allsitxt, indice: "DEST_") ?? allsitxt : null;
                    else if (!DestCB.Text.IsNullOrEmpty()) value = (DestCB.Text != App.Str("MW_Multiple")) ? App.Res(DestCB.Text, indice: "DEST_") ?? DestCB.Text : null;
                    if (value != null)
                    {
                        foreach (Fichier fichier in tasklist.FILEList.SelectedItems) fichier.OriginalDestination = (string)value;
                    }
                    break;
            }

            if (overflow) MessageBox.Show(App.Str("ERR_Overflow"), App.Str("TT_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Incrémente un valeur dans <see cref="Infos"/>.
        /// </summary>
        /// <param name="index">Index du dictionnaire où est la valeur.</param>
        /// <param name="value">Valeur à incrémenter.</param>
        void RegisterInfo(int index, object value)
        {
            if (Infos[index].ContainsKey(value)) Infos[index][value]++;
            else Infos[index].Add(value, 1);
        }

        /// <summary>
        /// Décrémente un valeur dans <see cref="Infos"/>.
        /// </summary>
        /// <param name="index">Index du dictionnaire où est la valeur.</param>
        /// <param name="value">Valeur à décrémenter.</param>
        bool UnRegisterInfo(int index, object value)
        {
            if (Infos[index].ContainsKey(value))
            {
                Infos[index][value]--;
                if (Infos[index][value] == 0) Infos[index].Remove(value);
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Exécute un <see cref="RegisterInfo"/> sur toutes les <see cref="MediaInfos"/> d'un fichier.
        /// </summary>
        /// <param name="fichier">Le fichier à enregistrer.</param>
        void RegisterAllInfos(Fichier fichier)
        {
            RegisterInfo(0, fichier.Format);
            RegisterInfo(1, fichier.Encoding);
            RegisterInfo(2, fichier.Channels);
            RegisterInfo(3, fichier.SampleRateString);
            RegisterInfo(4, fichier.TotalSamplesString);
            RegisterInfo(5, fichier.LoopFlag);
            RegisterInfo(6, fichier.LoopStartString);
            RegisterInfo(7, fichier.LoopEndString);

            RegisterInfo(8, fichier.LoopCount);
            RegisterInfo(9, fichier.StartEndLoop);
            RegisterInfo(10, fichier.FadeDelay);
            RegisterInfo(11, fichier.FadeTime);
            RegisterInfo(12, fichier.Destination);
            RegisterInfo(13, fichier.FadeOut);
            RegisterInfo(14, fichier.Layout);
            RegisterInfo(15, fichier.InterleaveString);
            RegisterInfo(16, fichier.BitrateString);
            RegisterInfo(17, fichier.SamplesToPlayString);
        }

        /// <summary>
        /// Exécute un <see cref="UnRegisterInfo"/> sur toutes les <see cref="MediaInfos"/> d'un fichier.
        /// </summary>
        /// <param name="fichier">Le fichier à effacer.</param>
        void UnRegisterAllInfos(Fichier fichier)
        {
            UnRegisterInfo(0, fichier.Format);
            UnRegisterInfo(1, fichier.Encoding);
            UnRegisterInfo(2, fichier.Channels);
            UnRegisterInfo(3, fichier.SampleRateString);
            UnRegisterInfo(4, fichier.TotalSamplesString);
            UnRegisterInfo(5, fichier.LoopFlag);
            UnRegisterInfo(6, fichier.LoopStartString);
            UnRegisterInfo(7, fichier.LoopEndString);

            UnRegisterInfo(8, fichier.LoopCount);
            UnRegisterInfo(9, fichier.StartEndLoop);
            UnRegisterInfo(10, fichier.FadeDelay);
            UnRegisterInfo(11, fichier.FadeTime);
            UnRegisterInfo(12, fichier.Destination);
            UnRegisterInfo(13, fichier.FadeOut);
            UnRegisterInfo(14, fichier.Layout);
            UnRegisterInfo(15, fichier.InterleaveString);
            UnRegisterInfo(16, fichier.BitrateString);
            UnRegisterInfo(17, fichier.SamplesToPlayString);
        }

        /// <summary>
        /// Supprime toutes les <see cref="MediaInfos"/> de <see cref="Infos"/> et enregistre celles des fichiers sélectionnés.
        /// </summary>
        void RefreshInfos()
        {
            foreach (Dictionary<object, int> dict in Infos) dict?.Clear();
            if (tasklist.FILEList.SelectedItems.Count > 0)
            {
                foreach (Fichier fichier in tasklist.FILEList.SelectedItems) RegisterAllInfos(fichier);
                DisplayInfo(MediaInfos.All);
            }
        }

        /// <summary>
        /// Inscrit les fichier récents dans <see cref="MainDestCB"/>.
        /// </summary>
        /// <param name="selectedItem">Le contenu du <see cref="ComboBoxItem"/> qui devra être sélectionné.</param>
        void DisplayRecentFiles(string selectedItem = null)
        {
            var items = (from ComboBoxItem item in MainDestCB.Items select item).ToList();

            foreach (ComboBoxItem item in items)
            {
                if (item.Name.Contains("Recent")) MainDestCB.Items.Remove(item);
            }

            var files = (Settings.SettingsData.Global["RecentFiles"]?.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))?.ToList() ?? new List<string>();

            for (int i = files.Count - 1; i >= 0; i--)
            {
                var item = new ComboBoxItem() { Name = "Recent" + i, Content = files[i] };
                MainDestCB.Items.Insert(1, item);
                if (item.Content.Equals(selectedItem)) MainDestCB.SelectedItem = item;
            }
        }

        #endregion

        #region Events

        private void FILEList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatusBar();

            CanEditFichier = false;

            foreach (Fichier fichier in e.AddedItems) RegisterAllInfos(fichier);
            foreach (Fichier fichier in e.RemovedItems) UnRegisterAllInfos(fichier);
            DisplayInfo(MediaInfos.All);

            CanEditFichier = true;
        }

        private void Files_ItemChangedEvent(object sender, PropertyChangedExtendedEventArgs<object> e)
        {
            if (sender is Fichier fichier && fichier.Selected)
            {
                switch (e.PropertyName)
                {
                    case "Format":
                        {
                            UnRegisterInfo(0, e.OldValue);
                            RegisterInfo(0, e.NewValue);
                            DisplayInfo(MediaInfos.Format);
                            break;
                        }
                    case "Encoding":
                        {
                            UnRegisterInfo(1, e.OldValue);
                            RegisterInfo(1, e.NewValue);
                            DisplayInfo(MediaInfos.Encoding);
                            break;
                        }
                    case "Channels":
                        {
                            UnRegisterInfo(2, e.OldValue);
                            RegisterInfo(2, e.NewValue);
                            DisplayInfo(MediaInfos.Channels);
                            break;
                        }
                    case "SampleRateString":
                        {
                            UnRegisterInfo(3, e.OldValue);
                            RegisterInfo(3, e.NewValue);
                            DisplayInfo(MediaInfos.SampleRate);
                            break;
                        }
                    case "TotalSamplesString":
                        {
                            UnRegisterInfo(4, e.OldValue);
                            RegisterInfo(4, e.NewValue);
                            DisplayInfo(MediaInfos.TotalSamples);
                            break;
                        }
                    case "LoopFlagString":
                        {
                            UnRegisterInfo(5, e.OldValue);
                            RegisterInfo(5, e.NewValue);
                            DisplayInfo(MediaInfos.LoopFlag);
                            break;
                        }
                    case "LoopStartString":
                        {
                            UnRegisterInfo(6, e.OldValue);
                            RegisterInfo(6, e.NewValue);
                            DisplayInfo(MediaInfos.LoopStartString);
                            break;
                        }
                    case "LoopEndString":
                        {
                            UnRegisterInfo(7, e.OldValue);
                            RegisterInfo(7, e.NewValue);
                            DisplayInfo(MediaInfos.LoopEndString);
                            break;
                        }
                    case "LoopCount":
                        {
                            UnRegisterInfo(8, e.OldValue);
                            RegisterInfo(8, e.NewValue);
                            DisplayInfo(MediaInfos.LoopCount);
                            break;
                        }
                    case "StartEndLoop":
                        {
                            UnRegisterInfo(9, e.OldValue);
                            RegisterInfo(9, e.NewValue);
                            DisplayInfo(MediaInfos.StartEndLoop);
                            break;
                        }
                    case "FadeDelay":
                        {
                            UnRegisterInfo(10, e.OldValue);
                            RegisterInfo(10, e.NewValue);
                            DisplayInfo(MediaInfos.FadeDelay);
                            break;
                        }
                    case "FadeTime":
                        {
                            UnRegisterInfo(11, e.OldValue);
                            RegisterInfo(11, e.NewValue);
                            DisplayInfo(MediaInfos.FadeTime);
                            break;
                        }
                    case "Destination":
                        {
                            UnRegisterInfo(12, e.OldValue);
                            RegisterInfo(12, e.NewValue);
                            DisplayInfo(MediaInfos.Destination);
                            break;
                        }
                    case "FadeOut":
                        {
                            UnRegisterInfo(13, e.OldValue);
                            RegisterInfo(13, e.NewValue);
                            DisplayInfo(MediaInfos.FadeOut);
                            break;
                        }
                    case "Layout":
                        {
                            UnRegisterInfo(14, e.OldValue);
                            RegisterInfo(14, e.NewValue);
                            DisplayInfo(MediaInfos.Layout);
                            break;
                        }
                    case "InterleaveString":
                        {
                            UnRegisterInfo(15, e.OldValue);
                            RegisterInfo(15, e.NewValue);
                            DisplayInfo(MediaInfos.Interleave);
                            break;
                        }
                    case "BitrateString":
                        {
                            UnRegisterInfo(16, e.OldValue);
                            RegisterInfo(16, e.NewValue);
                            DisplayInfo(MediaInfos.Bitrate);
                            break;
                        }
                    case "SamplesToPlayString":
                        {
                            UnRegisterInfo(17, e.OldValue);
                            RegisterInfo(17, e.NewValue);
                            DisplayInfo(MediaInfos.SamplesToPlay);
                            break;
                        }
                }
            }
        }

        private void SwitchableTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (CanEditFichier && sender is SwitchableTextBox stbsender)
                switch (stbsender.Name)
                {
                    case "LoopCountBox":
                        WriteInfo(MediaInfos.LoopCount);
                        break;
                    case "FadeDelayBox":
                        WriteInfo(MediaInfos.FadeDelay);
                        break;
                    case "FadeTimeBox":
                        WriteInfo(MediaInfos.FadeTime);
                        break;
                }
        }

        private void CheckBox_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            if (CanEditFichier && sender is CheckBox chbx)
                switch (chbx.Name)
                {
                    case "StartEndLoopCheckBox":
                        WriteInfo(MediaInfos.StartEndLoop);
                        break;
                    case "FadeOutCheckBox":
                        WriteInfo(MediaInfos.FadeOut);
                        break;
                }
        }

        private async void ComboBoxItem_Selected(object sender, RoutedEventArgs e)
        {
            using (CommonOpenFileDialog ofd = new CommonOpenFileDialog() { IsFolderPicker = true, Title = App.Str("TT_SelectFolder") })
            {
                if (!App.AutoCulture)
                {
                    MessageBoxManager.Unregister();
                    MessageBoxManager.Yes = App.Str("TT_Yes");
                    MessageBoxManager.No = App.Str("TT_No");
                    MessageBoxManager.OK = App.Str("TT_SelectFolder");
                    MessageBoxManager.Cancel = App.Str("TT_Cancel");
                    MessageBoxManager.Retry = App.Str("TT_Retry");
                    MessageBoxManager.Abort = App.Str("TT_Abort");
                    MessageBoxManager.Ignore = App.Str("TT_Ignore");
                    MessageBoxManager.Register();
                }

                if (ofd.ShowDialog() == CommonFileDialogResult.Ok && sender is ComboBoxItem item)
                {
                    ComboBoxItem customItem;

                    switch (item.Name)
                    {
                        case "PrincipalBrowse":
                            {
                                var files = (Settings.SettingsData.Global["RecentFiles"]?.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))?.ToList() ?? new List<string>();
                                if (!files.Contains(ofd.FileName))
                                {
                                    while (files.Count >= 5) files.RemoveAt(0);
                                    files.Add(ofd.FileName);

                                    StringBuilder sb = new StringBuilder();

                                    for (int i = 0; i < files.Count; i++)
                                    {
                                        sb.Append(files[i]);
                                        if (i < files.Count - 1) sb.Append(" | ");
                                    }

                                    Settings.SettingsData.Global["RecentFiles"] = sb.ToString();
                                    if ((await Settings.TryWriteSettings()).Result) await Dispatcher.BeginInvoke(new Action(() => DisplayRecentFiles(ofd.FileName)));
                                }
                                else await Dispatcher.BeginInvoke(new Action(() => MainDestCB.SelectedItem = (from ComboBoxItem cbxitem in MainDestCB.Items select cbxitem).FirstOrDefault(cbitem => cbitem.Content.Equals(ofd.FileName))));
                            }
                            break;
                        case "SelectedTasksBrowse":
                            (customItem = DestCB.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom")).Content = ofd.FileName;
                            break;
                    }
                }
                else m_cancelCBSelection = true;

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

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cbx)
                switch (cbx.Name)
                {
                    case "MainDestCB":
                        if (m_cancelCBSelection)
                        {
                            m_cancelCBSelection = false;
                            cbx.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
                        }
                        break;
                    case "DestCB":
                        if (m_cancelCBSelection)
                        {
                            m_cancelCBSelection = false;
                            cbx.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
                        }
                        else if (App.Res((cbx.SelectedItem as ComboBoxItem)?.Content?.ToString(), indice: "DEST") == "DEST_Browse")
                        {
                            cbx.SelectedItem = cbx.Items.FindCollectionItem<ComboBoxItem>("SelectedTasksCustom");
                        }
                        if (CanEditFichier) WriteInfo(MediaInfos.Destination);
                        break;
                }
        }

        #endregion
    }

    public enum MediaInfos { Format, Encoding, Channels, Layout, Interleave, Bitrate, SampleRate, TotalSamples, LoopFlag, LoopStartString, LoopEndString, LoopCount, StartEndLoop, FadeOut, FadeDelay, FadeTime, Destination, SamplesToPlay, All }
}
