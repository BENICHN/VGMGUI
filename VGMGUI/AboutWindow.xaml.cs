using System;
using System.Diagnostics;
using System.Windows;
using BenLib;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public static string Copyright => $"© {App.Assembly.GetLinkerTime().Year} BenNat";
        public static string VLCVersion => $"VLC {App.VLCVersion}";

        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => Process.Start(e.Uri.AbsoluteUri);

        public static new bool? Show()
        {
            AboutWindow aboutWindow = new AboutWindow();
            return aboutWindow.ShowDialog();
        }
    }
}
