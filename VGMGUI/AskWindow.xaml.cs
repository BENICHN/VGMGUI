using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BenLib;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour AskWindow.xaml
    /// </summary>
    public partial class AskWindow : Window
    {
        /// <summary>
        /// Indique si <see cref="SelectAll_Checked"/> ou <see cref="SelectAll_UnChecked"/> a été appelé par <see cref="SelectOne_Checked"/> ou <see cref="SelectOne_UnChecked"/>
        /// </summary>
        bool m_selectone;

        /// <summary>
        /// Indique si <see cref="SelectOne_UnChecked"/> ou <see cref="SelectOne_Checked"/> a été appelé par <see cref="SelectAll_Checked"/> ou <see cref="SelectAll_UnChecked"/>
        /// </summary>
        bool m_selectall;

        public ObservableCollection<AskingFile> ListSource { get; set; }

        ObservableCollection<AskingFile> m_result = null;

        public AskWindow(ObservableCollection<AskingFile> list)
        {
            InitializeComponent();
            AskList.MaxWidth = SystemParameters.PrimaryScreenWidth / 2;
            AskList.MaxHeight = SystemParameters.PrimaryScreenHeight / 2;
            DataContext = this;
            ListSource = list;

            App.AskItemCMItems.FindCollectionItem<MenuItem>("A_NumberMI").Click += NumberMI;
            App.AskItemCMItems.FindCollectionItem<MenuItem>("A_OverwriteMI").Click += OverwriteMI;
            App.AskItemCMItems.FindCollectionItem<MenuItem>("A_IgnoreMI").Click += IgnoreMI;
            App.AskItemCMItems.FindCollectionItem<MenuItem>("A_OpenFPMI").Click += OpenFPMI;
            App.AskItemCMItems.FindCollectionItem<MenuItem>("A_CopyFPMI").Click += CopyFPMI;
        }

        public new ObservableCollection<AskingFile> ShowDialog()
        {
            base.ShowDialog();
            return m_result;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chbx)
            {
                switch (chbx.Name)
                {
                    case "chbx_h_erase":
                        if (!m_selectone)
                        {
                            m_selectall = true;
                            foreach (AskingFile file in ListSource) file.Overwrite = true;
                            m_selectall = false;
                        }
                        break;
                    case "chbx_h_num":
                        if (!m_selectone)
                        {
                            m_selectall = true;
                            foreach (AskingFile file in ListSource) file.Number = true;
                            m_selectall = false;
                        }
                        break;

                    case "chbx_erase":
                        ((ItemsControl.ContainerFromElement(AskList, chbx) as ListViewItem).Content as AskingFile).Number = false;
                        chbx_h_num.IsChecked = false;
                        if (!m_selectall && ListSource.Where(af => af.Overwrite).Count() == ListSource.Count)
                        {
                            m_selectone = true;
                            chbx_h_erase.IsChecked = true;
                            m_selectone = false;
                        }
                        break;
                    case "chbx_num":
                        ((ItemsControl.ContainerFromElement(AskList, chbx) as ListViewItem).Content as AskingFile).Overwrite = false;
                        chbx_h_erase.IsChecked = false;
                        if (!m_selectall && ListSource.Where(af => af.Number).Count() == ListSource.Count)
                        {
                            m_selectone = true;
                            chbx_h_num.IsChecked = true;
                            m_selectone = false;
                        }
                        break;
                }
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chbx)
            {
                switch (chbx.Name)
                {
                    case "chbx_h_erase":
                        if (!m_selectone)
                        {
                            m_selectall = true;
                            foreach (AskingFile file in ListSource) file.Overwrite = false;
                            m_selectall = false;
                        }
                        break;
                    case "chbx_h_num":
                        if (!m_selectone)
                        {
                            m_selectall = true;
                            foreach (AskingFile file in ListSource) file.Number = false;
                            m_selectall = false;
                        }
                        break;

                    case "chbx_erase":
                        if (!m_selectall)
                        {
                            m_selectone = true;
                            chbx_h_erase.IsChecked = false;
                            m_selectone = false;
                        }
                        break;
                    case "chbx_num":
                        if (!m_selectall)
                        {
                            m_selectone = true;
                            chbx_h_num.IsChecked = false;
                            m_selectone = false;
                        }
                        break;
                }
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = ListSource;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void AskListHeader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is GridViewColumnHeader headerClicked && headerClicked.Content != null)
                {
                    string content = null;
                    if (headerClicked.Content is string s) content = s;
                    else if (headerClicked.Content is StackPanel sp) content = (sp.Children[1] as TextBlock).Text;

                    switch (App.Res(content, indice: "ASKW_COL_"))
                    {
                        case "ASKW_COL_1":
                            {
                                if (ListSource.IsOrderedBy(af => af.Name))
                                {
                                    ListSource.OrderByDescendingVoid(af => af.Name);
                                }
                                else ListSource.OrderByVoid(af => af.Name);
                            }
                            break;
                        case "ASKW_COL_2":
                            {
                                if (ListSource.IsOrderedBy(af => af.Overwrite))
                                {
                                    ListSource.OrderByDescendingVoid(af => af.Overwrite);
                                }
                                else ListSource.OrderByVoid(af => af.Overwrite);
                            }
                            break;
                        case "ASKW_COL_3":
                            {
                                if (ListSource.IsOrderedBy(af => af.Number))
                                {
                                    ListSource.OrderByDescendingVoid(af => af.Number);
                                }
                                else ListSource.OrderByVoid(af => af.Number);
                            }
                            break;
                    }
                }
            }
            catch { return; }
        }
    }

    public class AskingFile : INotifyPropertyChanged
    {
        public AskingFile(Fichier file = null) => File = file;

        public Fichier File { get; set; }

        public string Name => File.FinalDestination;

        public FileActions Action => Overwrite ? FileActions.Overwrite : Number ? FileActions.Number : FileActions.Ignore;

        private bool m_overwrite;
        public bool Overwrite
        {
            get => m_overwrite;
            set
            {
                m_overwrite = value;
                NotifyPropertyChanged("Overwrite");
            }
        }

        private bool m_number;
        public bool Number
        {
            get => m_number;
            set
            {
                m_number = value;
                NotifyPropertyChanged("Number");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum FileActions { Overwrite, Number, Ignore }
}
