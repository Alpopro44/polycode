using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using PolyCode.UI.ViewModels;

namespace PolyCode.UI.Views;

public partial class MainWindow : Window
{
    private TextEditor? Editor => this.FindControl<TextEditor>("CodeEditor");

    public MainWindow()
    {
        InitializeComponent();

        if (Editor != null)
        {
            Editor.TextChanged += OnEditorTextChanged;
            Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && Editor != null)
        {
            _ = vm.OnCodeEdited(Editor.Text);
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && Editor != null)
        {
            var line = Editor.TextArea.Caret.Line;
            var column = Editor.TextArea.Caret.Column;
            _ = vm.OnCursorMoved(line, column);
        }
    }
}
