﻿using System;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;


public class NotesEditorPresenter : MonoBehaviour
{
    [SerializeField]
    CanvasScaler canvasScaler;
    [SerializeField]
    RectTransform canvasRect;
    [SerializeField]
    RectTransform verticalLineRect;
    [SerializeField]
    AudioSource audioSource;
    [SerializeField]
    Button playButton;
    [SerializeField]
    Text titleText;
    [SerializeField]
    GLLineRenderer glLineRenderer;
    [SerializeField]
    Slider _scaleSliderTest;
    [SerializeField]
    Slider divisionNumOfOneMeasureSlider;
    [SerializeField]
    InputField BPMInputField;
    [SerializeField]
    InputField beatOffsetInputField;

    Subject<Vector3> ScrollPadOnMouseDownStream = new Subject<Vector3>();
    Subject<Vector3> VerticalLineOnMouseDownStream = new Subject<Vector3>();

    void Awake()
    {
        if (SelectedMusicDataStore.Instance.audioClip == null)
        {
            ObservableWWW.GetWWW("file:///" + Application.persistentDataPath + "/Musics/test.wav").Subscribe(www =>
            {
                SelectedMusicDataStore.Instance.audioClip = www.audioClip;
                Init();
            });

            return;
        }

        Init();
    }

    void Init()
    {
        var model = NotesEditorModel.Instance;
        var unitBeatSamples = new ReactiveProperty<int>();


        // Binds canvas scale factor
        model.CanvasScaleFactor.Value = canvasScaler.referenceResolution.x / Screen.width;
        this.UpdateAsObservable()
            .Select(_ => Screen.width)
            .DistinctUntilChanged()
            .Subscribe(w => model.CanvasScaleFactor.Value = canvasScaler.referenceResolution.x / w);


        // Binds division number of measure
        divisionNumOfOneMeasureSlider.OnValueChangedAsObservable()
            .Select(x => Mathf.FloorToInt(x))
            .Subscribe(x => model.DivisionNumOfOneMeasure.Value = x);


        // Apply music data
        audioSource.clip = SelectedMusicDataStore.Instance.audioClip;
        titleText.text = SelectedMusicDataStore.Instance.fileName ?? "Test";


        // Initialize canvas offset x
        model.CanvasOffsetX.Value = ScreenToCanvasPosition(Vector3.right * Screen.width * 0.05f).x;


        // Canvas width scaler Test
        model.CanvasWidth = this.UpdateAsObservable()
            .Select(_ => Input.GetAxis("Mouse ScrollWheel"))
            .Where(delta => delta != 0)
            .Select(delta => model.CanvasWidth.Value * (1 - delta))
            .Select(x => x / (audioSource.clip.samples / 100f))
            .Select(x => Mathf.Clamp(x, 0.1f, 2f))
            .Merge(_scaleSliderTest.OnValueChangedAsObservable()
            .DistinctUntilChanged())
            .Select(x => audioSource.clip.samples / 100f * x)
            .ToReactiveProperty();

        model.CanvasWidth.DistinctUntilChanged()
            .Do(x => _scaleSliderTest.value = x / (audioSource.clip.samples / 100f))
            .Subscribe(x => {
                var delta = canvasRect.sizeDelta;
                delta.x = x;
                canvasRect.sizeDelta = delta;
            });


        // Binds BPM
        unitBeatSamples = model.BPM.DistinctUntilChanged()
            .Select(x => Mathf.FloorToInt(audioSource.clip.frequency * 60 / x))
            .ToReactiveProperty();

        BPMInputField.OnValueChangeAsObservable()
            .Select(x => string.IsNullOrEmpty(x) ? "1" : x)
            .Select(x => int.Parse(x))
            .Select(x => Mathf.Clamp(x, 1, 320))
            .Subscribe(x => model.BPM.Value = x);

        model.BPM.DistinctUntilChanged()
            .Subscribe(x => BPMInputField.text = x.ToString());


        // Binds beat offset samples
        beatOffsetInputField.OnValueChangeAsObservable()
            .Select(x => string.IsNullOrEmpty(x) ? "0" : x)
            .Select(x => int.Parse(x))
            .Subscribe(x => model.BeatOffsetSamples.Value = x);

        model.BeatOffsetSamples.DistinctUntilChanged()
            .Subscribe(x => beatOffsetInputField.text = x.ToString());


        // Binds canvas position from samples
        this.UpdateAsObservable()
            .Select(_ => audioSource.timeSamples)
            .DistinctUntilChanged()
            .Merge(model.CanvasWidth.Select(_ => audioSource.timeSamples)) // Merge resized timing
            .Select(timeSamples => timeSamples / (float)audioSource.clip.samples)
            .Select(per => canvasRect.sizeDelta.x * per)
            .Select(x => x + model.CanvasOffsetX.Value)
            .Subscribe(x => canvasRect.localPosition = Vector3.left * x);


        // Binds samples from dragging canvas
        var canvasDragStream = this.UpdateAsObservable()
            .SkipUntil(ScrollPadOnMouseDownStream)
            .TakeWhile(_ => !Input.GetMouseButtonUp(0))
            .Select(_ => Mathf.FloorToInt(Input.mousePosition.x));

        canvasDragStream.Zip(canvasDragStream.Skip(1), (p, c) => new { p, c })
            .RepeatSafe()
            .Select(b => (b.p - b.c) / model.CanvasWidth.Value)
            .Select(p => p * model.CanvasScaleFactor.Value)
            .Select(p => Mathf.FloorToInt(audioSource.clip.samples * p))
            .Select(deltaSamples => audioSource.timeSamples + deltaSamples)
            .Select(timeSamples => Mathf.Clamp(timeSamples, 0, audioSource.clip.samples - 1))
            .Subscribe(timeSamples => audioSource.timeSamples = timeSamples);

        var isDraggingDuringPlay = false;
        ScrollPadOnMouseDownStream.Where(_ => model.IsPlaying.Value)
            .Select(_ => model.IsPlaying.Value = false)
            .Subscribe(_ => isDraggingDuringPlay = true);

        this.UpdateAsObservable().Where(_ => isDraggingDuringPlay)
            .Where(_ => Input.GetMouseButtonUp(0))
            .Select(_ => model.IsPlaying.Value = true)
            .Subscribe(_ => isDraggingDuringPlay = false);


        // Binds offset x of canvas
        var verticalLineDragStream = this.UpdateAsObservable()
            .SkipUntil(VerticalLineOnMouseDownStream)
            .TakeWhile(_ => !Input.GetMouseButtonUp(0))
            .Select(_ => Mathf.FloorToInt(Input.mousePosition.x));

        verticalLineDragStream.Zip(verticalLineDragStream.Skip(1), (p, c) => new { p, c })
            .RepeatSafe()
            .Select(b => (b.c - b.p) * model.CanvasScaleFactor.Value)
            .Select(x => x + model.CanvasOffsetX.Value)
            .Select(x => new { x, max = Screen.width * 0.5f * 0.95f * model.CanvasScaleFactor.Value })
            .Select(v => Mathf.Clamp(v.x, -v.max, v.max))
            .Subscribe(x => model.CanvasOffsetX.Value = x);

        model.CanvasOffsetX.DistinctUntilChanged().Subscribe(x => {
            var pos = verticalLineRect.localPosition;
            pos.x = x;
            verticalLineRect.localPosition = pos;
        });


        // Binds play pause toggle
        playButton.OnClickAsObservable()
            .Subscribe(_ => model.IsPlaying.Value = !model.IsPlaying.Value);

        model.IsPlaying.DistinctUntilChanged().Subscribe(playing => {
            var playButtonText = playButton.GetComponentInChildren<Text>();

            if (playing)
            {
                audioSource.Play();
                playButtonText.text = "Pause";
            }
            else
            {
                audioSource.Pause();
                playButtonText.text = "Play";
            }
        });


        // Render wave
        {
            var waveData = new float[500000];
            var skipSamples = 50;
            var lineColor = Color.green * 0.5f;
            var lines = Enumerable.Range(0, waveData.Length / skipSamples)
                .Select(_ => new Line(Vector3.zero, Vector3.zero, lineColor))
                .ToArray();

            this.UpdateAsObservable()
                .Subscribe(_ =>
                {
                    audioSource.clip.GetData(waveData, audioSource.timeSamples);
                    var x = (model.CanvasWidth.Value / audioSource.clip.samples) / 2f;
                    var offsetX = model.CanvasOffsetX.Value;

                    for (int li = 0, wi = 0, l = waveData.Length; wi < l; li++, wi += skipSamples)
                    {
                        lines[li].start.x = lines[li].end.x = wi * x + offsetX;
                        lines[li].end.y = -(lines[li].start.y = waveData[wi] * 200);
                    }

                    glLineRenderer.RenderLines("wave", lines);
                });
        }


        // Render beat lines
        this.UpdateAsObservable()
            .Select(_ => model.DivisionNumOfOneMeasure.Value * Mathf.CeilToInt(audioSource.clip.samples / (float)unitBeatSamples.Value))
            .Subscribe(max =>
            {
                var beatSamples = Enumerable.Range(0, max)
                    .Select(i => i * unitBeatSamples.Value / model.DivisionNumOfOneMeasure.Value)
                    .Select(i => i + model.BeatOffsetSamples.Value)
                    .ToArray();

                var beatLines = beatSamples
                    .Select(i => i / (float)audioSource.clip.samples)
                    .Select(per => per * model.CanvasWidth.Value)
                    .Select(x => x - model.CanvasWidth.Value * (audioSource.timeSamples / (float)audioSource.clip.samples))
                    .Select(x => x + model.CanvasOffsetX.Value)
                    .Select((x, i) => new Line(
                        new Vector3(x, 200, 0),
                        new Vector3(x, -200, 0),
                        i % model.DivisionNumOfOneMeasure.Value == 0 ? Color.white : Color.white / 2))
                    .ToArray();


                var highlightColor = Color.yellow * 0.8f;

                // Highlight closest line to mouse pointer
                var mouoseX = ScreenToCanvasPosition(Input.mousePosition).x;
                var closestLineIndex = GetClosestLineIndex(beatLines, c => Mathf.Abs(c.start.x - mouoseX));
                var closestLine = beatLines[closestLineIndex];
                closestLine.color = highlightColor;

                glLineRenderer.RenderLines("measures", beatLines);


                var blockLines = Enumerable.Range(0, 5)
                    .Select(i => i * 70 - 140)
                    .Select(i => i + Screen.height * 0.5f)
                    .Select((y, i) => new Line(
                        ScreenToCanvasPosition(new Vector3(0, y, 0)),
                        ScreenToCanvasPosition(new Vector3(Screen.width, y, 0)),
                        Color.white / 2f))
                    .ToArray();

                var mouseY = ScreenToCanvasPosition(Input.mousePosition).y;
                var closestBlockLindex = GetClosestLineIndex(blockLines, c => Mathf.Abs(c.start.y - mouseY));
                closestLine = blockLines[closestBlockLindex];
                closestLine.color = highlightColor;

                glLineRenderer.RenderLines("blocks", blockLines);

                // Debug.Log("Closest measure samples: " + beatSamples[closestLineIndex] + " - " + blockLines[closestBlockLindex]);
            });
    }

    public void ScrollPadOnMouseDown()
    {
        ScrollPadOnMouseDownStream.OnNext(Input.mousePosition);
    }

    public void VerticalLineOnMouseDown()
    {
        VerticalLineOnMouseDownStream.OnNext(Input.mousePosition);
    }

    int GetClosestLineIndex(Line[] lines, Func<Line, float> calcDistance)
    {
        var minValue = lines.Min(calcDistance);
        return Array.FindIndex(lines, c => calcDistance(c) == minValue);
    }

    Vector3 ScreenToCanvasPosition(Vector3 screenPosition)
    {
        return (screenPosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0)) * NotesEditorModel.Instance.CanvasScaleFactor.Value;
    }
}
