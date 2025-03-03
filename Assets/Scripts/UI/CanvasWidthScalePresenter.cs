﻿using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class CanvasWidthScalePresenter : MonoBehaviour
{
    [SerializeField]
    CanvasEvents canvasEvents;
    [SerializeField]
    Slider canvasWidthScaleController;

    NotesEditorModel model;

    void Awake()
    {
        model = NotesEditorModel.Instance;
        model.OnLoadMusicObservable.First().Subscribe(_ => Init());
    }

    void Init()
    {
        var operateCanvasScaleObservable = canvasEvents.MouseScrollWheelObservable
            .Where(_ => KeyInput.CtrlKey())
            .Merge(this.UpdateAsObservable().Where(_ => Input.GetKey(KeyCode.UpArrow)).Select(_ => 0.05f))
            .Merge(this.UpdateAsObservable().Where(_ => Input.GetKey(KeyCode.DownArrow)).Select(_ => -0.05f))
            .Select(delta => model.CanvasWidth.Value * (1 + delta))
            .Select(x => x / (model.Audio.clip.samples / 100f))
            .Select(x => Mathf.Clamp(x, 0.1f, 2f))
            .Merge(canvasWidthScaleController.OnValueChangedAsObservable()
                .DistinctUntilChanged())
            .DistinctUntilChanged()
            .Select(x => model.Audio.clip.samples / 100f * x);

        operateCanvasScaleObservable.Subscribe(x => model.CanvasWidth.Value = x);

        operateCanvasScaleObservable.Buffer(operateCanvasScaleObservable.ThrottleFrame(2))
            .Where(b => 2 <= b.Count)
            .Select(x => new { current = x[x.Count - 1], prev = x[0] })
            .Subscribe(x => UndoRedoManager.Do(
                new Command(
                    () => model.CanvasWidth.Value = x.current,
                    () => model.CanvasWidth.Value = x.prev)));
    }
}
