using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clipboard = System.Windows.Forms.Clipboard;

namespace VGMGUI
{
    public partial class Global : ResourceDictionary
    {
        public Global()
        {
            InitializeComponent();
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Source is TextBox textBox)
            {
                switch ((e.Command as RoutedUICommand).Name)
                {
                    case "Copy":
                        BenLib.WPF.ApplicationCommands.Copy.Execute(e.Source);
                        break;
                    case "Cut":
                        if (!textBox.IsReadOnly) BenLib.WPF.ApplicationCommands.Cut.Execute(e.Source);
                        else goto case "Copy";
                        break;
                    case "Paste":
                        if (!textBox.IsReadOnly) BenLib.WPF.ApplicationCommands.Paste.Execute(e.Source);
                        break;
                }
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu ctxtMenu && ctxtMenu.PlacementTarget is ListView lstView && lstView.Parent is Grid grd && grd.Parent is FileList flList) F_PasteMI.IsEnabled = flList.CanAdd && Clipboard.GetData("FichierCollection") is Fichier[] array && array.Length > 0;
            if (sender is ContextMenu contextMenu && contextMenu.PlacementTarget is ListViewItem item && ItemsControl.ItemsControlFromItemContainer(item) is ListView listView && listView.Parent is Grid grid && grid.Parent is FileList fileList)
            {
                CutMI.IsEnabled = fileList.CanRemove;
                PasteMI.IsEnabled = fileList.CanAdd && Clipboard.GetData("FichierCollection") is Fichier[] array && array.Length > 0;

                var selectedItems = listView.SelectedItems.OfType<Fichier>();
                SkipMI.IsEnabled = selectedItems.Any(fichier => fichier.IsCancellable);
            }
        }
    }
}
