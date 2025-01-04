﻿using System.IO;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class SettingWorkSpacePathPresenter : MonoBehaviour
{
    [SerializeField]
    InputField workSpacePathInputField;
    [SerializeField]
    Text workSpacePathInputFieldText;
    [SerializeField]
    Color defaultTextColor;
    [SerializeField]
    Color invalidStateTextColor;

    void Awake()
    {
        var model = NotesEditorSettingsModel.Instance;

        workSpacePathInputField.OnValueChangeAsObservable()
            .Select(path => Directory.Exists(path))
            .Subscribe(exists => workSpacePathInputFieldText.color = exists ? defaultTextColor : invalidStateTextColor);

        workSpacePathInputField.OnValueChangeAsObservable()
            .Where(path => Directory.Exists(path))
            .Subscribe(path => model.WorkSpaceDirectoryPath.Value = path);

        model.WorkSpaceDirectoryPath.DistinctUntilChanged()
            .Subscribe(path => workSpacePathInputField.text = path);
    }
}
