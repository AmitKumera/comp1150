﻿using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class MusicSelectorPresenter : MonoBehaviour
{
    [SerializeField]
    InputField directoryPathInputField;
    [SerializeField]
    GameObject fileItem;
    [SerializeField]
    GameObject fileItemContainer;
    [SerializeField]
    Transform fileItemContainerTransform;
    [SerializeField]
    Button LoadButton;
    [SerializeField]
    GameObject notesRegion;

    /*
    [SerializeField]
    Text selectedFileNameText;
    */
    [SerializeField]
    GameObject noteObjectPrefab;

    void Start()
    {
        var model = MusicSelectorModel.Instance;

        directoryPathInputField.OnValueChangeAsObservable()
            .Subscribe(path => model.DirectoryPath.Value = path);

        model.DirectoryPath.DistinctUntilChanged()
            .Subscribe(path => directoryPathInputField.text = path);

        model.DirectoryPath.Value = NotesEditorSettingsModel.Instance.WorkSpaceDirectoryPath.Value + "/Musics/";


        if (!Directory.Exists(model.DirectoryPath.Value))
        {
            Directory.CreateDirectory(model.DirectoryPath.Value);
        }


        Observable.Timer(TimeSpan.FromMilliseconds(300), TimeSpan.Zero)
                .Where(_ => Directory.Exists(model.DirectoryPath.Value))
                .Select(_ => new DirectoryInfo(model.DirectoryPath.Value).GetFiles())
                .Select(fileInfo => fileInfo.Select(file => file.FullName).ToList())
                .Where(x => !x.SequenceEqual(model.FilePathList.Value))
                .Subscribe(filePathList => model.FilePathList.Value = filePathList);


        model.FilePathList.AsObservable()
            .Select(filePathList => filePathList.Select(path => Path.GetFileName(path)))
            .Do(_ => Enumerable.Range(0, fileItemContainerTransform.childCount)
                .Select(i => fileItemContainerTransform.GetChild(i))
                .ToList()
                .ForEach(child => DestroyObject(child.gameObject)))
            .SelectMany(fileNameList => fileNameList)
                .Select(fileName => new { fileName, obj = Instantiate(fileItem) as GameObject })
                .Do(elm => elm.obj.transform.SetParent(fileItemContainer.transform))
                .Subscribe(elm => elm.obj.GetComponent<FileItem>().SetName(elm.fileName));


        LoadButton.OnClickAsObservable()
            .Select(_ => model.SelectedFileName.Value)
                .Where(fileName => !string.IsNullOrEmpty(fileName))
                .Subscribe(fileName =>
                {
                    ObservableWWW.GetWWW("file:///" + model.DirectoryPath.Value + fileName).Subscribe(www =>
                    {

                        if (www.audioClip == null)
                        {
                            // selectedFileNameText.text = fileName + " は音楽ファイルじゃない件!!!!!!!!!!!!!";
                            return;
                        }

                        var editorModel = NotesEditorModel.Instance;
                        editorModel.ClearNotesData();

                        // Apply music data
                        editorModel.Audio.clip = www.audioClip;
                        editorModel.MusicName.Value = fileName;

                        editorModel.OnLoadedMusicObservable.OnNext(0);

                        LoadNotesData();
                    });
                });

        // model.SelectedFileName.SubscribeToText(selectedFileNameText);
    }

    void LoadNotesData()
    {
        var editorModel = NotesEditorModel.Instance;

        var fileName = Path.GetFileNameWithoutExtension(editorModel.MusicName.Value) + ".json";
        var directoryPath = Application.persistentDataPath + "/Notes/";
        var filePath = directoryPath + fileName;

        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var notesData = JsonMapper.ToObject<MusicModel.NotesData>(json);
            InstantiateNotesData(notesData);
        }
    }

    void InstantiateNotesData(MusicModel.NotesData notesData)
    {
        var editorModel = NotesEditorModel.Instance;

        editorModel.BPM.Value = notesData.BPM;
        editorModel.BeatOffsetSamples.Value = notesData.offset;

        foreach (var note in notesData.notes)
        {
            if (note.type == 1)
            {
                InstantiateNoteObject(notesData, note);
                continue;
            }

            var longNoteObjects = new[] { note }.Concat(note.notes)
                .Select(note_ => InstantiateNoteObject(notesData, note_))
                .ToList();

            for (int i = 1; i < longNoteObjects.Count; i++)
            {
                longNoteObjects[i].prev = longNoteObjects[i - 1];
                longNoteObjects[i - 1].next = longNoteObjects[i];
            }
        }
    }

    NoteObject InstantiateNoteObject(MusicModel.NotesData notesData, MusicModel.Note note)
    {
        var noteObject = (Instantiate(noteObjectPrefab) as GameObject).GetComponent<NoteObject>();
        noteObject.notePosition = new NotePosition(notesData.BPM, note.LPB, note.num, note.block);
        noteObject.noteType.Value = note.type == 2 ? NoteTypes.Long : NoteTypes.Normal;
        noteObject.transform.SetParent(notesRegion.transform);
        NotesEditorModel.Instance.NoteObjects.Add(noteObject.notePosition, noteObject);
        return noteObject;
    }
}
