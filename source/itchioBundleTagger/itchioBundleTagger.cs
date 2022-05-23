﻿using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteExtensions.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Controls;

namespace itchioBundleTagger
{
    public class itchioBundleTagger : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private itchioBundleTaggerSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("fc4fa75e-6e99-4c02-8547-113747efbb82");
        private Guid ItchIoLibraryId { get; }
        private ICachedFile DatabaseFile { get; }

        public itchioBundleTagger(IPlayniteAPI api) : base(api)
        {
            settings = new itchioBundleTaggerSettings(this);
            Properties = new GenericPluginProperties()
            {
                HasSettings = true
            };
            ItchIoLibraryId = BuiltinExtensions.GetIdFromExtension(BuiltinExtension.ItchioLibrary);
            DatabaseFile = new CachedFileDownloader(
                onlinePath: "https://randombundlegame.com/games.json",
                localPath: Path.Combine(GetPluginUserDataPath(), "games.json"),
                maxCacheAge: TimeSpan.FromDays(180),
                encoding: Encoding.UTF8,
                packagedFallbackPath: Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "games.json"));
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new itchioBundleTaggerSettingsView();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var menuItems = new List<GameMenuItem>();

            if (args.Games.Any(g => g.PluginId == ItchIoLibraryId))
                menuItems.Add(new GameMenuItem { Description = "Tag itch.io bundles", Action = TagItchBundleGames });

            return menuItems;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem { Description = "Refresh itch.io bundle database", Action = DownloadNewDataFile, MenuSection = "@itch.io Bundle Tagger" };
        }

        public void DownloadNewDataFile(MainMenuItemActionArgs args)
        {
            DatabaseFile.RefreshCache();
            PlayniteApi.Dialogs.ShowMessage("itch.io bundle database refreshed");
        }

        public Dictionary<string, ItchIoGame> GetAllBundleGameData()
        {
            return Playnite.SDK.Data.Serialization.FromJson<Dictionary<string, ItchIoGame>>(DatabaseFile.GetFileContents());
        }

        private Tag GetTag(string name)
        {
            string computedTagName = settings.UseTagPrefix ? $"{settings.TagPrefix}{name}" : name;

            var tag = PlayniteApi.Database.Tags.FirstOrDefault(t => t.Name == computedTagName);
            if (tag == null)
            {
                tag = new Tag(computedTagName);
                PlayniteApi.Database.Tags.Add(tag);
            }
            return tag;
        }

        private void AddTagToGame(Game game, Tag tag)
        {
            var tagIds = game.TagIds ?? (game.TagIds = new List<Guid>());

            if (!tagIds.Contains(tag.Id))
                tagIds.Add(tag.Id);
        }

        private void TagItchBundleGames(GameMenuItemActionArgs args)
        {
            PlayniteApi.Dialogs.ActivateGlobalProgress(progressActionArgs =>
            {
                try
                {
                    var relevantGames = args.Games.Where(g => g.PluginId == ItchIoLibraryId).ToList();

                    if (relevantGames.Count == 0)
                        return;

                    progressActionArgs.ProgressMaxValue = relevantGames.Count * 2;

                    var allData = GetAllBundleGameData();

                    if (progressActionArgs.CancelToken.IsCancellationRequested)
                        return;

                    progressActionArgs.CurrentProgressValue = relevantGames.Count;

                    var steamTag = GetTag("Also available on Steam");
                    var palestineTag = GetTag("Indie bundle for Palestinian Aid");
                    var blmTag = GetTag("Bundle for Racial Justice and Equality");
                    var ukraineTag = GetTag("Bundle for Ukraine");
                    var ttrpgTag = GetTag("TTRPGs for Trans Rights in Texas!");

                    progressActionArgs.Text = "Setting tags";

                    using (PlayniteApi.Database.BufferedUpdate())
                    {
                        int i = 0;
                        foreach (var game in relevantGames)
                        {
                            if (progressActionArgs.CancelToken.IsCancellationRequested)
                                return;

                            if (game.GameId == null)
                                continue;

                            if (!allData.TryGetValue(game.GameId, out var data))
                                continue;

                            if (!string.IsNullOrWhiteSpace(data.Steam))
                            {
                                if (settings.AddAvailableOnSteamTag)
                                    AddTagToGame(game, steamTag);

                                if (settings.AddSteamLink)
                                {
                                    List<Link> links = new List<Link>();
                                    if (game.Links != null)
                                        links = new List<Link>(game.Links);
                                    else
                                        links = new List<Link>();

                                    if (!links.Any(l => l.Url.StartsWith(data.Steam)))
                                    {
                                        links.Add(new Link("Steam", data.Steam));
                                        game.Links = new ObservableCollection<Link>(links); //adding to observablecollections on another thread throws exceptions, so just replace them
                                    }
                                }
                            }

                            if (data.Bundles.ContainsKey("pb"))
                                AddTagToGame(game, palestineTag);

                            if (data.Bundles.ContainsKey("blm"))
                                AddTagToGame(game, blmTag);

                            if (data.Bundles.ContainsKey("ukraine"))
                                AddTagToGame(game, ukraineTag);

                            if (data.Bundles.ContainsKey("ttrpg"))
                                AddTagToGame(game, ttrpgTag);

                            PlayniteApi.Database.Games.Update(game);
                            i++;
                            if (i % 10 == 0)
                                progressActionArgs.CurrentProgressValue = relevantGames.Count + i;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error while tagging itch.io bundles");
                    PlayniteApi.Notifications.Add(new NotificationMessage("itch.io bundle tagger error", $"Error while tagging itch.io bundles: {ex.Message}", NotificationType.Error));
                }
            }, new GlobalProgressOptions("Fetching itch.io bundle data") { Cancelable = true, IsIndeterminate = false });
        }
    }

    public class ItchIoGame
    {
        public string Id;
        public string Title;
        public string Steam;
        public Dictionary<string, string> Bundles;
    }
}