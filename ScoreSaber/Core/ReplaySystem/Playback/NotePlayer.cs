﻿using IPA.Utilities;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Extensions;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zenject;
using static NoteData;

namespace ScoreSaber.Core.ReplaySystem.Playback {
    internal class NotePlayer : TimeSynchronizer, ITickable, IScroller, IAffinity {
        private int _nextIndex = 0;
        private readonly SiraLog _siraLog;
        private readonly SaberManager _saberManager;
        private readonly NoteEvent[] _sortedNoteEvents;
        private readonly MemoryPoolContainer<GameNoteController> _gameNotePool;
        private readonly MemoryPoolContainer<GameNoteController> _burstSliderHeadNotePool;
        private readonly MemoryPoolContainer<BurstSliderGameNoteController> _burstSliderNotePool;
        private readonly MemoryPoolContainer<BombNoteController> _bombNotePool;

        private readonly Dictionary<NoteCutInfo, NoteEvent> _recognizedNoteCutInfos = new Dictionary<NoteCutInfo, NoteEvent>();
        private readonly Dictionary<NoteID, NoteCutInfo> _noteCutInfoCache = new Dictionary<NoteID, NoteCutInfo>();

        public NotePlayer(SiraLog siraLog, ReplayFile file, SaberManager saberManager, BasicBeatmapObjectManager basicBeatmapObjectManager) {

            _siraLog = siraLog;
            _saberManager = saberManager;
            _gameNotePool = basicBeatmapObjectManager._basicGameNotePoolContainer;
            _burstSliderHeadNotePool = basicBeatmapObjectManager._burstSliderHeadGameNotePoolContainer;
            _burstSliderNotePool = basicBeatmapObjectManager._burstSliderGameNotePoolContainer;
            _bombNotePool = basicBeatmapObjectManager._bombNotePoolContainer;
            _sortedNoteEvents = file.noteKeyframes.OrderBy(nk => nk.Time).ToArray();
        }

        public void Tick() {
            while (_nextIndex < _sortedNoteEvents.Length && audioTimeSyncController.songTime >= _sortedNoteEvents[_nextIndex].Time) {
                NoteEvent activeEvent = _sortedNoteEvents[_nextIndex++];
                ProcessEvent(activeEvent);
            }
        }

        private void ProcessEvent(NoteEvent activeEvent) {
            switch (activeEvent.EventType) {
                case NoteEventType.BadCut:
                    goto case NoteEventType.GoodCut;
                case NoteEventType.GoodCut:
                    _gameNotePool.activeItems.ForEach(x => HandleEvent(activeEvent, x));
                    _burstSliderHeadNotePool.activeItems.ForEach(x => HandleEvent(activeEvent, x));
                    _burstSliderNotePool.activeItems.ForEach(x => HandleEvent(activeEvent, x));
                    break;
                case NoteEventType.Bomb:
                    _bombNotePool.activeItems.ForEach(x => HandleEvent(activeEvent, x));
                    break;
                default:
                    break;
            }
        }

        private bool HandleEvent(NoteEvent activeEvent, NoteController noteController) {
            if (DoesNoteMatchID(activeEvent.NoteID, noteController.noteData)) {
                if (!_noteCutInfoCache.TryGetValue(activeEvent.NoteID, out NoteCutInfo noteCutInfo)) {
                    
                    Saber correctSaber = noteController.noteData.colorType == ColorType.ColorA ? _saberManager.leftSaber : _saberManager.rightSaber;
                    var noteTransform = noteController.noteTransform;

                    noteCutInfo = new NoteCutInfo(noteController.noteData,
                        activeEvent.SaberSpeed > 2f,
                        activeEvent.DirectionOK,
                        activeEvent.SaberType == (int)correctSaber.saberType,
                        false,
                        activeEvent.SaberSpeed,
                        activeEvent.SaberDirection.Convert(),
                        noteController.noteData.colorType == ColorType.ColorA ? SaberType.SaberA : SaberType.SaberB,
                        noteController.noteData.time - activeEvent.Time,
                        activeEvent.CutDirectionDeviation,
                        activeEvent.CutPoint.Convert(),
                        activeEvent.CutNormal.Convert(),
                        activeEvent.CutDistanceToCenter,
                        activeEvent.CutAngle,

                        noteController.worldRotation,
                        noteController.inverseWorldRotation,
                        noteTransform.rotation,
                        noteTransform.position,

                        correctSaber.movementDataForLogic
                    );

                    _noteCutInfoCache[activeEvent.NoteID] = noteCutInfo;
                }

                _recognizedNoteCutInfos.Add(noteCutInfo, activeEvent);
                noteController.InvokeMethod<object, NoteController>("SendNoteWasCutEvent", noteCutInfo);
                return true;
            }
            return false;
        }

        bool DoesNoteMatchID(NoteID id, NoteData noteData) {
            if (!Mathf.Approximately(id.Time, noteData.time) || id.LineIndex != noteData.lineIndex || id.LineLayer != (int)noteData.noteLineLayer || id.ColorType != (int)noteData.colorType || id.CutDirection != (int)noteData.cutDirection)
                return false;

            if (id.GameplayType is int gameplayType && gameplayType != (int)noteData.gameplayType)
                return false;

            if (id.ScoringType is int scoringType && scoringType != (int)noteData.scoringType)
                return false;

            if (id.CutDirectionAngleOffset is float cutDirectionAngleOffset && !Mathf.Approximately(cutDirectionAngleOffset, noteData.cutDirectionAngleOffset))
                return false;

            return true;
        }

        [AffinityPostfix, AffinityPatch(typeof(GoodCutScoringElement), nameof(GoodCutScoringElement.Init))]
        protected void ForceCompleteGoodScoringElements(GoodCutScoringElement __instance, NoteCutInfo noteCutInfo, CutScoreBuffer ____cutScoreBuffer) {
            if (!_recognizedNoteCutInfos.TryGetValue(noteCutInfo, out var activeEvent))
                return;

            _recognizedNoteCutInfos.Remove(noteCutInfo);

            if (!__instance.isFinished) {
                var ratingCounter = ____cutScoreBuffer._saberSwingRatingCounter;

                ratingCounter._afterCutRating = activeEvent.AfterCutRating;
                ratingCounter._beforeCutRating = activeEvent.BeforeCutRating;

                ____cutScoreBuffer.HandleSaberSwingRatingCounterDidFinish(ratingCounter);

                ScoringElement element = __instance;
                element.isFinished = true;
            }
        }

        public void TimeUpdate(float newTime) {
            for (int c = 0; c < _sortedNoteEvents.Length; c++) {
                if (_sortedNoteEvents[c].Time > newTime) {
                    _nextIndex = c;
                    return;
                }
            }
            _nextIndex = _sortedNoteEvents.Length;
        }
    }
}