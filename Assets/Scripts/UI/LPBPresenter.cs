﻿using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class LPBPresenter : MonoBehaviour
{
    [SerializeField]
    Text LPBDisplayText;

    Subject<int> ChangeButtonsOnMouseUpObservable = new Subject<int>();
    Subject<int> ChangeButtonsOnMouseDownObservable = new Subject<int>();

    void Awake()
    {
        var model = NotesEditorModel.Instance;

        model.LPB.DistinctUntilChanged().SubscribeToText(LPBDisplayText);

        Observable.Merge(
                ChangeButtonsOnMouseDownObservable,
                ChangeButtonsOnMouseUpObservable)
            .Throttle(TimeSpan.FromMilliseconds(350))
            .Where(delta => delta != 0)
            .SelectMany(delta => Observable.Interval(TimeSpan.FromMilliseconds(50))
                .TakeUntil(ChangeButtonsOnMouseUpObservable)
                .Select(_ => delta))
            .Merge(ChangeButtonsOnMouseDownObservable)
            .Select(delta => model.LPB.Value + delta)
            .Select(LPB => Mathf.Clamp(LPB, 2, 32))
            .DistinctUntilChanged()
            .Select(x => new { current = x, prev = model.LPB.Value })
            .Subscribe(x => UndoRedoManager.Do(
                new Command(
                    () => model.LPB.Value = x.current,
                    () => model.LPB.Value = x.prev)));
    }

    public void IncreaseButtonOnMouseDown() { ChangeButtonsOnMouseDownObservable.OnNext(1); }
    public void IncreaseButtonOnMouseUp() { ChangeButtonsOnMouseUpObservable.OnNext(0); }
    public void DecreaseButtonOnMouseDown() { ChangeButtonsOnMouseDownObservable.OnNext(-1); }
    public void DecreaseButtonOnMouseUp() { ChangeButtonsOnMouseUpObservable.OnNext(0); }
}
