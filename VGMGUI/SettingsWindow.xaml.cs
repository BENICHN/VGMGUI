using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using IniParser.Model;
using BenLib;
using BenLib.WPF;
using static VGMGUI.Settings;
using System.Linq;
using System.Collections.Generic;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        #region Champs & Propriétés

        /// <summary>
        /// Paramètres utilisés dans cette instance.
        /// </summary>
        IniData m_data;

        /// <summary>
        /// Résultat de cette instance.
        /// </summary>
        bool m_result;

        bool m_valid = true;

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref='SettingsWindow'/>.
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            m_data = (IniData)SettingsData.Clone();
            ApplySettings();
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Applique des paramètres à cette instance.
        /// </summary>
        /// <param name="m_data">Les paramètres à appliquer.</param>
        void ApplySettings()
        {
            object o;

            if ((o = m_data.Global["Language"]) != null)
            {
                switch (o)
                {
                    case "Auto":
                        cmbx_lang.SelectedIndex = 0;
                        break;
                    case "en-US":
                        cmbx_lang.SelectedIndex = 1;
                        break;
                    case "fr-FR":
                        cmbx_lang.SelectedIndex = 2;
                        break;
                }
            }

            if ((o = m_data.Global["VLCC"]) != null)
            {
                switch (o)
                {
                    case "Memory":
                        cmbx_vlcc.SelectedIndex = 0;
                        break;
                    case "File":
                        cmbx_vlcc.SelectedIndex = 1;
                        break;
                    case "Never":
                        cmbx_vlcc.SelectedIndex = 2;
                        break;
                }
            }

            if ((o = m_data.Global["StopWhenDelete"].ToBool()) != null) chbx_stopwhendelete.IsChecked = (bool)o;
            else chbx_stopwhendelete.IsChecked = true;

            if ((o = m_data.Global["PreAnalyse"].ToBool()) != null) chbx_preanalyse.IsChecked = (bool)o;
            else chbx_preanalyse.IsChecked = true;

            if ((o = m_data.Global["Preview"]) != null)
            {
                switch (o)
                {
                    case "In":
                        rbtn_preview_in.IsChecked = true;
                        break;
                    case "Out":
                        rbtn_preview_out.IsChecked = true;
                        break;
                }
            }
            else rbtn_preview_in.IsChecked = true;

            if ((o = m_data.Global["SamplesDisplay"]) != null)
            {
                switch (o)
                {
                    case "S":
                        rbtn_samplesdisplay_s.IsChecked = true;
                        break;
                    case "HMS":
                        rbtn_samplesdisplay_hms.IsChecked = true;
                        break;
                }
            }
            else rbtn_samplesdisplay_s.IsChecked = true;

            if ((o = m_data.Global["SamplesDisplayMaxDec"].ToInt()) != null) stbx_maxdec.Text = ((int)o).ToString();
            else stbx_maxdec.Text = "4";

            if ((o = m_data["Search"]["SearchDelay"].ToInt()) != null) stbx_searchdelay.Text = ((int)o).ToString();
            else stbx_searchdelay.Text = "250";

            if ((o = m_data["Multithreading"]["Conversion"].ToBool()) != null) chbx_multith_conversion.IsChecked = (bool)o;
            else chbx_multith_conversion.IsChecked = true;

            if ((o = m_data["Multithreading"]["MaxConversion"].ToInt()) != null) stbx_max_conversion.Text = ((int)o).ToString();
            else stbx_max_conversion.Text = "5";

            if ((o = m_data["Multithreading"]["Adding"].ToBool()) != null) chbx_multith_adding.IsChecked = (bool)o;
            else chbx_multith_adding.IsChecked = true;

            if ((o = m_data["Multithreading"]["MaxAdding"].ToInt()) != null) stbx_max_adding.Text = ((int)o).ToString();
            else stbx_max_adding.Text = "5";

            if ((o = m_data["AdditionalFormats"]["DKCTFCSMP"].ToBool()) != null) chbx_additionalformats_dkctfcsmp.IsChecked = (bool)o;
            else chbx_additionalformats_dkctfcsmp.IsChecked = true;

            if ((o = m_data["StatusBar"]["Display"].ToBool()) != null) chbx_stsbar.IsChecked = (bool)o;
            else chbx_stsbar.IsChecked = true;

            if ((o = m_data["StatusBar"]["Counter"].ToBool()) != null) chbx_stsbar_count.IsChecked = (bool)o;
            else chbx_stsbar_count.IsChecked = true;

            if ((o = m_data["StatusBar"]["RAM"].ToBool()) != null) chbx_stsbar_RAM.IsChecked = (bool)o;
            else chbx_stsbar_RAM.IsChecked = true;

            if ((o = m_data["StatusBar"]["StreamingType"].ToBool()) != null) chbx_stsbar_streamingType.IsChecked = (bool)o;
            else chbx_stsbar_streamingType.IsChecked = true;

            if ((o = m_data["StatusBar"]["SamplesDisplay"].ToBool()) != null) chbx_stsbar_samplesDisplay.IsChecked = (bool)o;
            else chbx_stsbar_samplesDisplay.IsChecked = true;

            if ((o = m_data["StatusBar"]["SearchDelay"].ToBool()) != null) chbx_stsbar_searchDelay.IsChecked = (bool)o;
            else chbx_stsbar_searchDelay.IsChecked = true;

            if ((o = m_data["StatusBar"]["PreAnalyse"].ToBool()) != null) chbx_stsbar_preAnalyse.IsChecked = (bool)o;
            else chbx_stsbar_preAnalyse.IsChecked = true;

            if ((o = m_data["Colors"]["Foreground"].ToColor()) != null) rect_foregroundcolor.Fill = new SolidColorBrush((Color)o);
            if ((o = m_data["Colors"]["Background"].ToColor()) != null) rect_backgroundcolor.Fill = new SolidColorBrush((Color)o);
            if ((o = m_data["Colors"]["Error"].ToColor()) != null) rect_errorcolor.Fill = new SolidColorBrush((Color)o);
        }

        /// <summary>
        /// Réinitialise les paramètres.
        /// </summary>
        private void Reset()
        {
            m_data.Global["Language"] = "Auto";
            m_data.Global["VLCC"] = "Memory";
            m_data.Global["StopWhenDelete"] = "True";
            m_data.Global["PreAnalyse"] = "True";
            m_data.Global["Preview"] = "In";
            m_data.Global["SamplesDisplay"] = "S";
            m_data.Global["SamplesDisplayMaxDec"] = "4";
            m_data["Search"]["SearchDelay"] = "250";

            m_data["Multithreading"]["Conversion"] = "True";
            m_data["Multithreading"]["MaxConversion"] = "5";
            m_data["Multithreading"]["Adding"] = "True";
            m_data["Multithreading"]["MaxAdding"] = "5";

            m_data["AdditionalFormats"]["DKCTFCSMP"] = "True";

            m_data["StatusBar"]["Display"] = "True";
            m_data["StatusBar"]["Counter"] = "True";
            m_data["StatusBar"]["RAM"] = "True";
            m_data["StatusBar"]["SamplesDisplay"] = "True";
            m_data["StatusBar"]["SearchDelay"] = "True";
            m_data["StatusBar"]["PreAnalyse"] = "True";
            m_data["StatusBar"]["StreamingType"] = "True";

            m_data["Colors"]["Foreground"] = "#16A085";
            m_data["Colors"]["Background"] = "#727272";
            m_data["Colors"]["Error"] = "#C0392B";

            ApplySettings();
        }

        /// <summary>
        /// Affiche la fenêtre et retourne un résultat.
        /// </summary>
        /// <returns>true si <see cref="btn_ok"/> a été cliqué; sinon false</returns>
        public new bool ShowDialog()
        {
            base.ShowDialog();
            return m_result;
        }

        /// <summary>
        /// Action du bouton <see cref="btn_ok"/> ou de <see cref="btn_apply"/>.
        /// </summary>
        private void OK(bool apply)
        {
            var result = Parser.TryWriteFile(System.IO.Path.Combine(App.AppPath, "VGMGUI.ini"), m_data);
            if (result.Result)
            {
                SettingsData = m_data;
                if (apply)
                {
                    if (Application.Current.MainWindow is MainWindow mainwin) mainwin.ApplySettings(Settings.SettingsData);
                }
                else
                {
                    m_result = true;
                    Close();
                }
            }
            else MessageBox.Show(result.Exception.Message, App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Action du bouton <see cref="btn_cancel"/>.
        /// </summary>
        private void Cancel()
        {
            m_result = false;
            Close();
        }

        #endregion

        #region Events

        private void chbx_checked(object sender, RoutedEventArgs e)
        {
            if (m_data == null) return;

            switch ((sender as CheckBox).Name)
            {
                case "chbx_preanalyse":
                    m_data.Global["PreAnalyse"] = "True";
                    break;
                case "chbx_multith_conversion":
                    m_data["Multithreading"]["Conversion"] = "True";
                    break;
                case "chbx_multith_adding":
                    m_data["Multithreading"]["Adding"] = "True";
                    break;
                case "chbx_stopwhendelete":
                    m_data.Global["StopWhenDelete"] = "True";
                    break;
                case "chbx_additionalformats_dkctfcsmp":
                    m_data["AdditionalFormats"]["DKCTFCSMP"] = "True";
                    break;
                case "chbx_stsbar":
                    m_data["StatusBar"]["Display"] = "True";
                    break;
                case "chbx_stsbar_count":
                    m_data["StatusBar"]["Counter"] = "True";
                    break;
                case "chbx_stsbar_RAM":
                    m_data["StatusBar"]["RAM"] = "True";
                    break;
                case "chbx_stsbar_samplesDisplay":
                    m_data["StatusBar"]["SamplesDisplay"] = "True";
                    break;
                case "chbx_stsbar_searchDelay":
                    m_data["StatusBar"]["SearchDelay"] = "True";
                    break;
                case "chbx_stsbar_preAnalyse":
                    m_data["StatusBar"]["PreAnalyse"] = "True";
                    break;
                case "chbx_stsbar_streamingType":
                    m_data["StatusBar"]["StreamingType"] = "True";
                    break;
            }
        }

        private void chbx_unchecked(object sender, RoutedEventArgs e)
        {
            if (m_data == null) return;

            switch ((sender as CheckBox).Name)
            {
                case "chbx_preanalyse":
                    m_data.Global["PreAnalyse"] = "False";
                    break;
                case "chbx_multith_conversion":
                    m_data["Multithreading"]["Conversion"] = "False";
                    break;
                case "chbx_multith_adding":
                    m_data["Multithreading"]["Adding"] = "False";
                    break;
                case "chbx_stopwhendelete":
                    m_data.Global["StopWhenDelete"] = "False";
                    break;
                case "chbx_additionalformats_dkctfcsmp":
                    m_data["AdditionalFormats"]["DKCTFCSMP"] = "False";
                    break;
                case "chbx_stsbar":
                    m_data["StatusBar"]["Display"] = "False";
                    break;
                case "chbx_stsbar_count":
                    m_data["StatusBar"]["Counter"] = "False";
                    break;
                case "chbx_stsbar_RAM":
                    m_data["StatusBar"]["RAM"] = "False";
                    break;
                case "chbx_stsbar_samplesDisplay":
                    m_data["StatusBar"]["SamplesDisplay"] = "False";
                    break;
                case "chbx_stsbar_searchDelay":
                    m_data["StatusBar"]["SearchDelay"] = "False";
                    break;
                case "chbx_stsbar_preAnalyse":
                    m_data["StatusBar"]["PreAnalyse"] = "False";
                    break;
                case "chbx_stsbar_streamingType":
                    m_data["StatusBar"]["StreamingType"] = "False";
                    break;
            }
        }

        private void stbx_GotFocus(object sender, RoutedEventArgs e) => m_valid = false;

        private void stbx_LostFocus(object sender, RoutedEventArgs e)
        {
            if (m_data != null)
            {
                SwitchableTextBox ssender = sender as SwitchableTextBox;

                switch (ssender.Name)
                {
                    case "stbx_max_conversion":
                        m_data["Multithreading"]["MaxConversion"] = ssender.Text;
                        break;
                    case "stbx_max_adding":
                        m_data["Multithreading"]["MaxAdding"] = ssender.Text;
                        break;
                    case "stbx_searchdelay":
                        m_data["Search"]["SearchDelay"] = ssender.Text;
                        break;
                    case "stbx_maxdec":
                        m_data.Global["SamplesDisplayMaxDec"] = ssender.Text;
                        break;
                }
            }

            m_valid = true;
        }

        private void rect_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (m_data == null) return;

            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                switch (((sender as ContentControl)?.Content as Rectangle)?.Name)
                {
                    case "rect_foregroundcolor":
                        colorDialog.Color = (rect_foregroundcolor.Fill as SolidColorBrush).Color.ToDrawingColor();
                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            m_data["Colors"]["Foreground"] = colorDialog.Color.ToHex();
                            rect_foregroundcolor.Fill = new SolidColorBrush(colorDialog.Color.ToMediaColor());
                        }
                        break;
                    case "rect_backgroundcolor":
                        colorDialog.Color = (rect_backgroundcolor.Fill as SolidColorBrush).Color.ToDrawingColor();
                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            m_data["Colors"]["Background"] = colorDialog.Color.ToHex();
                            rect_backgroundcolor.Fill = new SolidColorBrush(colorDialog.Color.ToMediaColor());
                        }
                        break;
                    case "rect_errorcolor":
                        colorDialog.Color = (rect_errorcolor.Fill as SolidColorBrush).Color.ToDrawingColor();
                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            m_data["Colors"]["Error"] = colorDialog.Color.ToHex();
                            rect_errorcolor.Fill = new SolidColorBrush(colorDialog.Color.ToMediaColor());
                        }
                        break;
                }
            }

        }

        private void rbtn_Checked(object sender, RoutedEventArgs e)
        {
            if (m_data == null) return;

            switch ((sender as RadioButton).Name)
            {
                case "rbtn_preview_in":
                    m_data.Global["Preview"] = "In";
                    break;
                case "rbtn_preview_out":
                    m_data.Global["Preview"] = "Out";
                    break;

                case "rbtn_samplesdisplay_s":
                    m_data.Global["SamplesDisplay"] = "S";
                    break;
                case "rbtn_samplesdisplay_hms":
                    m_data.Global["SamplesDisplay"] = "HMS";
                    break;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_data == null) return;

            if (sender is ComboBox cbx && m_data != null)
            {
                switch (cbx.Name)
                {
                    case "cmbx_lang":
                        switch ((cbx.SelectedItem as ComboBoxItem).Content)
                        {
                            case "Auto":
                                m_data.Global["Language"] = "Auto";
                                break;
                            case "Français":
                                m_data.Global["Language"] = "fr-FR";
                                break;
                            case "English":
                                m_data.Global["Language"] = "en-US";
                                break;
                        }
                        break;
                    case "cmbx_vlcc":
                        switch (App.Res((cbx.SelectedItem as ComboBoxItem).Content.ToString()))
                        {
                            case "STGS_VLCC_Memory":
                                m_data.Global["VLCC"] = "Memory";
                                break;
                            case "STGS_VLCC_File":
                                m_data.Global["VLCC"] = "File";
                                break;
                            case "STGS_VLCC_Never":
                                m_data.Global["VLCC"] = "Never";
                                break;
                        }
                        break;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                switch (btn.Name)
                {
                    case "btn_about":
                        AboutWindow.Show();
                        break;
                    case "btn_apply":
                        OK(true);
                        break;
                    case "btn_cancel":
                        Cancel();
                        break;
                    case "btn_reset":
                        Reset();
                        break;
                    case "btn_ok":
                        OK(false);
                        break;
                }
            }
        }

        #endregion
    }

    public partial class MainWindow
    {
        /// <summary>
        /// Applique des paramètres à l'instance actuelle de VGMGUI.
        /// </summary>
        /// <param name="data">Les paramètres à appliquer.</param>
        public void ApplySettings(IniData data, bool startup = false)
        {
            object o;

            if ((o = data.Global["Language"]) != null) App.SetLanguage(o as string);
            if ((o = data.Global["StopWhenDelete"].ToBool()) != null) StopPlayingWhenDeleteFile = (bool)o;
            if ((o = data.Global["PreAnalyse"].ToBool()) != null) PreAnalyse = (bool)o;
            if (startup && (o = data.Global["Preview"]) != null)
            {
                switch (o)
                {
                    case "In":
                        ALERadioButton.IsChecked = true;
                        break;
                    case "Out":
                        ALSRadioButton.IsChecked = true;
                        break;
                }
            }
            if ((o = data.Global["SamplesDisplay"]) != null)
            {
                switch (o)
                {
                    case "S":
                        HMSSamplesDisplay = false;
                        break;
                    case "HMS":
                        HMSSamplesDisplay = true;
                        break;
                }
            }
            if ((o = data.Global["SamplesDisplayMaxDec"].ToInt()) != null) SamplesDisplayMaxDec = ((int)o);

            if ((o = data["Search"]["SearchDelay"].ToInt()) != null) SearchDelay = ((int)o);

            if (startup)
            {
                o = data.Global["LoopType"];
                {
                    switch (o)
                    {
                        case "None":
                            AP.LoopType = LoopTypes.None;
                            break;
                        case "All":
                            AP.LoopType = LoopTypes.All;
                            break;
                        case "Random":
                            AP.LoopType = LoopTypes.Random;
                            break;
                    }
                }

                if ((o = data.Global["Volume"].ToInt()) != null) AP.Volume = (int)o;
                if ((o = data.Global["Mute"].ToBool()) != null) AP.Mute = (bool)o;

                if ((o = data.Global["ConversionFolderName"]) != null) MainDestTB.Text = o as string;
                if ((o = (SettingsData.Global["DefaultOutData"]?.Replace("\"", String.Empty).Split(new[] { " | " }, StringSplitOptions.None))?.ToList()) != null)
                {
                    var outData = (List<string>)o;
                    var s = SettingsData.Global["DefaultOutData"];
                    if (outData.Count == 6)
                    {
                        DefaultOutData = new FichierOutData()
                        {
                            OriginalDestination = outData[0],
                            FadeDelay = outData[1].ToDouble(),
                            FadeOut = outData[2].ToBool(),
                            FadeTime = outData[3].ToDouble(),
                            LoopCount = outData[4].ToInt(),
                            StartEndLoop = outData[5].ToBool(),
                        };
                    }
                }

                if ((o = (SettingsData.Global["ColumnsInfo"]?.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))) != null)
                {
                    var columns = (tasklist.FILEList.View as GridView).Columns;
                    var oldColumns = (tasklist.FILEList.View as GridView).Columns.ToList();
                    var columnsInfo = (from string pair in (string[])o let num = pair.Split(new[] { " : " }, StringSplitOptions.None) where num.Count() == 2 let i = num[0].ToInt() where i != null let w = num[1].ToDouble() where w != null select new KeyValuePair<int, double>((int)i, (double)w)).ToList();
                    if (columnsInfo.Count == oldColumns.Count && !columnsInfo.OrderBy(i => i.Key).Select((i, j) => i.Key - j).Distinct().Skip(1).Any())
                    {
                        for (int i = 0; i < oldColumns.Count; i++)
                        {
                            int oldIndex = columns.IndexOf(oldColumns[i]);
                            int newIndex = columnsInfo[i].Key;

                            columns.Move(oldIndex, newIndex);
                            columns[newIndex].Width = columnsInfo[i].Value;
                        }
                    }
                }

                if ((o = data["Window"]["Width"].ToDouble()) != null) Width = (double)o;
                if ((o = data["Window"]["Height"].ToDouble()) != null) Height = (double)o;
                if ((o = data["Window"]["State"].ToEnum<WindowState>()) != null && (WindowState)o != WindowState.Minimized) WindowState = (WindowState)o;

                if ((o = (SettingsData["Grids"]["TopGrid"]?.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))) is string[] columndefsstr && columndefsstr.Length == 2)
                {
                    var columndefs = new GridLength?[] { columndefsstr[0].ToGridLength(), columndefsstr[1].ToGridLength() };
                    if (!columndefs.Contains(null))
                    {
                        TopGrid.ColumnDefinitions[0].Width = (GridLength)columndefs[0];
                        TopGrid.ColumnDefinitions[1].Width = (GridLength)columndefs[1];
                    }
                }
                if ((o = (SettingsData["Grids"]["RightGrid"]?.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))) is string[] rowdefsstr && rowdefsstr.Length == 2)
                {
                    var rowdefs = new GridLength?[] { rowdefsstr[0].ToGridLength(), rowdefsstr[1].ToGridLength() };
                    if (!rowdefs.Contains(null))
                    {
                        RightGrid.RowDefinitions[0].Height = (GridLength)rowdefs[0];
                        RightGrid.RowDefinitions[1].Height = (GridLength)rowdefs[1];
                    }
                }

                if ((o = data["Search"]["SearchFilter"]) != null) RestoreSearchFilter = o as string;
                if ((o = data["Search"]["SearchColumn"].ToEnum<FileListColumn>()) != null) SearchColumn = (FileListColumn)o;
                if ((o = data["Search"]["CaseSensitive"].ToBool()) != null) SearchCaseSensitive = ((bool)o);
            }

            if ((o = data["Multithreading"]["Conversion"].ToBool()) != null) ConversionMultithreading = (bool)o;
            if ((o = data["Multithreading"]["MaxConversion"].ToInt()) != null) ConversionMaxProcessCount = (int)o;

            if ((o = data["Multithreading"]["Adding"].ToBool()) != null) AddingMultithreading = (bool)o;
            if ((o = data["Multithreading"]["MaxAdding"].ToInt()) != null) AddingMaxProcessCount = (int)o;

            if ((o = data["StatusBar"]["Display"].ToBool()) != null) StatusBar.Display = (bool)o;
            if ((o = data["StatusBar"]["Counter"].ToBool()) != null) StatusBar.Counter = (bool)o;
            if ((o = data["StatusBar"]["RAM"].ToBool()) != null) StatusBar.RAM = (bool)o;
            if ((o = data["StatusBar"]["SamplesDisplay"].ToBool()) != null) StatusBar.SamplesDisplay = (bool)o;
            if ((o = data["StatusBar"]["SearchDelay"].ToBool()) != null) StatusBar.SearchDelay = (bool)o;
            if ((o = data["StatusBar"]["PreAnalyse"].ToBool()) != null) StatusBar.PreAnalyse = (bool)o;
            if ((o = data["StatusBar"]["StreamingType"].ToBool()) != null) StatusBar.StreamingType = (bool)o;

            if ((o = data.Global["StreamingType"].ToEnum<StreamingType>()) != null) Settings.StreamingType = (StreamingType)o;

            if ((o = data["AdditionalFormats"]["DKCTFCSMP"].ToBool()) != null) AdditionalFormats.DKCTFCSMP = (bool)o;

            if ((o = data["Colors"]["Foreground"].ToColor()) != null)
            {
                Color ocolor = (Color)o;

                Application.Current.Resources["ForegroundBrush"] = new SolidColorBrush(ocolor);
                Application.Current.Resources["ForegroundBrush_Disabled"] = new SolidColorBrush(Color.FromArgb(80, ocolor.R, ocolor.G, ocolor.B));
            }
            if ((o = data["Colors"]["Background"].ToColor()) != null)
            {
                Color ocolor = (Color)o;

                Application.Current.Resources["BackgroundBrush"] = new SolidColorBrush(ocolor);
                Application.Current.Resources["BackgroundBrush_Disabled"] = new SolidColorBrush(Color.FromArgb(80, ocolor.R, ocolor.G, ocolor.B));
            }
            if ((o = data["Colors"]["Error"].ToColor()) != null)
            {
                Color ocolor = (Color)o;

                Application.Current.Resources["ErrorBrush"] = new SolidColorBrush(ocolor);
                Application.Current.Resources["ErrorBrush_Disabled"] = new SolidColorBrush(Color.FromArgb(80, ocolor.R, ocolor.G, ocolor.B));

                foreach (Fichier fichier in tasklist.Files) fichier.RefreshValidity();
            }

            RefreshInfos();
            UpdateStatusBar(false, true);
        }
    }
}
