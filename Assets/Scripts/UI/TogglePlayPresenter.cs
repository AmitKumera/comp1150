﻿using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class TogglePlayPresenter : MonoBehaviour
{
    [SerializeField]
    Button togglePlayButton;
    [SerializeField]
    Sprite iconPlay;
    [SerializeField]
    Sprite iconPause;

    NotesEditorModel model;

    void Awake()
    {
        model = NotesEditorModel.Instance;
        model.OnLoadedMusicObservable.First().Subscribe(_ => Init());
    }

    void Init()
    {
        this.UpdateAsObservable()
            .Where(_ => Input.GetKeyDown(KeyCode.Space))
            .Merge(togglePlayButton.OnClickAsObservable())
            .Subscribe(_ => model.IsPlaying.Value = !model.IsPlaying.Value);

        model.IsPlaying.DistinctUntilChanged().Subscribe(playing =>
        {
            var playButtonImage = togglePlayButton.GetComponent<Image>();

            if (playing)
            {
                model.Audio.Play();
                playButtonImage.sprite = iconPause;

            }
            else
            {
                model.Audio.Pause();
                playButtonImage.sprite = iconPlay;
            }
        });
    }
}
