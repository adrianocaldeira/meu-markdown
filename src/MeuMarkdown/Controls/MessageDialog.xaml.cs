using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MeuMarkdown.Controls;

public enum MessageDialogKind
{
    Info,
    Warning,
    Error,
    Question,
}

public enum MessageDialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
}

public enum MessageDialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No,
}

/// <summary>
/// Dialog modal estilo MessageBox respeitando o tema do app (DynamicResource).
/// Substitui System.Windows.MessageBox.Show em todos os pontos do app que querem
/// alerta/confirmação dentro da paleta light/dark.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialogResult Result { get; private set; } = MessageDialogResult.None;

    // Ícones Lucide-style (path data) — fundo do círculo + traço do ícone.
    // Cores escolhidas pra contraste razoável tanto em light quanto dark.
    private static readonly Geometry IconInfo = Geometry.Parse(
        "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20 M12 8h.01 M11 12h1v5h1");
    private static readonly Geometry IconWarning = Geometry.Parse(
        "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01");
    private static readonly Geometry IconError = Geometry.Parse(
        "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20 M15 9l-6 6 M9 9l6 6");
    private static readonly Geometry IconQuestion = Geometry.Parse(
        "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20 M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3 M12 17h.01");

    public MessageDialog(string title, string message, MessageDialogKind kind, MessageDialogButtons buttons)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        ApplyKindStyling(kind);
        BuildButtons(buttons);
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // Tratar X como cancelamento (mesmo que botão Cancelar/Não)
        Result = MessageDialogResult.Cancel;
        DialogResult = false;
        Close();
    }

    private void ApplyKindStyling(MessageDialogKind kind)
    {
        // Cores por kind. Ícone com stroke colorido + fundo do círculo com a mesma cor em
        // baixa opacidade (10%) — funciona bem em ambos os temas.
        Color tint;
        Geometry geom;
        switch (kind)
        {
            case MessageDialogKind.Warning:
                tint = Color.FromRgb(0xf5, 0xa6, 0x23); // amber
                geom = IconWarning;
                break;
            case MessageDialogKind.Error:
                tint = Color.FromRgb(0xe5, 0x48, 0x4f); // red
                geom = IconError;
                break;
            case MessageDialogKind.Question:
                // Question usa o accent warm do app pra ficar coeso
                tint = Color.FromRgb(0xe8, 0xa8, 0x7c);
                geom = IconQuestion;
                break;
            default:
                tint = Color.FromRgb(0x58, 0xa6, 0xff); // blue
                geom = IconInfo;
                break;
        }

        var stroke = new SolidColorBrush(tint);
        var bgTint = new SolidColorBrush(Color.FromArgb(0x22, tint.R, tint.G, tint.B));
        IconPath.Stroke = stroke;
        IconPath.Data = geom;
        IconBox.Background = bgTint;
    }

    private void BuildButtons(MessageDialogButtons buttons)
    {
        switch (buttons)
        {
            case MessageDialogButtons.Ok:
                AddButton("OK", MessageDialogResult.Ok, primary: true, isDefault: true, isCancel: true);
                break;
            case MessageDialogButtons.OkCancel:
                AddButton("Cancelar", MessageDialogResult.Cancel, primary: false, isDefault: false, isCancel: true);
                AddButton("OK", MessageDialogResult.Ok, primary: true, isDefault: true, isCancel: false);
                break;
            case MessageDialogButtons.YesNo:
                AddButton("Não", MessageDialogResult.No, primary: false, isDefault: false, isCancel: true);
                AddButton("Sim", MessageDialogResult.Yes, primary: true, isDefault: true, isCancel: false);
                break;
            case MessageDialogButtons.YesNoCancel:
                AddButton("Cancelar", MessageDialogResult.Cancel, primary: false, isDefault: false, isCancel: true);
                AddButton("Não", MessageDialogResult.No, primary: false, isDefault: false, isCancel: false);
                AddButton("Sim", MessageDialogResult.Yes, primary: true, isDefault: true, isCancel: false);
                break;
        }
    }

    private void AddButton(string text, MessageDialogResult result, bool primary, bool isDefault, bool isCancel)
    {
        var styleKey = primary ? "DialogPrimaryButton" : "DialogSecondaryButton";
        var btn = new Button
        {
            Content = text,
            Style = (Style)FindResource(styleKey),
            IsDefault = isDefault,
            IsCancel = isCancel,
            Margin = new Thickness(ButtonRow.Children.Count == 0 ? 0 : 10, 0, 0, 0),
        };
        btn.Click += (_, _) =>
        {
            Result = result;
            DialogResult = true;
        };
        ButtonRow.Children.Add(btn);
    }

    // ─────── Helpers static ───────

    public static bool Confirm(Window? owner, string title, string message, MessageDialogKind kind = MessageDialogKind.Question)
    {
        var dlg = new MessageDialog(title, message, kind, MessageDialogButtons.YesNo);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Result == MessageDialogResult.Yes;
    }

    public static void Error(Window? owner, string title, string message)
    {
        var dlg = new MessageDialog(title, message, MessageDialogKind.Error, MessageDialogButtons.Ok);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
    }

    public static void Info(Window? owner, string title, string message)
    {
        var dlg = new MessageDialog(title, message, MessageDialogKind.Info, MessageDialogButtons.Ok);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
    }

    public static MessageDialogResult YesNoCancel(Window? owner, string title, string message, MessageDialogKind kind = MessageDialogKind.Question)
    {
        var dlg = new MessageDialog(title, message, kind, MessageDialogButtons.YesNoCancel);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Result;
    }
}
