﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using BenLib;
using BenLib.WPF;
using System.Windows.Controls;
using System.Windows.Input;

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
            if (sender is ContextMenu contextMenu && contextMenu.PlacementTarget is ListViewItem item && ItemsControl.ItemsControlFromItemContainer(item) is ListView listView)
            {
                var selectedItems = listView.SelectedItems.OfType<Fichier>();
                contextMenu.Items.FindCollectionItem<MenuItem>("SkipMI").IsEnabled = !selectedItems.All(fichier => !fichier.IsCancellable);
            }
        }
    }
}
