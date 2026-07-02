using System;
using System.Collections.Generic;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>Una acción que puede deshacerse y rehacerse.</summary>
public interface IUndoableAction
{
    void Redo();
    void Undo();
}

/// <summary>Acción de deshacer/rehacer basada en delegados.</summary>
public sealed class DelegateAction : IUndoableAction
{
    private readonly Action _redo;
    private readonly Action _undo;

    public DelegateAction(Action redo, Action undo)
    {
        _redo = redo;
        _undo = undo;
    }

    public void Redo() => _redo();
    public void Undo() => _undo();
}

/// <summary>
/// Pila simple de deshacer/rehacer. Notifica <see cref="Changed"/> cuando cambia
/// el estado, para que la UI actualice el habilitado de los comandos.
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();

    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Ejecuta la acción (Redo) y la registra en la pila de deshacer.</summary>
    public void Do(IUndoableAction action)
    {
        action.Redo();
        Push(action);
    }

    /// <summary>Registra una acción ya aplicada (sin ejecutarla de nuevo).</summary>
    public void Push(IUndoableAction action)
    {
        _undo.Push(action);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var action = _undo.Pop();
        action.Undo();
        _redo.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var action = _redo.Pop();
        action.Redo();
        _undo.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
