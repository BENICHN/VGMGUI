using BenLib;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Clipboard = System.Windows.Forms.Clipboard;

namespace VGMGUI
{
    public partial class MainWindow
    {
        private async void PlayInMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            await CancelAndStop();
            await PlayFile(fsender, false, true);
        }

        private async void PlayOutMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            await CancelAndStop();
            await PlayFile(fsender, true, true);
        }

        private async void ConvertMI(object sender, RoutedEventArgs e) => await LaunchConversion(tasklist.GetSelectedFiles());

        private void SkipMI(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is ListViewItem item && ItemsControl.ItemsControlFromItemContainer(item) is ListView listView)
            {
                foreach (Fichier fichier in listView.SelectedItems) fichier.CancelIfCancellable();
            }
        }
    }

    public partial class AskWindow
    {
        private void CopyFPMI(object sender, RoutedEventArgs e)
        {
            AskingFile fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as AskingFile;
            Threading.MultipleAttempts(() => { Clipboard.SetText("\"" + fsender.Name + "\""); }, throwEx: false);
        }

        private void OpenFPMI(object sender, RoutedEventArgs e)
        {
            AskingFile fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as AskingFile;
            Process.Start("explorer.exe", "/select, \"" + fsender.Name + "\"");
        }

        private void PropertiesMI(object sender, RoutedEventArgs e)
        {
            AskingFile fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as AskingFile;
            IO.ShowFileProperties(fsender.Name);
        }

        private void OverwriteMI(object sender, RoutedEventArgs e)
        {
            foreach (AskingFile file in AskList.SelectedItems) file.Overwrite = true;
        }

        private void NumberMI(object sender, RoutedEventArgs e)
        {
            foreach (AskingFile file in AskList.SelectedItems) file.Number = true;
        }

        private void IgnoreMI(object sender, RoutedEventArgs e)
        {
            foreach (AskingFile file in AskList.SelectedItems) file.Overwrite = file.Number = false;
        }
    }

    public partial class FileList
    {
        private void DeleteMI(object sender, RoutedEventArgs e) => RemoveSelectedItems();

        private void CopyMI(object sender, RoutedEventArgs e) => CopySelectedFiles();

        private void CutMI(object sender, RoutedEventArgs e) => CutSelectedFiles();

        private void PasteMI(object sender, RoutedEventArgs e) => PasteFiles();

        private void AnalyzeMI(object sender, RoutedEventArgs e) => AnalyzeFiles(GetSelectedFiles());

        private void UnCheckMI(object sender, RoutedEventArgs e)
        {
            foreach (Fichier fichier in FILEList.SelectedItems) fichier.Checked = false;
        }

        private void CheckMI(object sender, RoutedEventArgs e)
        {
            foreach (Fichier fichier in FILEList.SelectedItems) fichier.Checked = true;
        }

        private void OpenFPMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            Process.Start("explorer.exe", "/select, \"" + fsender.Path + "\"");
        }

        private void CopyFPMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            Threading.MultipleAttempts(() => { Clipboard.SetText("\"" + fsender.Path + "\""); }, throwEx: false);
        }

        private void PropertiesMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            IO.ShowFileProperties(fsender.Path);
        }
    }

    public partial class ErrorWindow
    {
        private void CopyFPMI(object sender, RoutedEventArgs e)
        {
            string ssender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListBoxItem).Content as string;
            Threading.MultipleAttempts(() => { Clipboard.SetText("\"" + ssender + "\""); }, throwEx: false);
        }

        private void OpenFPMI(object sender, RoutedEventArgs e)
        {
            string ssender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListBoxItem).Content as string;
            Process.Start("explorer.exe", "/select, \"" + ssender + "\"");
        }

        private void PropertiesMI(object sender, RoutedEventArgs e)
        {
            string ssender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListBoxItem).Content as string;
            IO.ShowFileProperties(ssender);
        }
    }
}
