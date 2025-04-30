﻿using System.Collections.Generic;
using UnityEngine;

public class GLLineRenderer : SingletonGameObject<GLLineRenderer>
{
    [SerializeField]
    Material mat;
    List<Line> drawData = new List<Line>();

    static int size = 0;
    static int maxSize = 0;

    void OnRenderObject()
    {
        GL.PushMatrix();
        mat.SetPass(0);
        GL.LoadPixelMatrix();
        GL.Begin(GL.LINES);

        if (size * 2 < maxSize)
        {
            drawData.RemoveRange(size - 1, maxSize - size);
            maxSize = size;
        }

        drawData.ForEach(line =>
        {
            GL.Color(line.color);
            GL.Vertex(line.start);
            GL.Vertex(line.end);
        });

        GL.End();
        GL.PopMatrix();
        size = 0;
    }

    public static void Render(Line[] lines)
    {
        foreach (var line in lines)
        {
            Render(line);
        }
    }

    public static void Render(Line line)
    {
        if (size < maxSize)
        {
            Instance.drawData[size] = line;
        }
        else
        {
            Instance.drawData.Add(line);
            maxSize++;
        }

        size++;
    }
}
