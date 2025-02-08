﻿using UnityEngine;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;

public class UndoRedoPresenter : MonoBehaviour
{
    Stack<EditorState> operationStack = new Stack<EditorState>();
    Stack<EditorState> undoStack = new Stack<EditorState>();

    bool isUndo = false;

    void Awake()
    {
        var model = NotesEditorModel.Instance;

        model.OnLoadedMusicObservable
            .Do(_ => operationStack.Clear())
            .Do(_ => undoStack.Clear())
            .DelayFrame(1)
            .Subscribe(_ => operationStack.Push(GetState()));

        this.UpdateAsObservable()
            .Where(_ => KeyInput.ShiftPlus(KeyCode.Z))
            .Subscribe(_ => Undo());

        this.UpdateAsObservable()
            .Where(_ => KeyInput.ShiftPlus(KeyCode.Y))
            .Subscribe(_ => Redo());

        Observable.Merge(
                model.LPB.Select(_ => true),
                model.BPM.Select(_ => true),
                model.BeatOffsetSamples.Select(_ => true),
                model.NormalNoteObservable.Select(_ => true),
                model.LongNoteObservable.Select(_ => true),
                model.MaxBlock.Select(_ => true),
                model.TimeSamples.Select(_ => true),
                model.CanvasOffsetX.Select(_ => true),
                model.CanvasWidth.Select(_ => true))
            .SkipUntil(model.OnLoadedMusicObservable)
            .ThrottleFrame(2)
            .Where(_ => isUndo ? (isUndo = false) : true)
            .Do(_ => Debug.Log("PushState"))
            .Subscribe(_ => operationStack.Push(GetState()));
    }

    EditorState GetState()
    {
        var model = NotesEditorModel.Instance;
        var state = new EditorState();
        state.LPB = model.LPB.Value;
        state.BPM = model.BPM.Value;
        state.BeatOffsetSamples = model.BeatOffsetSamples.Value;
        state.NotesData = model.NoteObjects
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToNote());
        state.TimeSamples = model.Audio.timeSamples;
        state.CanvasOffsetX = model.CanvasOffsetX.Value;
        state.CanvasWidth = model.CanvasWidth.Value;
        return state;
    }

    void Undo()
    {
        var currentState = GetState();

        if (operationStack.Count > 0)
            undoStack.Push(operationStack.Peek());

        while (operationStack.Count > 0 && operationStack.Peek().Equals(currentState))
            operationStack.Pop();

        if (operationStack.Count == 0)
            return;

        Debug.Log("Undo");
        var state = operationStack.Pop();

        undoStack.Push(state);
        ApplyDiff(state);
        isUndo = true;
    }

    void Redo()
    {
        var currentState = GetState();

        if (undoStack.Count > 0)
            operationStack.Push(undoStack.Peek());

        while (undoStack.Count > 0 && undoStack.Peek().Equals(currentState))
            undoStack.Pop();

        if (undoStack.Count == 0)
            return;

        Debug.Log("Redo");
        var state = undoStack.Pop();

        operationStack.Push(state);
        ApplyDiff(state);
    }

    void ApplyDiff(EditorState state)
    {
        var model = NotesEditorModel.Instance;

        model.LPB.Value = state.LPB;
        model.BPM.Value = state.BPM;
        model.BeatOffsetSamples.Value = state.BeatOffsetSamples;
        model.Audio.timeSamples = state.TimeSamples;
        model.CanvasOffsetX.Value = state.CanvasOffsetX;
        model.CanvasWidth.Value = state.CanvasWidth;


        var wantDeleteNotes = model.NoteObjects.Values
            .Where(noteObj => !state.NotesData.ContainsKey(noteObj.notePosition))
            .ToList();

        foreach (var note in wantDeleteNotes)
        {
            if (note.noteType.Value == NoteTypes.Long)
            {
                model.LongNoteObservable.OnNext(note.notePosition);
            }
            else
            {
                model.NormalNoteObservable.OnNext(note.notePosition);
            }
        }


        var wantAddNotes = state.NotesData.Values
            .Where(note => !model.NoteObjects.ContainsKey(note.position))
            .ToList();

        foreach (var note in wantAddNotes)
        {
            model.NormalNoteObservable.OnNext(note.position);
        }


        foreach (var note in state.NotesData.Values)
        {
            var instantiatedNote = model.NoteObjects[note.position];
            instantiatedNote.noteType.Value = note.type;

            if (note.type == NoteTypes.Long)
            {
                instantiatedNote.next = model.NoteObjects.ContainsKey(note.next) ? model.NoteObjects[note.next] : null;
                instantiatedNote.prev = model.NoteObjects.ContainsKey(note.prev) ? model.NoteObjects[note.prev] : null;
            }
        }
    }

    class EditorState
    {
        public int BPM = 0;
        public int LPB = 0;
        public int BeatOffsetSamples = 0;
        public Dictionary<NotePosition, Note> NotesData = new Dictionary<NotePosition, Note>();
        public int MaxBlock = 0;
        public int TimeSamples = 0;
        public float CanvasOffsetX = 0;
        public float CanvasWidth = 0;

        public bool Equals(EditorState target)
        {
            if (target == null)
                return false;

            if (target.NotesData.Values.Any(targetNote => !NotesData.ContainsKey(targetNote.position) || !NotesData[targetNote.position].Equals(targetNote)) ||
                NotesData.Values.Any(selfNote => !target.NotesData.ContainsKey(selfNote.position) || !target.NotesData[selfNote.position].Equals(selfNote)))
                return false;

            return BPM == target.BPM &&
                LPB == target.LPB &&
                BeatOffsetSamples == target.BeatOffsetSamples &&
                MaxBlock == target.MaxBlock &&
                TimeSamples == target.TimeSamples &&
                CanvasOffsetX == target.CanvasOffsetX &&
                CanvasWidth == target.CanvasWidth;
        }
    }
}
