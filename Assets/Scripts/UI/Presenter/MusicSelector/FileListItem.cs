﻿using NoteEditor.UI.Model;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.UI.Presenter
{
    public class FileListItem : MonoBehaviour
    {
        [SerializeField]
        Color selectedStateBackgroundColor;
        [SerializeField]
        Color defaultBackgroundColor;
        [SerializeField]
        Color selectedTextColor;
        [SerializeField]
        Color defaultTextColor;

        string fileName;
        MusicSelector model;

        void Start()
        {
            GetComponent<RectTransform>().localScale = Vector3.one;
            model = MusicSelector.Instance;

            var text = GetComponentInChildren<Text>();
            var image = GetComponent<Image>();

            this.UpdateAsObservable()
                .Select(_ => fileName == model.SelectedFileName.Value)
                .DistinctUntilChanged()
                .Do(selected => image.color = selected ? selectedStateBackgroundColor : defaultBackgroundColor)
                .Subscribe(selected => text.color = selected ? selectedTextColor : defaultTextColor)
                .AddTo(this);
        }

        public void SetName(string name)
        {
            fileName = name;
            GetComponentInChildren<Text>().text = name;
        }

        public void OnMouseDown()
        {
            model.SelectedFileName.Value = fileName;
        }
    }
}
