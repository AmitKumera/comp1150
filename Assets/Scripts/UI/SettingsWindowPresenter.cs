﻿using LitJson;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;

public class SettingsWindowPresenter : MonoBehaviour
{
    [SerializeField]
    GameObject itemPrefab;
    [SerializeField]
    Transform itemContentTransform;

    static string directoryPath = Directory.GetCurrentDirectory() + "/Settings/";
    static string fileName = "settings.json";
    static string filePath = directoryPath + fileName;

    SettingsModel LoadSettings(NotesEditorSettingsModel model)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (!File.Exists(filePath))
        {
            var defaultSettings = Resources.Load("Settings/default") as TextAsset;
            File.WriteAllText(filePath, defaultSettings.text, System.Text.Encoding.UTF8);
        }

        var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        return JsonMapper.ToObject<SettingsModel>(json);
    }

    void SaveSettings(NotesEditorSettingsModel model)
    {
        File.WriteAllText(filePath, model.SerializeSettings(), System.Text.Encoding.UTF8);
    }

    void Awake()
    {
        var model = NotesEditorSettingsModel.Instance;
        model.Apply(LoadSettings(model));


        NotesEditorModel.Instance.MaxBlock.Do(_ => Enumerable.Range(0, itemContentTransform.childCount)
                .Select(i => itemContentTransform.GetChild(i))
                .ToList()
                .ForEach(child => DestroyObject(child.gameObject)))
            .Do(maxNum => {
                if (model.NoteInputKeyCodes.Value.Count < maxNum)
                {
                    model.NoteInputKeyCodes.Value.AddRange(
                        Enumerable.Range(0, maxNum - model.NoteInputKeyCodes.Value.Count)
                            .Select(_ => KeyCode.None));
                }
            })
            .SelectMany(maxNum => Enumerable.Range(0, maxNum))
            .Subscribe(num =>
            {
                var obj = Instantiate(itemPrefab) as GameObject;
                obj.transform.SetParent(itemContentTransform);

                var item = obj.GetComponent<InputNoteKeyCodeSettingsItem>();
                item.SetData(num, num < model.NoteInputKeyCodes.Value.Count ? model.NoteInputKeyCodes.Value[num] : KeyCode.None);
            });


        Observable.Merge(
                 model.RequestForChangeInputNoteKeyCode.Select(_ => 0),
                 NotesEditorModel.Instance.MaxBlock,
                 model.WorkSpaceDirectoryPath.Select(_ => 0))
             .Where(_ => model.IsViewing.Value)
             .DelayFrame(1)
             .Subscribe(_ => SaveSettings(model));
    }
}
