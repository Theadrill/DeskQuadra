using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskQuadra.UI.Wpf.Theme;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Diálogo modal escuro padrão do app (visual idêntico ao antigo
/// QuadraWindow.CreateDarkDialog/CreateDialogButton + App.ShowCrashRecoveryDialog).
/// Textos vêm do resx via chamadores; aqui só o visual via TryFindResource
/// (equivalente code-behind de DynamicResource; chaves Dialog.* em Theme/Default.xaml).
/// </summary>
public static class DarkDialog
{
    // Seam de temas (Default): valores via ThemeResolver (TryFindResource + fallback).

    private static Button CreateButton(string text, bool isPrimary)
    {
        return new Button
        {
            Content = text,
            Padding = ThemeResolver.Get("Dialog.Button.Padding", new Thickness(14, 6, 14, 6)),
            Margin = ThemeResolver.Get("Dialog.Button.Margin", new Thickness(6, 0, 0, 0)),
            Cursor = Cursors.Hand,
            FontSize = ThemeResolver.Get("Dialog.Button.FontSize", 12.0),
            Background = isPrimary
                ? ThemeResolver.Get("Dialog.Primary.Background", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)))
                : ThemeResolver.Get("Dialog.Secondary.Background", new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF))),
            Foreground = ThemeResolver.Get("Dialog.Foreground", new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))),
            BorderThickness = ThemeResolver.Get("Dialog.Button.BorderThickness", new Thickness(0))
        };
    }

    private static Window CreateWindow(string title, string message, Window? owner, double width, out StackPanel buttonPanel, bool showInTaskbar = true)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = ThemeResolver.Get("Dialog.Panel.Margin", new Thickness(16)) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = ThemeResolver.Get("Dialog.Foreground", new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))),
            FontSize = ThemeResolver.Get("Dialog.Message.FontSize", 13.0),
            TextWrapping = TextWrapping.Wrap,
            Margin = ThemeResolver.Get("Dialog.Message.Margin", new Thickness(0, 0, 0, 14))
        });

        buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        panel.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = showInTaskbar ? WindowStyle.SingleBorderWindow : WindowStyle.ToolWindow,
            ShowInTaskbar = showInTaskbar,
            Background = ThemeResolver.Get("Dialog.Background", new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24))),
            Content = panel
        };

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog;
    }

    /// <summary>
    /// Diálogo de 2 botões: retorna true só se o primário for clicado (fechar via X = false).
    /// Regra de taskbar: diálogo alcançável pelo botão do dono na taskbar não precisa
    /// de botão próprio (showInTaskbar: false); recovery (sem dono) e donos Quadra
    /// (fora da taskbar) usam o default true.
    /// </summary>
    public static bool Show(
        string title,
        string message,
        string primaryLabel,
        string secondaryLabel,
        Window? owner = null,
        double width = 330,
        bool showInTaskbar = true)
    {
        int index = ShowOptions(title, message, owner, width, showInTaskbar: showInTaskbar, (primaryLabel, true), (secondaryLabel, false));
        return index == 0;
    }

    /// <summary>
    /// Diálogo de N botões na ordem dada: retorna o índice clicado (fechar via X = -1).
    /// Compat: equivale a showInTaskbar: true (SingleBorderWindow + botão próprio).
    /// </summary>
    public static int ShowOptions(
        string title,
        string message,
        Window? owner,
        double width,
        params (string Label, bool IsPrimary)[] buttons)
    {
        return ShowOptions(title, message, owner, width, true, buttons);
    }

    /// <summary>
    /// Diálogo de N botões na ordem dada: retorna o índice clicado (fechar via X = -1).
    /// Regra de taskbar: diálogo alcançável pelo botão do dono na taskbar não precisa
    /// de botão próprio (showInTaskbar: false → ToolWindow, sem taskbar); recovery
    /// (sem dono) e donos Quadra (fora da taskbar) usam o default true
    /// (SingleBorderWindow + ShowInTaskbar true, comportamento atual).
    /// Overload separado (em vez de só trocar a assinatura com params) para não
    /// quebrar os call sites posicionais existentes, que seguem com default true.
    /// </summary>
    public static int ShowOptions(
        string title,
        string message,
        Window? owner,
        double width,
        bool showInTaskbar = true,
        params (string Label, bool IsPrimary)[] buttons)
    {
        int chosen = -1;
        var dialog = CreateWindow(title, message, owner, width, out var panel, showInTaskbar);

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var button = CreateButton(buttons[index].Label, buttons[index].IsPrimary);
            button.Click += (s, e) => { chosen = index; dialog.Close(); };
            panel.Children.Add(button);
        }

        dialog.ShowDialog();
        return chosen;
    }
}
