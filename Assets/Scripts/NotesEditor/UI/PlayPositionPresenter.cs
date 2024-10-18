﻿using UniRx;
using UniRx.Triggers;
using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayPositionPresenter : MonoBehaviour
{
    [SerializeField]
    CanvasEvents canvasEvents;
    [SerializeField]
    RectTransform canvasRect;
    [SerializeField]
    Slider playPositionController;
    [SerializeField]
    Text playPositionDisplayText;

    NotesEditorModel model;

    void Awake()
    {
        model = NotesEditorModel.Instance;
        model.OnLoadedMusicObservable.Subscribe(_ => Init());
    }

    void Init()
    {
        // Binds canvas position with samples
        this.UpdateAsObservable()
            .Select(_ => model.Audio.timeSamples)
            .DistinctUntilChanged()
            .Merge(model.CanvasWidth.Select(_ => model.Audio.timeSamples)) // Merge resized timing
            .Select(timeSamples => timeSamples / (float)model.Audio.clip.samples)
            .Select(per => canvasRect.sizeDelta.x * per)
            .Select(x => x + model.CanvasOffsetX.Value)
            .Subscribe(x => canvasRect.localPosition = Vector3.left * x);


        // Binds play position controller with samples
        this.UpdateAsObservable()
            .Select(_ => model.Audio.timeSamples)
            .DistinctUntilChanged()
            .Select(timeSamples => timeSamples / (float)model.Audio.clip.samples)
            /*
            .Do(per => playPositionController.value = per)
            // */
            .Select(per => new TimeSpan(0, 0, Mathf.FloorToInt(model.Audio.time)).ToString().Substring(3, 5)
                + " / "
                + new TimeSpan(0, 0, Mathf.RoundToInt(model.Audio.clip.samples / model.Audio.clip.frequency)).ToString().Substring(3, 5))
            .SubscribeToText(playPositionDisplayText);


        // Binds samples with dragging canvas and mouse scroll wheel and slider
        this.UpdateAsObservable()
            .SkipUntil(canvasEvents.ScrollPadOnMouseDownObservable
                .Where(_ => !Input.GetMouseButtonDown(1))
                .Where(_ => 0 > model.ClosestNotePosition.Value.samples))
            .TakeWhile(_ => !Input.GetMouseButtonUp(0))
            .Select(_ => Input.mousePosition.x)
            .Buffer(2, 1).Where(b => 2 <= b.Count)
            .RepeatSafe()
            .Select(b => (b[0] - b[1])
                / model.CanvasWidth.Value
                * model.CanvasScaleFactor.Value
                * model.Audio.clip.samples)
            .Merge(canvasEvents.MouseScrollWheelObservable // Merge mouse scroll wheel
                .Where(_ => !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                .Select(delta => model.Audio.clip.samples / 100 * -delta))
            .Select(deltaSamples => model.Audio.timeSamples + Mathf.RoundToInt(deltaSamples))
            .Merge(playPositionController.OnValueChangedAsObservable() // Merge slider value change
                .DistinctUntilChanged()
                .Select(x => x * model.Audio.clip.samples * x)
                .Select(x => Mathf.RoundToInt(x)))
            .Select(timeSamples => Mathf.Clamp(timeSamples, 0, model.Audio.clip.samples - 1))
            .Subscribe(timeSamples => model.Audio.timeSamples = timeSamples);

        model.IsDraggingDuringPlay = canvasEvents.ScrollPadOnMouseDownObservable
            .Where(_ => model.IsPlaying.Value)
            .Select(_ => !(model.IsPlaying.Value = false))
            .Merge(this.UpdateAsObservable()
                .Where(_ => model.IsDraggingDuringPlay.Value)
                .Where(_ => Input.GetMouseButtonUp(0))
                .Select(_ => !(model.IsPlaying.Value = true)))
            .ToReactiveProperty();
    }
}
