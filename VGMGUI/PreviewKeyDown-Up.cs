﻿using System;
using System.Windows;
using System.Windows.Input;
using Vlc.DotNet.Core.Interops.Signatures;

namespace VGMGUI
{
    public partial class MainWindow
    {
        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            ApplyKeyboardModifiers();

            if (!tasklist.SearchBox.TextBox.IsFocused)
            {
                if (AP.IsMouseOver && !AP.IsKeyboardFocusWithin)
                {
                    e.Handled = true;
                    switch (key)
                    {
                        case Key.Space:
                            if (Keyboard.Modifiers == ModifierKeys.Control) tasklist.FILEList.ScrollIntoView(AP.CurrentPlaying);
                            else
                            {
                                if (AP.Player.State == MediaStates.Stopped || AP.Player.State == MediaStates.NothingSpecial) await PlayFile(tasklist.FILEList.SelectedItem as Fichier, null);
                                else await AP.PlayPause();
                            }
                            break;
                        case Key.S:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                            {
                                await VGMStream.DeleteTempFilesIfNotUsed();
                                GC.Collect();
                            }
                            else await CancelAndStop();
                            break;
                        case Key.PageDown:
                            await NextWithRandom();
                            break;
                        case Key.PageUp:
                            await PreviousWithRandom();
                            break;
                        case Key.Insert:
                            tasklist.OpenFileDialog((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt);
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
                                case ModifierKeys.Alt:
                                    tasklist.RemoveDFiles();
                                    break;
                                case ModifierKeys.Control | ModifierKeys.Alt:
                                    tasklist.RemoveSNFiles();
                                    break;
                                default:
                                    tasklist.RemoveSelectedItems();
                                    break;
                            }
                            break;
                        case Key.Left:
                            await AP.PositionMinus();
                            break;
                        case Key.Right:
                            await AP.PositionPlus();
                            break;
                        default:
                            goto NoMouseOver;
                    }

                }
                if (tasklist.IsMouseOver && !tasklist.IsKeyboardFocusWithin)
                {
                    switch (key)
                    {
                        case Key.F:
                            if (Keyboard.Modifiers == ModifierKeys.Control)
                            {
                                e.Handled = true;
                                if (tasklist.SearchBox.Visibility == Visibility.Visible) tasklist.SearchBox.TextBox.Focus();
                                else tasklist.SearchBox.Visibility = Visibility.Visible;
                            }
                            break;
                        case Key.C:
                            if (Keyboard.Modifiers == ModifierKeys.Control) tasklist.CopySelectedFiles();
                            break;
                        case Key.X:
                            if (Keyboard.Modifiers == ModifierKeys.Control) tasklist.CutSelectedFiles();
                            break;
                        case Key.V:
                            if (Keyboard.Modifiers == ModifierKeys.Control) tasklist.PasteFiles();
                            break;
                    }

                    if (!tasklist.IsKeyboardFocusWithin) tasklist.FILEList_PreviewKeyDown(sender, e);
                }
                if (tasklist.IsMouseOver && !AP.IsKeyboardFocusWithin)
                {
                    switch (key)
                    {
                        case Key.Space:
                            e.Handled = true;
                            if (Keyboard.Modifiers == ModifierKeys.Control) tasklist.FILEList.ScrollIntoView(AP.CurrentPlaying);
                            else
                            {
                                if (AP.Player.State == MediaStates.Stopped || AP.Player.State == MediaStates.NothingSpecial) await PlayFile(tasklist.FILEList.SelectedItem as Fichier, null);
                                else await AP.PlayPause();
                            }
                            break;
                        case Key.S:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                            {
                                await VGMStream.DeleteTempFilesIfNotUsed();
                                GC.Collect();
                            }
                            else await CancelAndStop();
                            break;
                        case Key.PageDown:
                            e.Handled = true;
                            await NextWithRandom();
                            break;
                        case Key.PageUp:
                            e.Handled = true;
                            await PreviousWithRandom();
                            break;
                    }
                }
            }

            NoMouseOver:
            switch (key)
            {
                case Key.Play:
                    if (AP.Player.State == MediaStates.Stopped || AP.Player.State == MediaStates.NothingSpecial) await PlayFile(tasklist.FILEList.SelectedItem as Fichier, null);
                    else await AP.Play();
                    break;
                case Key.Pause:
                    await AP.Pause();
                    break;
                case Key.MediaPlayPause:
                    if (AP.Player.State == MediaStates.Stopped || AP.Player.State == MediaStates.NothingSpecial) await PlayFile(tasklist.FILEList.SelectedItem as Fichier, null);
                    else await AP.PlayPause();
                    break;
                case Key.MediaStop:
                    await CancelAndStop();
                    break;
                case Key.MediaNextTrack:
                    await NextWithRandom();
                    break;
                case Key.MediaPreviousTrack:
                    await PreviousWithRandom();
                    break;
                case Key.B:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        e.Handled = true;
                        StatusBar.Display = !StatusBar.Display;
                        Settings.SettingsData["StatusBar"]["Display"] = StatusBar.Display.ToString();
                    }
                    break;
                case Key.P:
                    if (Keyboard.Modifiers == ModifierKeys.Control) OpenSettingsWindow();
                    break;
                case Key.O:
                    switch (Keyboard.Modifiers)
                    {
                        case ModifierKeys.Control | ModifierKeys.Shift:
                            tasklist.OpenFileDialog(true, false);
                            break;
                        case ModifierKeys.Control:
                            tasklist.OpenFileDialog(false, false);
                            break;
                        case ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift:
                            tasklist.OpenFileDialog(true, true);
                            break;
                        case ModifierKeys.Control | ModifierKeys.Alt:
                            tasklist.OpenFileDialog(false, true);
                            break;
                    }
                    break;
                case Key.D:
                    switch (Keyboard.Modifiers)
                    {
                        case ModifierKeys.Control | ModifierKeys.Alt:
                            if (await VGMStream.DownloadVLC()) MessageBox.Show(App.Str("WW_VLCDownloaded"), string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                        case ModifierKeys.Control:
                            await VGMStream.DownloadVGMStream();
                            break;
                    }
                    break;
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
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
                            case ModifierKeys.Alt:
                                RemoveDFiles();
                                break;
                            case ModifierKeys.Shift | ModifierKeys.Alt:
                                RemoveSNFiles();
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
                    OpenFileDialog((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt);
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
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control) CopySelectedFiles();
                    break;
                case Key.X:
                    if (Keyboard.Modifiers == ModifierKeys.Control) CutSelectedFiles();
                    break;
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control) PasteFiles();
                    break;
            }
        }
    }

    public partial class SearchBox
    {
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
                var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
                    default: return;
                }
            }
        }
    }

    public partial class AskWindow
    {
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
