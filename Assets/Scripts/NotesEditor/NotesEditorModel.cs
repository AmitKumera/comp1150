﻿using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;

public enum NoteTypes { Normal, Long }

public class NotesEditorModel : SingletonGameObject<NotesEditorModel>
{
    public ReactiveProperty<NoteTypes> EditType = new ReactiveProperty<NoteTypes>(NoteTypes.Normal);
    public ReactiveProperty<string> MusicName = new ReactiveProperty<string>();
    public ReactiveProperty<int> LPB = new ReactiveProperty<int>(4);
    public ReactiveProperty<float> BPM = new ReactiveProperty<float>(0);
    public ReactiveProperty<int> BeatOffsetSamples = new ReactiveProperty<int>(0);
    public ReactiveProperty<float> Volume = new ReactiveProperty<float>(1);
    public ReactiveProperty<bool> IsPlaying = new ReactiveProperty<bool>(false);
    public ReactiveProperty<int> TimeSamples = new ReactiveProperty<int>();
    public ReactiveProperty<float> CanvasOffsetX = new ReactiveProperty<float>();
    public ReactiveProperty<float> CanvasScaleFactor = new ReactiveProperty<float>();
    public ReactiveProperty<float> CanvasWidth = new ReactiveProperty<float>();
    public ReactiveProperty<bool> IsMouseOverCanvas = new ReactiveProperty<bool>();
    public ReactiveProperty<int> UnitBeatSamples = new ReactiveProperty<int>();
    public ReactiveProperty<bool> IsDraggingDuringPlay = new ReactiveProperty<bool>();
    public ReactiveProperty<NotePosition> ClosestNotePosition = new ReactiveProperty<NotePosition>();
    public ReactiveProperty<bool> WaveformDisplayEnabled = new ReactiveProperty<bool>(true);
    public Dictionary<NotePosition, NoteObject> NoteObjects = new Dictionary<NotePosition, NoteObject>();
    public ReactiveProperty<NotePosition> LongNoteTailPosition = new ReactiveProperty<NotePosition>();
    public Subject<NotePosition> NormalNoteObservable = new Subject<NotePosition>();
    public Subject<NotePosition> LongNoteObservable = new Subject<NotePosition>();
    public Subject<SelectedMusicDataStore> OnLoadedMusicObservable = new Subject<SelectedMusicDataStore>();
    public Subject<NoteObject> AddedLongNoteObjectObservable = new Subject<NoteObject>();
    public AudioSource Audio;

    public float SamplesToScreenPositionX(int samples)
    {
        return (samples - Audio.timeSamples + BeatOffsetSamples.Value)
            * CanvasWidth.Value / Audio.clip.samples
            + CanvasOffsetX.Value;
    }

    public float BlockNumToScreenPositionY(int blockNum)
    {
        return (blockNum * 60 - 120) / CanvasScaleFactor.Value;
    }

    public Vector3 ScreenToCanvasPosition(Vector3 screenPosition)
    {
        return (screenPosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0)) * CanvasScaleFactor.Value;
    }

    public string SerializeNotesData()
    {
        var data = new MusicModel.NotesData();
        data.BPM = BPM.Value.ToString();
        data.offset = BeatOffsetSamples.Value;
        data.fileName = Path.GetFileNameWithoutExtension(MusicName.Value);

        var sortedNoteObjects = NoteObjects.Values
            .Where(note => !(note.noteType.Value == NoteTypes.Long && note.prev != null))
            .OrderBy(note => note.notePosition.samples);

        data.notes = new List<MusicModel.Note>();

        foreach (var noteObject in sortedNoteObjects)
        {
            if (noteObject.noteType.Value == NoteTypes.Normal)
            {
                data.notes.Add(ConvertToNote(noteObject));
            }
            else if (noteObject.noteType.Value == NoteTypes.Long)
            {
                var current = noteObject;
                var note = ConvertToNote(noteObject);

                while (current.next != null)
                {
                    note.noteList.Add(ConvertToNote(current.next));
                    current = current.next;
                }

                data.notes.Add(note);
            }
        }

        var jsonWriter = new JsonWriter();
        jsonWriter.PrettyPrint = true;
        jsonWriter.IndentValue = 4;
        JsonMapper.ToJson(data, jsonWriter);
        return jsonWriter.ToString();
    }

    public MusicModel.Note ConvertToNote(NoteObject noteObject)
    {
        var note = new MusicModel.Note();
        note.sample = noteObject.notePosition.samples;
        note.blockNum = noteObject.notePosition.blockNum;
        note.noteList = new List<MusicModel.Note>();
        return note;
    }
}
