using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using BenLib;
using Clipboard = System.Windows.Forms.Clipboard;

namespace VGMGUI
{
    public partial class MainWindow
    {
        private void PlayInMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            CancelAndStop();
            PlayFile(fsender, false, true);
        }

        private void PlayOutMI(object sender, RoutedEventArgs e)
        {
            Fichier fsender = (((ContextMenu)(sender as MenuItem).Parent).PlacementTarget as ListViewItem).Content as Fichier;
            CancelAndStop();
            PlayFile(fsender, true, true);
        }

        private async void ConvertMI(object sender, RoutedEventArgs e)
        {
            Preconversion = true;

            if (File.Exists(App.VGMStreamPath) || await App.AskVGMStream())
            {
                if (!MainDestTB.Text.ContainsAny(Literal.ForbiddenPathNameCharacters))
                {
                    foreach (Fichier fichier in tasklist.FILEList.SelectedItems)
                    {
                        if ((fichier.FinalDestination = await GetOrCreateDestinationFileAsync(fichier)) != null)
                        {
                            FilesToConvertTMP.Add(fichier);
                            fichier.SetValid(); // <=> fichier.State = "En attente"
                        }
                    } //Remplissage de "FilesToConvert"

                    StartConversion();
                }
                else MessageBox.Show(App.Str("ERR_UnauthorizedChars"), App.Str("TT_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else Preconversion = false;
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

        private void AnalyzeMI(object sender, RoutedEventArgs e) => AnalyzeFiles(GetSelectedFiles().ToArray());

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
    }
}
