﻿using NoteEditor.UI.Model;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace NoteEditor.UI.Presenter
{
    public class SmoothedTimeSamplesPresenter : MonoBehaviour
    {
        NoteEditorModel model;

        void Awake()
        {
            model = NoteEditorModel.Instance;
            model.OnLoadMusicObservable.First().Subscribe(_ => Init());
        }

        void Init()
        {
            var prevFrameSamples = 0f;
            var counter = 0;

            this.UpdateAsObservable()
                .Where(_ => model.Audio.clip != null)
                .Where(_ => model.IsPlaying.Value)
                .Subscribe(_ =>
                {
                    var deltaSamples = counter == 0
                        ? (model.Audio.timeSamples - prevFrameSamples)
                        : model.Audio.clip.frequency * Time.deltaTime;

                    model.SmoothedTimeSamples.Value += deltaSamples;
                    prevFrameSamples = model.SmoothedTimeSamples.Value;

                    counter = ++counter % 180;
                });

            model.TimeSamples
                .Where(_ => model.Audio.clip != null)
                .Where(_ => !model.IsPlaying.Value)
                .Subscribe(timeSamples =>
                {
                    counter = 0;
                    model.SmoothedTimeSamples.Value = timeSamples;
                    prevFrameSamples = timeSamples;
                });
        }
    }
}
