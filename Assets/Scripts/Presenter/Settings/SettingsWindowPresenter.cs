﻿using LitJson;
using NoteEditor.Model.JSON;
using NoteEditor.Model;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;

namespace NoteEditor.Presenter
{
    public class SettingsWindowPresenter : MonoBehaviour
    {
        [SerializeField]
        GameObject itemPrefab;
        [SerializeField]
        Transform itemContentTransform;

        static string directoryPath = Directory.GetCurrentDirectory() + "/Settings/";
        static string fileName = "settings.json";
        static string filePath = directoryPath + fileName;

        SettingsDataModel LoadSettings()
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
            return JsonMapper.ToObject<SettingsDataModel>(json);
        }

        void SaveSettings()
        {
            File.WriteAllText(filePath, Settings.SerializeSettings(), System.Text.Encoding.UTF8);
        }

        void Awake()
        {
            Settings.Apply(LoadSettings());

            EditData.MaxBlock.Do(_ => Enumerable.Range(0, itemContentTransform.childCount)
                    .Select(i => itemContentTransform.GetChild(i))
                    .ToList()
                    .ForEach(child => DestroyObject(child.gameObject)))
                .Do(maxNum =>
                {
                    if (Settings.NoteInputKeyCodes.Value.Count < maxNum)
                    {
                        Settings.NoteInputKeyCodes.Value.AddRange(
                            Enumerable.Range(0, maxNum - Settings.NoteInputKeyCodes.Value.Count)
                                .Select(_ => KeyCode.None));
                    }
                })
                .SelectMany(maxNum => Enumerable.Range(0, maxNum))
                .Subscribe(num =>
                {
                    var obj = Instantiate(itemPrefab) as GameObject;
                    obj.transform.SetParent(itemContentTransform);

                    var item = obj.GetComponent<InputNoteKeyCodeSettingsItem>();
                    item.SetData(num, num < Settings.NoteInputKeyCodes.Value.Count ? Settings.NoteInputKeyCodes.Value[num] : KeyCode.None);
                });


            Observable.Merge(
                     Settings.RequestForChangeInputNoteKeyCode.Select(_ => 0),
                     EditData.MaxBlock,
                     Settings.WorkSpaceDirectoryPath.Select(_ => 0))
                 .Where(_ => Settings.IsViewing.Value)
                 .DelayFrame(1)
                 .Subscribe(_ => SaveSettings());
        }
    }
}
