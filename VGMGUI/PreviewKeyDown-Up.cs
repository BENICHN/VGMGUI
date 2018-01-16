using System.Linq;
using System.Windows;
using System.Windows.Input;
using CSCore.SoundOut;

namespace VGMGUI
{
    public partial class MainWindow
    {
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            ApplyKeyboardModifiers();

            if (!tasklist.SearchBox.TextBox.IsFocused)
            {
                if (AP.IsMouseOver)
                {
                    switch (key)
                    {
                        case Key.Space:
                            if (AP.PlaybackState == PlaybackState.Stopped) PlayFile(tasklist.FILEList.SelectedItem as Fichier);
                            else AP.PlayPause();
                            e.Handled = true;
                            break;
                        case Key.S:
                            CancelAndStop();
                            break;
                        case Key.PageDown:
                            NextWithRandom();
                            break;
                        case Key.PageUp:
                            PreviousWithRandom();
                            break;
                        case Key.Insert:
                            tasklist.OpenFileDialog(Keyboard.Modifiers == ModifierKeys.Control);
                            break;
                        case Key.Delete:
                            switch (Keyboard.Modifiers)
                            {
                                case ModifierKeys.Control:
                                    tasklist.RemoveInvalidItems();
                                    break;
                                case ModifierKeys.Shift:
                                    tasklist.RemoveAll();
                                    break;
                                default:
                                    tasklist.RemoveSelectedItems();
                                    break;
                            }
                            break;
                        case Key.Left:
                            AP.PositionMinus();
                            break;
                        case Key.Right:
                            AP.PositionPlus();
                            break;
                        default:
                            goto NoMouseOver;
                    }

                    e.Handled = true;
                }
                else if (tasklist.IsMouseOver)
                {
                    switch (key)
                    {
                        case Key.Space:
                            if (AP.PlaybackState == PlaybackState.Stopped) PlayFile(tasklist.FILEList.SelectedItem as Fichier, null);
                            else AP.PlayPause();
                            e.Handled = true;
                            break;
                        case Key.S:
                            CancelAndStop();
                            break;
                        case Key.PageDown:
                            NextWithRandom();
                            e.Handled = true;
                            break;
                        case Key.PageUp:
                            PreviousWithRandom();
                            e.Handled = true;
                            break;
                        case Key.F:
                            if (Keyboard.Modifiers == ModifierKeys.Control)
                            {
                                if (tasklist.SearchBox.Visibility == Visibility.Visible) tasklist.SearchBox.TextBox.Focus();
                                else tasklist.SearchBox.Visibility = Visibility.Visible;
                                e.Handled = true;
                            }
                            break;
                    }

                    if (!tasklist.IsKeyboardFocusWithin) tasklist.FILEList_PreviewKeyDown(sender, e);
                }
            }
            NoMouseOver:
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Control:
                    switch (key)
                    {
                        case Key.O:
                            tasklist.OpenFileDialog(false);
                            break;
                        case Key.P:
                            OpenSettingsWindow();
                            break;
                        case Key.D:
                            VGMStream.DownloadVGMStream();
                            break;
                    }

                    break;
                case (ModifierKeys.Control | ModifierKeys.Shift):
                    switch (key)
                    {
                        case Key.O:
                            tasklist.OpenFileDialog(true);
                            break;
                    }

                    break;
            }

            switch (key)
            {
                case Key.F5:
                    StartButton_Click(StartButton, new RoutedEventArgs());
                    break;

                #if DEBUG

                case Key.X:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        /*switch (App.CurrentCulture.ToString())
                        {
                            case "fr-FR":
                                App.SetLanguage("en-US");
                                break;
                            case "en-US":
                                App.SetLanguage("fr-FR");
                                break;

                        }*/
                    }
                    break;

                    #endif
            }


        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => ApplyKeyboardModifiers();
    }

    public partial class FileList
    {
        public void FILEList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            switch (key)
            {
                case Key.Delete:
                    {
                        switch (Keyboard.Modifiers)
                        {
                            case ModifierKeys.Control:
                                RemoveInvalidItems();
                                break;
                            case ModifierKeys.Shift:
                                RemoveAll();
                                break;
                            default:
                                RemoveSelectedItems();
                                break;
                        }
                    }
                    break;
                case Key.Add:
                case Key.OemPlus:
                    {
                        switch (Keyboard.Modifiers)
                        {
                            case ModifierKeys.Shift:
                                MoveListViewItems(MoveDirection.First);
                                break;
                            case ModifierKeys.Control:
                                MoveListViewItems(MoveDirection.CustomUp);
                                break;
                            case ModifierKeys.Alt:
                                e.Handled = true;
                                MoveListViewItems(MoveDirection.MashUp);
                                break;
                            case ModifierKeys.Alt | ModifierKeys.Shift:
                                e.Handled = true;
                                MoveListViewItems(MoveDirection.FirstMash);
                                break;
                            case ModifierKeys.Alt | ModifierKeys.Control:
                                MoveListViewItems(MoveDirection.CustomUpMash);
                                break;
                            default:
                                MoveListViewItems(MoveDirection.Up);
                                break;
                        }
                    }
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    {
                        switch (Keyboard.Modifiers)
                        {
                            case ModifierKeys.Shift:
                                MoveListViewItems(MoveDirection.Last);
                                break;
                            case ModifierKeys.Control:
                                MoveListViewItems(MoveDirection.CustomDown);
                                break;
                            case ModifierKeys.Alt:
                                e.Handled = true;
                                MoveListViewItems(MoveDirection.MashDown);
                                break;
                            case ModifierKeys.Alt | ModifierKeys.Shift:
                                e.Handled = true;
                                MoveListViewItems(MoveDirection.LastMash);
                                break;
                            case ModifierKeys.Alt | ModifierKeys.Control:
                                MoveListViewItems(MoveDirection.CustomDownMash);
                                break;
                            default:
                                MoveListViewItems(MoveDirection.Down);
                                break;
                        }
                    }
                    break;
                case Key.Insert:
                        OpenFileDialog(Keyboard.Modifiers == ModifierKeys.Control);
                    break;
                case Key.F:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (SearchBox.Visibility == Visibility.Visible) SearchBox.TextBox.Focus();
                        else SearchBox.Visibility = Visibility.Visible;
                    }
                    break;
                case Key.Escape:
                    SearchBox.Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }

    public partial class SearchBox
    {
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            switch (key)
            {
                case Key.Escape:
                    Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }

    public partial class SettingsWindow
    {
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!stbx_max_conversion.IsFocused)
            {
                Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

                switch (key)
                {
                    case Key.Enter:
                        if (m_valid)
                        {
                            OK(false);
                            e.Handled = true;
                        }
                        break;
                    case Key.Escape:
                        Cancel();
                        e.Handled = true;
                        break;
                    default:return;
                }
            }
        }
    }

    public partial class AskWindow
    {
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            switch (key)
            {
                case Key.Enter:
                    m_result = ListSource;
                    Close();
                    break;
            }
        }
    }
}
