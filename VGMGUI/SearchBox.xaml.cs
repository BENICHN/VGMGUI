using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static VGMGUI.Settings;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour SearchBox.xaml
    /// </summary>
    public partial class SearchBox : UserControl
    {
        public SearchBox()
        {
            InitializeComponent();
        }

        private void btn_close_Click(object sender, RoutedEventArgs e) => Visibility = Visibility.Collapsed;

        private void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue) TextBox.Focus();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e) => TextBox.SelectAll();

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!btn_close.IsFocused) Opacity = 0.3;
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e) => Opacity = 1;

        private void cbx_column_SelectionChanged(object sender, SelectionChangedEventArgs e) => SearchColumn = FileList.FLCOLToFileListColumn(App.Res((cbx_column.SelectedItem as ComboBoxItem).Content.ToString(), indice: "FL_COL_")) ?? FileListColumn.Name;

        private void cbx_column_Loaded(object sender, RoutedEventArgs e) => cbx_column.SelectedItem = cbx_column.Items.OfType<ComboBoxItem>().FirstOrDefault(item => App.Res(item.Content.ToString(), indice: "FL_COL_") == FileList.FileListColumnToFLCOL(SearchColumn)) ?? cbx_column.Items[0];

        private void btn_case_CheckedUnchecked(object sender, RoutedEventArgs e) => SearchCaseSensitive = btn_case.IsChecked ?? false;
    }
}
