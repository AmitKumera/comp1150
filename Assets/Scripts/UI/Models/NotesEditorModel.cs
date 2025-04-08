﻿using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public enum NoteTypes { Normal, Long }

public class NotesEditorModel : SingletonGameObject<NotesEditorModel>
{
    public readonly ReactiveProperty<NoteTypes> EditType = new ReactiveProperty<NoteTypes>(NoteTypes.Normal);
    public readonly ReactiveProperty<string> MusicName = new ReactiveProperty<string>();
    public readonly ReactiveProperty<int> MaxBlock = new ReactiveProperty<int>(5);
    public readonly ReactiveProperty<int> LPB = new ReactiveProperty<int>(4);
    public readonly ReactiveProperty<int> BPM = new ReactiveProperty<int>(0);
    public readonly ReactiveProperty<int> BeatOffsetSamples = new ReactiveProperty<int>(0);
    public readonly ReactiveProperty<float> Volume = new ReactiveProperty<float>(1);
    public readonly ReactiveProperty<bool> IsPlaying = new ReactiveProperty<bool>(false);
    public readonly ReactiveProperty<int> TimeSamples = new ReactiveProperty<int>();
    public readonly ReactiveProperty<float> CanvasOffsetX = new ReactiveProperty<float>();
    public readonly ReactiveProperty<float> CanvasScaleFactor = new ReactiveProperty<float>();
    public readonly ReactiveProperty<float> CanvasWidth = new ReactiveProperty<float>();
    public readonly ReactiveProperty<bool> IsMouseOverNotesRegion = new ReactiveProperty<bool>();
    public readonly ReactiveProperty<bool> IsMouseOverWaveformRegion = new ReactiveProperty<bool>();
    public readonly ReactiveProperty<bool> IsOperatingPlaybackPositionDuringPlay = new ReactiveProperty<bool>(false);
    public readonly ReactiveProperty<NotePosition> ClosestNotePosition = new ReactiveProperty<NotePosition>();
    public readonly ReactiveProperty<bool> WaveformDisplayEnabled = new ReactiveProperty<bool>(true);
    public readonly ReactiveProperty<bool> PlaySoundEffectEnabled = new ReactiveProperty<bool>(true);
    public readonly Dictionary<NotePosition, NoteObject> NoteObjects = new Dictionary<NotePosition, NoteObject>();
    public readonly ReactiveProperty<NotePosition> LongNoteTailPosition = new ReactiveProperty<NotePosition>();
    public readonly Subject<int> OnLoadMusicObservable = new Subject<int>();

    [HideInInspector]
    public AudioSource Audio;

    [SerializeField]
    CanvasScaler canvasScaler;

    void Awake()
    {
        Audio = gameObject.AddComponent<AudioSource>();

        this.ObserveEveryValueChanged(_ => Screen.width)
            .DistinctUntilChanged()
            .Subscribe(w => CanvasScaleFactor.Value = 1280f / w);
            // .Subscribe(w => CanvasScaleFactor.Value = canvasScaler.referenceResolution.x / w);
        
        ClearNotesData();
    }

    public void ClearNotesData()
    {
        BPM.Value = 120;
        BeatOffsetSamples.Value = 0;
        MusicName.Value = "Notes Editor";
        MaxBlock.Value = NotesEditorSettingsModel.Instance.MaxBlock;
        LPB.Value = 4;
        IsPlaying.Value = false;
        TimeSamples.Value = 0;
        EditType.Value = NoteTypes.Normal;
        LongNoteTailPosition.Value = NotePosition.None;

        foreach (var noteObject in NoteObjects.Values)
        {
            DestroyObject(noteObject.gameObject);
        }

        NoteObjects.Clear();
    }

    public int CanvasPositionXToSamples(float x)
    {
        var per = (x - SamplesToCanvasPositionX(0)) / CanvasWidth.Value;
        return Mathf.RoundToInt(Audio.clip.samples * per);
    }

    public float SamplesToCanvasPositionX(int samples)
    {
        return (samples - Audio.timeSamples + BeatOffsetSamples.Value)
            * CanvasWidth.Value / Audio.clip.samples
            + CanvasOffsetX.Value;
    }

    public float BlockNumToCanvasPositionY(int blockNum)
    {
        var height = 240f;
        var maxIndex = MaxBlock.Value - 1;
        return ((maxIndex - blockNum) * height / maxIndex - height / 2) / CanvasScaleFactor.Value;
    }

    public Vector3 NoteToCanvasPosition(NotePosition notePosition)
    {
        return new Vector3(
            SamplesToCanvasPositionX(notePosition.ToSamples(Audio.clip.frequency, BPM.Value)),
            BlockNumToCanvasPositionY(notePosition.block) * CanvasScaleFactor.Value,
            0);
    }

    public Vector3 ScreenToCanvasPosition(Vector3 screenPosition)
    {
        return (screenPosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0)) * CanvasScaleFactor.Value;
    }

    public string SerializeNotesData()
    {
        var data = new SaveDataModel.NotesData();
        data.BPM = BPM.Value;
        data.maxBlock = MaxBlock.Value;
        data.offset = BeatOffsetSamples.Value;
        data.name = Path.GetFileNameWithoutExtension(MusicName.Value);

        var sortedNoteObjects = NoteObjects.Values
            .Where(note => !(note.note.type == NoteTypes.Long && NoteObjects.ContainsKey(note.note.prev)))
            .OrderBy(note => note.note.position.ToSamples(Audio.clip.frequency, BPM.Value));

        data.notes = new List<SaveDataModel.Note>();

        foreach (var noteObject in sortedNoteObjects)
        {
            if (noteObject.note.type == NoteTypes.Normal)
            {
                data.notes.Add(ConvertToNote(noteObject));
            }
            else if (noteObject.note.type == NoteTypes.Long)
            {
                var current = noteObject;
                var note = ConvertToNote(noteObject);

                while (NoteObjects.ContainsKey(current.note.next))
                {
                    var nextObj = NoteObjects[current.note.next];
                    note.notes.Add(ConvertToNote(nextObj));
                    current = nextObj;
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

    public SaveDataModel.Note ConvertToNote(NoteObject noteObject)
    {
        var note = new SaveDataModel.Note();
        note.num = noteObject.note.position.num;
        note.block = noteObject.note.position.block;
        note.LPB = noteObject.note.position.LPB;
        note.type = noteObject.note.type == NoteTypes.Long ? 2 : 1;
        note.notes = new List<SaveDataModel.Note>();
        return note;
    }
}
