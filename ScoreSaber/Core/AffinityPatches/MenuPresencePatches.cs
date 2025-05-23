﻿using HarmonyLib;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Services;
using ScoreSaber.Core.Utils;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace ScoreSaber.Core.AffinityPatches {
    public class MenuPresencePatches : IAffinity {

        [Inject] private readonly RichPresenceService _richPresenceService = null;

        [AffinityPatch(typeof(SinglePlayerLevelSelectionFlowCoordinator), "HandleStandardLevelDidFinish")]
        [AffinityPostfix]
        public void HandleStopStandardLevelPostfix() {
            var jsonObject = new SceneChangeEvent() {
                Timestamp = _richPresenceService.TimeRightNow,
                Scene = Scene.menu
            };

            _richPresenceService.SendUserProfileChannel("scene_change", jsonObject);
        }

        [AffinityPatch(typeof(SinglePlayerLevelSelectionFlowCoordinator), nameof(SinglePlayerLevelSelectionFlowCoordinator.StartLevel))]
        [AffinityPostfix]
        public void HandleStartLevelPostfix(SinglePlayerLevelSelectionFlowCoordinator __instance, Action beforeSceneSwitchCallback, bool practice) {

            bool isPractice = practice;
            GameMode gameMode = GameMode.solo;

            if (isPractice) {
                gameMode = GameMode.practice;

                // this is to privatise the practice mode song, as it would be exposed in the rich presence, still not shown in UI though.
                var songEventPrivate = new SongRichPresenceInfo(_richPresenceService.TimeRightNow, gameMode,
                                    "PRACTICE",
                                    string.Empty,
                                    "PRACTICE AUTHOR",
                                    "PRACTICE MAPPER",
                                    "Standard",
                                    "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE",
                                    (int)2000000000,
                                    1,
                                    0,
                                    1);

                _richPresenceService.SendUserProfileChannel("start_song", songEventPrivate);
                return;
            }

            GameplayModifiers gameplayModifiers = __instance.gameplayModifiers;
            int startTime = 0;
            if (__instance.isInPracticeView) {
                startTime = (int)__instance._practiceViewController._practiceSettings.startSongTime;
            }

            var songEvent = CreateSongStartEvent(__instance.selectedBeatmapLevel, __instance.selectedBeatmapKey, gameplayModifiers, startTime, gameMode);

            _richPresenceService.SendUserProfileChannel("start_song", songEvent);
        }

        [AffinityPatch(typeof(MultiplayerLevelSelectionFlowCoordinator), nameof(MultiplayerLevelSelectionFlowCoordinator.HandleLobbyGameStateControllerGameStarted))]
        [AffinityPostfix]
        public void HandleMultiplayerGameStartPostfix(MultiplayerLevelSelectionFlowCoordinator __instance, ILevelGameplaySetupData levelGameplaySetupData) {

            // i dont like this, but i have to do it, just in case the users selected level doesnt match what the game started with
            BeatmapLevel beatmapLevel = SongCore.Loader.GetLevelById(levelGameplaySetupData.beatmapKey.levelId);

            GameplayModifiers gameplayModifiers = levelGameplaySetupData.gameplayModifiers;
            int startTime = 0;

            var songEvent = CreateSongStartEvent(beatmapLevel, levelGameplaySetupData.beatmapKey, gameplayModifiers, startTime, GameMode.multiplayer);

            _richPresenceService.SendUserProfileChannel("start_song", songEvent);
        }

        private SongRichPresenceInfo CreateSongStartEvent(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, GameplayModifiers gameplayModifiers, int startTime, GameMode gameMode) {

            string hash = BeatmapUtils.GetHashFromLevelID(beatmapLevel, out bool isOst);

            var songEvent = new SongRichPresenceInfo(_richPresenceService.TimeRightNow, gameMode,
                                    beatmapLevel.songName,
                                    beatmapLevel.songSubName,
                                    BeatmapUtils.FriendlyLevelAuthorName(beatmapLevel.allMappers, beatmapLevel.allLighters),
                                    beatmapLevel.songAuthorName,
                                    beatmapKey.beatmapCharacteristic.SerializedName(),
                                    hash,
                                    (int)beatmapLevel.songDuration,
                                    ((int)beatmapKey.difficulty * 2) + 1,
                                    startTime,
                                    gameplayModifiers.songSpeedMul);

            return songEvent;
        }
    }
}
