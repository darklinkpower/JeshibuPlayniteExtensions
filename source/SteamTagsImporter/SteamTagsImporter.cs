﻿using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SteamTagsImporter
{
    public class SteamTagsImporter : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Func<ISteamAppIdUtility> getAppIdUtility;
        private readonly Func<ISteamTagScraper> getTagScraper;

        public SteamTagsImporterSettings Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("01b67948-33a1-42d5-bd39-e4e8a226d215");

        public SteamTagsImporter(IPlayniteAPI api)
            : this(api, () => new SteamAppIdUtility(), () => new SteamTagScraper())
        {
        }

        public SteamTagsImporter(IPlayniteAPI api, Func<ISteamAppIdUtility> getAppIdUtility, Func<ISteamTagScraper> getTagScraper)
            : base(api)
        {
            this.Settings = new SteamTagsImporterSettings(this);
            this.getAppIdUtility = getAppIdUtility;
            this.getTagScraper = getTagScraper;
            this.Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override ISettings GetSettings(bool firstRunSettings = false)
        {
            if (firstRunSettings)
                Settings = new SteamTagsImporterSettings() { LastAutomaticTagUpdate = DateTime.Now };
            else
                Settings = Settings ?? LoadPluginSettings<SteamTagsImporterSettings>();

            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamTagsImporterSettingsView();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            return new GameMenuItem[] { new GameMenuItem { Description = "Import Steam tags", Action = x => SetTagsAccordingToSettings(x.Games) } };
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (!Settings.AutomaticallyAddTagsToNewGames)
                return;

            List<Game> games;
            if (Settings.LastAutomaticTagUpdate == DateTime.MinValue)
                games = new List<Game>();
            else
                games = PlayniteApi.Database.Games.Where(g => g.Added > Settings.LastAutomaticTagUpdate).ToList();

            logger.Debug($"Library update: {games.Count} games");

            SetTagsAccordingToSettings(games);

            Settings.LastAutomaticTagUpdate = DateTime.Now;
            SavePluginSettings(Settings);
        }

        public void SetTagsAccordingToSettings(List<Game> games)
        {
            if (Settings.LimitTagsToFixedAmount)
                SetTags(games, Settings.FixedTagCount);
            else
                SetTags(games);
        }

        public void SetTags(List<Game> games, int? max = null)
        {
            PlayniteApi.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                logger.Debug($"Adding tags to {games.Count} games (tag max: {max})");

                var appIdUtility = getAppIdUtility();
                var tagScraper = getTagScraper();
                using (PlayniteApi.Database.BufferedUpdate())
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            return;

                        try
                        {
                            string appId = appIdUtility.GetSteamGameId(game);
                            if (string.IsNullOrEmpty(appId))
                            {
                                logger.Debug($"Couldn't find app ID for game {game.Name}");
                                args.CurrentProgressValue++;
                                continue;
                            }

                            var tags = tagScraper.GetTags(appId);

                            if (max.HasValue)
                                tags = tags.Take(max.Value);

                            bool tagsAdded = false;
                            foreach (var tag in tags)
                            {
                                tagsAdded |= AddTagToGame(game, tag);
                            }

                            if (tagsAdded)
                            {
                                game.Modified = DateTime.Now;
                                PlayniteApi.Database.Games.Update(game);
                            }

                            args.CurrentProgressValue++;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Error setting Steam tags");
                        }
                    }
                }
            }, new GlobalProgressOptions("Applying Steam tags to games", cancelable: true) { IsIndeterminate = false });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game"></param>
        /// <param name="tagName"></param>
        /// <returns>true if the tag was added, false if not</returns>
        private bool AddTagToGame(Game game, string tagName)
        {
            var tagIds = game.TagIds ?? (game.TagIds = new List<Guid>());

            var tag = PlayniteApi.Database.Tags.FirstOrDefault(t => tagName.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase));

            if (tag == null)
            {
                tag = new Tag(tagName);
                PlayniteApi.Database.Tags.Add(tag);
            }

            bool whitelisted = Settings.OkayTags.Contains(tagName);
            bool blacklisted = Settings.BlacklistedTags.Contains(tagName);

            if (!whitelisted && !blacklisted)
                Settings.OkayTags.Add(tagName);

            if (!tagIds.Contains(tag.Id) && !blacklisted)
            {
                tagIds.Add(tag.Id);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}