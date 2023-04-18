﻿using PlayniteExtensions.Metadata.Common;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MobyGamesMetadata
{
    public partial class MobyGamesMetadataSettingsView : UserControl
    {
        public MobyGamesMetadataSettingsView()
        {
            InitializeComponent();
        }

        private void SetImportTarget_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is MobyGamesMetadataSettingsViewModel viewModel)
                || !(e.Source is MenuItem menuItem)
                || !Enum.TryParse<PropertyImportTarget>(menuItem.Header.ToString(), out var target))
                return;

            var selectedItems = GenreSettingsView.SelectedItems.Cast<MobyGamesGenreSetting>().ToList();

            viewModel.SetImportTarget(target, selectedItems);
        }

        private void SetNameOverride(Func<MobyGamesGenreSetting, string> nameOverrideGetter)
        {
            if (!(DataContext is MobyGamesMetadataSettingsViewModel viewModel))
                return;

            var selectedItems = GenreSettingsView.SelectedItems.Cast<MobyGamesGenreSetting>().ToList();
            foreach (var item in selectedItems)
            {
                item.NameOverride = nameOverrideGetter.Invoke(item);
            }
        }

        private void SetNameOverride_Name(object sender, RoutedEventArgs e)
        {
            SetNameOverride(item => item.Name);
        }

        private void SetNameOverride_Category(object sender, RoutedEventArgs e)
        {
            SetNameOverride(item => $"{item.Category}: {item.Name}");
        }

        private void SetNameOverride_CustomPrefix(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is MobyGamesMetadataSettingsViewModel viewModel))
                return;

            var prefixInput = viewModel.PlayniteApi.Dialogs.SelectString("Select the prefix for the selected genres", "Select prefix", string.Empty);
            if(!prefixInput.Result)
                return;

            var prefix = prefixInput.SelectedString.TrimEnd(' ', ':');

            SetNameOverride(item => $"{prefix}: {item.Name}");
        }

        private void SetNameOverride_Remove(object sender, RoutedEventArgs e)
        {
            SetNameOverride(item => null);
        }
    }
}