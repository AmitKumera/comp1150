﻿using NoteEditor.Notes;
using NoteEditor.UI.Model;
using NoteEditor.Utility;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace NoteEditor.UI.Presenter
{
    public class InputNotesByKeyboardPresenter : MonoBehaviour
    {
        NoteEditorModel model;
        EditNotesPresenter editPresenter;

        void Awake()
        {
            model = NoteEditorModel.Instance;
            editPresenter = EditNotesPresenter.Instance;
            Audio.OnLoad.First().Subscribe(_ => Init());
        }

        void Init()
        {
            var settingsModel = NoteEditorSettingsModel.Instance;

            this.UpdateAsObservable()
                .Where(_ => !settingsModel.IsViewing.Value)
                .Where(_ => !KeyInput.AltKey())
                .Where(_ => !KeyInput.CtrlKey())
                .Where(_ => !KeyInput.ShiftKey())
                .SelectMany(_ => Observable.Range(0, EditData.MaxBlock.Value))
                .Where(num => Input.GetKeyDown(settingsModel.NoteInputKeyCodes.Value[num]))
                .Subscribe(num => EnterNote(num));
        }

        void EnterNote(int block)
        {
            var offset = -5000;
            var unitBeatSamples = Audio.Source.clip.frequency * 60f / EditData.BPM.Value / EditData.LPB.Value;
            var timeSamples = Audio.Source.timeSamples - EditData.OffsetSamples.Value + (Audio.IsPlaying.Value ? offset : 0);
            var beats = Mathf.RoundToInt(timeSamples / unitBeatSamples);

            editPresenter.RequestForEditNote.OnNext(new Note(new NotePosition(EditData.LPB.Value, beats, block), EditState.NoteType.Value));
        }
    }
}
