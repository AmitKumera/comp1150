﻿using System;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class AuxiliaryLineRendererer : MonoBehaviour
{
    [SerializeField]
    Color highlightColor;
    [SerializeField]
    Color mainBeatLineColor;
    [SerializeField]
    Color subBeatLineColor;
    [SerializeField]
    Color blockLineColor;

    NotesEditorModel model;

    void Awake()
    {
        model = NotesEditorModel.Instance;

        var beatSamples = new int[1];
        var beatLines = new Line[1];
        var blockLines = new Line[1];
        var cachedZeroSamplePosX = -1f;
        var cachedCanvasWidth = 0f;

        this.LateUpdateAsObservable()
            .Where(_ => model.Audio != null && model.Audio.clip != null)
            .Subscribe(_ => {

                var unitBeatSamples = Mathf.FloorToInt(model.Audio.clip.frequency * 60f / model.BPM.Value);
                var beatNum = model.LPB.Value * Mathf.CeilToInt(model.Audio.clip.samples / (float)unitBeatSamples);


                if (beatSamples.Length != beatNum || cachedCanvasWidth != model.CanvasWidth.Value)
                {
                    beatSamples = Enumerable.Range(0, beatNum)
                        .Select(i => i * unitBeatSamples / model.LPB.Value)
                        .ToArray();

                    beatLines = beatSamples
                        .Select(x => model.SamplesToCanvasPositionX(x))
                        .Select((x, i) => new Line(
                            model.CanvasToScreenPosition(new Vector3(x, 140, 0)),
                            model.CanvasToScreenPosition(new Vector3(x, -140, 0)),
                            i % model.LPB.Value == 0 ? mainBeatLineColor : subBeatLineColor))
                        .ToArray();

                    cachedZeroSamplePosX = beatLines[0].start.x;
                    cachedCanvasWidth = model.CanvasWidth.Value;
                }
                else
                {
                    float currentX = model.CanvasToScreenPosition(Vector3.right * model.SamplesToCanvasPositionX(0)).x;
                    float diffX = currentX - cachedZeroSamplePosX;

                    for (int i = 0; i < beatNum; i++)
                    {
                        beatLines[i].end.x = (beatLines[i].start.x += diffX);
                        beatLines[i].color = i % model.LPB.Value == 0 ? mainBeatLineColor : subBeatLineColor;
                    }

                    cachedZeroSamplePosX = currentX;
                }


                if (blockLines.Length != model.MaxBlock.Value)
                {
                    blockLines = Enumerable.Range(0, model.MaxBlock.Value)
                        .Select(i => model.BlockNumToCanvasPositionY(i))
                        .Select(i => i + Screen.height * 0.5f)
                        .Select((y, i) => new Line(
                            new Vector3(0, y, 0),
                            new Vector3(Screen.width, y, 0),
                            blockLineColor))
                        .ToArray();
                }
                else
                {
                    for (int i = 0; i < model.MaxBlock.Value; i++)
                    {
                        blockLines[i].color = blockLineColor;
                    }
                }


                // Highlighting closest line to mouse pointer
                if (model.IsMouseOverNotesRegion.Value)
                {
                    var mouseX = Input.mousePosition.x;
                    var closestLineIndex = GetClosestLineIndex(beatLines, c => Mathf.Abs(c.start.x - mouseX));
                    var closestBeatLine = beatLines[closestLineIndex];

                    var mouseY = Input.mousePosition.y;
                    var closestBlockLindex = GetClosestLineIndex(blockLines, c => Mathf.Abs(c.start.y - mouseY));
                    var closestBlockLine = blockLines[closestBlockLindex];

                    var distance = Vector2.Distance(
                        new Vector2(closestBeatLine.start.x, closestBlockLine.start.y),
                        new Vector2(mouseX, mouseY));

                    var threshold = Mathf.Min(
                        Mathf.Abs(model.BlockNumToCanvasPositionY(0) - model.BlockNumToCanvasPositionY(1)),
                        Mathf.Abs(model.SamplesToCanvasPositionX(beatSamples[0]) - model.SamplesToCanvasPositionX(beatSamples[1]))) / 3f;

                    if (distance < threshold)
                    {
                        closestBlockLine.color = highlightColor;
                        closestBeatLine.color = highlightColor;
                        model.ClosestNotePosition.Value = new NotePosition(model.LPB.Value, closestLineIndex, closestBlockLindex);
                    }
                    else
                    {
                        model.ClosestNotePosition.Value = NotePosition.None;
                    }
                }

                GLLineRenderer.Render("beats", beatLines);
                GLLineRenderer.Render("blocks", blockLines);

            });
    }

    int GetClosestLineIndex(Line[] lines, Func<Line, float> calcDistance)
    {
        var minValue = lines.Min(calcDistance);
        return Array.FindIndex(lines, c => calcDistance(c) == minValue);
    }
}
