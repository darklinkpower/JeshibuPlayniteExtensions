﻿using Newtonsoft.Json;
using Playnite.SDK;
using PlayniteExtensions.Metadata.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MobyGamesMetadata
{
    public class MobyGamesMetadataSettings : ObservableObject
    {
        private string apiKey;

        public DataSource DataSource { get; set; } = DataSource.Api;
        public string ApiKey
        {
            get { return apiKey; }
            set
            {
                apiKey = value?.Trim();
                OnPropertyChanged(nameof(ApiKey));
            }
        }
        public bool ShowTopPanelButton { get; set; } = true;

        public ObservableCollection<MobyGamesGenreSetting> Genres { get; set; } = new ObservableCollection<MobyGamesGenreSetting>();
    }

    [Flags]
    public enum DataSource
    {
        None = 0,
        Api = 1,
        Scraping = 2,
        ApiAndScraping = 3,
    }

    public class MobyGamesGenreSetting : ObservableObject
    {
        private string nameOverride;
        private PropertyImportTarget importTarget = PropertyImportTarget.Genres;

        [JsonProperty("genre_category_id")]
        public int CategoryId { get; set; }

        [JsonProperty("genre_category")]
        public string Category { get; set; }

        [JsonProperty("genre_id")]
        public int Id { get; set; }

        [JsonProperty("genre_name")]
        public string Name { get; set; }

        public string NameOverride
        {
            get { return nameOverride; }
            set
            {
                nameOverride = value;
                OnPropertyChanged(nameof(NameOverride));
            }
        }

        public PropertyImportTarget ImportTarget
        {
            get { return importTarget; }
            set
            {
                importTarget = value;
                OnPropertyChanged(nameof(ImportTarget));
            }
        }
    }

    internal class MobyGamesGenresRoot
    {
        public List<MobyGamesGenreSetting> Genres { get; set; }
    }

    public class MobyGamesMetadataSettingsViewModel : PluginSettingsViewModel<MobyGamesMetadataSettings, MobyGamesMetadata>
    {
        public MobyGamesMetadataSettingsViewModel(MobyGamesMetadata plugin) : base(plugin, plugin.PlayniteApi)
        {
            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<MobyGamesMetadataSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new MobyGamesMetadataSettings();
            }
            InitializeGenres();
        }

        public RelayCommand<object> GetApiKeyCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Process.Start(@"https://www.mobygames.com/info/api/");
            });
        }

        public PropertyImportTarget[] ImportTargets { get; } = new[]
        {
            PropertyImportTarget.Ignore,
            PropertyImportTarget.Genres,
            PropertyImportTarget.Tags,
            PropertyImportTarget.Features,
        };

        private void InitializeGenres()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var genresPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), "genres.json");
            var genresContent = File.ReadAllText(genresPath);
            var root = JsonConvert.DeserializeObject<MobyGamesGenresRoot>(genresContent);
            bool genreAdded = false;
            foreach (var newGenre in root.Genres)
            {
                var existingGenre = Settings.Genres.FirstOrDefault(g => g.Id == newGenre.Id);
                if (existingGenre != null)
                {
                    existingGenre.CategoryId = newGenre.CategoryId;
                    existingGenre.Category = newGenre.Category;
                    existingGenre.Name = newGenre.Name;
                }
                else
                {
                    newGenre.ImportTarget = GetDefaultImportTarget(newGenre);
                    Settings.Genres.Add(newGenre);
                    genreAdded = true;
                }
            }
            if (genreAdded)
            {
                var orderedGenres = Settings.Genres.OrderBy(g => g.Category).ThenBy(g => g.Name).ToList();
                Settings.Genres.Clear();
                foreach (var g in orderedGenres)
                {
                    Settings.Genres.Add(g);
                }
            }
            Logger.Debug($"{Settings.Genres.Count} genres");
        }

        private PropertyImportTarget GetDefaultImportTarget(MobyGamesGenreSetting genre)
        {
            switch (genre.CategoryId)
            {
                case 1: //Basic Genres
                case 2: //Perspective
                case 4: //Gameplay
                    return PropertyImportTarget.Genres;
                case 14: //DLC/Add-on
                case 15: //Special Edition
                    return PropertyImportTarget.Ignore;
                case 3: //Sports Themes
                case 5: //Educational Categories
                case 6: //Other Attributes
                case 7: //Interface/Control
                case 8: //Narrative Theme/Topic
                case 9: //Pacing
                case 10: //Setting
                case 11: //Vehicular Themes
                case 12: //Visual Presentation
                case 13: //Art Style
                default:
                    return PropertyImportTarget.Tags;
            }
        }
    }
}