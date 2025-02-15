﻿using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class UndoRedoManager : SingletonGameObject<UndoRedoManager>
{
    Stack<Command> undoStack = new Stack<Command>();
    Stack<Command> redoStack = new Stack<Command>();

    void Awake()
    {
        var model = NotesEditorModel.Instance;
        model.OnLoadedMusicObservable
            .DelayFrame(1)
            .Subscribe(_ =>
            {
                undoStack.Clear();
                redoStack.Clear();
            });

        this.UpdateAsObservable()
            .Where(_ => KeyInput.CtrlPlus(KeyCode.Z))
            .Subscribe(_ => Undo());

        this.UpdateAsObservable()
            .Where(_ => KeyInput.CtrlPlus(KeyCode.Y))
            .Subscribe(_ => Redo());
    }

    static public void Do(Command command)
    {
        command.Do();
        Instance.undoStack.Push(command);
        Instance.redoStack.Clear();
    }

    static public void Undo()
    {
        if (Instance.undoStack.Count == 0)
            return;

        var command = Instance.undoStack.Pop();
        command.Undo();
        Instance.redoStack.Push(command);
    }

    static public void Redo()
    {
        if (Instance.redoStack.Count == 0)
            return;

        var command = Instance.redoStack.Pop();
        command.Redo();
        Instance.undoStack.Push(command);
    }
}

public class Command
{
    Action doAction;
    Action redoAction;
    Action undoAction;

    public Command(Action doAction, Action undoAction, Action redoAction)
    {
        this.doAction = doAction;
        this.undoAction = undoAction;
        this.redoAction = redoAction;
    }

    public Command(Action doAction, Action undoAction)
    {
        this.doAction = doAction;
        this.undoAction = undoAction;
        this.redoAction = doAction;
    }

    public void Do() { doAction(); }
    public void Undo() { undoAction(); }
    public void Redo() { redoAction(); }
}
