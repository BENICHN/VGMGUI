using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BenLib;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public static ObservableCollection<string> BadFiles { get; set; } = new ObservableCollection<string>();

        public ErrorWindow()
        {
            InitializeComponent();
            ErrorList.MaxWidth = SystemParameters.PrimaryScreenWidth / 2;
            ErrorList.MaxHeight = SystemParameters.PrimaryScreenHeight / 2;
            DataContext = this;
            App.ErrorItemCMItems.FindCollectionItem<MenuItem>("E_OpenFPMI").Click += OpenFPMI;
            App.ErrorItemCMItems.FindCollectionItem<MenuItem>("E_CopyFPMI").Click += CopyFPMI;
            App.ErrorItemCMItems.FindCollectionItem<MenuItem>("E_PropertiesMI").Click += PropertiesMI;
        }

        public static void ShowErrors(string resTitle = "TITLE_ErrorWindow")
        {
            ErrorWindow box = new ErrorWindow() { Title = App.Str(resTitle) };
            box.ShowDialog();
        }
    }
}
