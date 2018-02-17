using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour WaitingWindow.xaml
    /// </summary>
    public partial class WaitingWindow : Window
    {
        public double Value
        {
            get => Bar.Value;
            set
            {
                //tii_ww.ProgressState = value <= 0 ? System.Windows.Shell.TaskbarItemProgressState.None : System.Windows.Shell.TaskbarItemProgressState.Normal;
                Bar.Value = value;
                tii_ww.ProgressValue = value / Maximum;
            }
        }
        public double Maximum { get => Bar.Maximum; set => Bar.Maximum = value; }
        public bool IsIndeterminate
        {
            get => Bar.IsIndeterminate;
            set
            {
                Bar.IsIndeterminate = value;
                tii_ww.ProgressState = value ? System.Windows.Shell.TaskbarItemProgressState.Indeterminate : System.Windows.Shell.TaskbarItemProgressState.Normal;//Value <= 0 ? System.Windows.Shell.TaskbarItemProgressState.None : System.Windows.Shell.TaskbarItemProgressState.Normal;
            }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(WaitingWindow));
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

        public static readonly DependencyProperty StateProperty = DependencyProperty.Register("State", typeof(string), typeof(WaitingWindow));
        public string State { get => (string)GetValue(StateProperty); set => SetValue(StateProperty, value); }

        public WaitingWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
