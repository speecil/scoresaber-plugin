﻿using Newtonsoft.Json;
using System.Threading.Tasks;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Core.Data.Models;
using System;
using System.Linq;
using ScoreSaber.UI.Leaderboard;

namespace ScoreSaber.Core.Services {
    internal class LeaderboardService {

        public LeaderboardMap currentLoadedLeaderboard = null;
        public CustomLevelLoader customLevelLoader = null;
        public BeatmapDataLoader beatmapDataLoader = null;

        public LeaderboardService() {
            Plugin.Log.Debug("LeaderboardService Setup");
        }

        public async Task<LeaderboardMap> GetLeaderboardData(int maxMultipliedScore, BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, ScoreSaber.UI.Leaderboard.ScoreSaberLeaderboardViewController.ScoreSaberScoresScope scope, int page, PlayerSpecificSettings playerSpecificSettings, bool filterAroundCountry = false) {

            string leaderboardUrl = GetLeaderboardUrl(beatmapKey, scope, page, filterAroundCountry);
            string leaderboardRawData = await Plugin.HttpInstance.GetAsync(leaderboardUrl);
            Leaderboard leaderboardData = JsonConvert.DeserializeObject<Leaderboard>(leaderboardRawData);

            Plugin.Log.Debug($"Current leaderboard set to: {beatmapKey.levelId}:{beatmapLevel.songName}");
            currentLoadedLeaderboard = new LeaderboardMap(leaderboardData, beatmapLevel, beatmapKey, maxMultipliedScore);
            return currentLoadedLeaderboard;
        }

        public async Task<Leaderboard> GetCurrentLeaderboard(BeatmapKey beatmapKey) {

            string leaderboardUrl = GetLeaderboardUrl(beatmapKey, ScoreSaberLeaderboardViewController.ScoreSaberScoresScope.Global, 1, false);

            int attempts = 0;
            while (attempts < 4) {
                try {
                    string leaderboardRawData = await Plugin.HttpInstance.GetAsync(leaderboardUrl);
                    Leaderboard leaderboardData = JsonConvert.DeserializeObject<Leaderboard>(leaderboardRawData);
                    return leaderboardData;
                } catch (Exception) {
                }
                attempts++;
                await Task.Delay(1000);
            }
            return null;
        }
      
        private string GetLeaderboardUrl(BeatmapKey beatmapKey, ScoreSaberLeaderboardViewController.ScoreSaberScoresScope scope, int page, bool filterAroundCountry) {

            string url = "/game/leaderboard";
            string leaderboardId = beatmapKey.levelId.Split('_')[2];
            string gameMode = $"Solo{beatmapKey.beatmapCharacteristic.serializedName}";
            string difficulty = BeatmapDifficultyMethods.DefaultRating(beatmapKey.difficulty).ToString();

            bool hasPage = true;

            switch (scope) {
                case ScoreSaberLeaderboardViewController.ScoreSaberScoresScope.Global:
                    url = $"{url}/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                    break;
                case ScoreSaberLeaderboardViewController.ScoreSaberScoresScope.AroundPlayer:
                    url = $"{url}/around-player/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}";
                    hasPage = false;
                    break;
                case ScoreSaberLeaderboardViewController.ScoreSaberScoresScope.Friends:
                    url = $"{url}/around-friends/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                    break;
                case ScoreSaberLeaderboardViewController.ScoreSaberScoresScope.Area:
                    if (Plugin.Settings.locationFilterMode.ToLower() == "region") {
                        url = $"{url}/around-region/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                    } else if (Plugin.Settings.locationFilterMode.ToLower() == "country") {
                        url = $"{url}/around-country/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                    } else {
                        Plugin.Log.Error("Invalid location filter mode, falling back to country");
                        url = $"{url}/around-country/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                    }
                    break;
            }

            if (Plugin.Settings.hideNAScoresFromLeaderboard) {
                if (hasPage)
                    url = $"{url}&hideNA=1";
                else 
                    url = $"{url}?hideNA=1";
            }

            return url;
        }
    }
}
