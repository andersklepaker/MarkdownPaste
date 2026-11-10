using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using MarkdownPaste.Core;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using Forms = System.Windows.Forms;

namespace MarkdownPaste;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var app = new TrayApp(application);
        app.Start();
        application.Run();
    }
}

internal sealed class TrayApp : IDisposable
{
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModWin = 0x0008, ModNoRepeat = 0x4000;
    private readonly System.Windows.Application application;
    private readonly HwndSource source;
    private readonly Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon trayIcon;
    private readonly Forms.ToolStripMenuItem defaultHotkey;
    private readonly Forms.ToolStripMenuItem alternativeHotkey;
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkdownPaste", "settings.json");
    private Hotkey? registered;
    private bool processing;

    private record Hotkey(string Name, uint Modifiers, uint Key);
    private static readonly Hotkey Default = new("Win + Ctrl + B", ModWin | ModControl, 0x42);
    private static readonly Hotkey Alternative = new("Ctrl + Alt + V", ModControl | ModAlt, 0x56);

    public TrayApp(System.Windows.Application application)
    {
        this.application = application;
        var parameters = new HwndSourceParameters("MarkdownPaste") { ParentWindow = new IntPtr(-3) };
        source = new HwndSource(parameters);
        source.AddHook(WindowProc);

        defaultHotkey = new Forms.ToolStripMenuItem(Default.Name, null, (_, _) => SelectHotkey(Default));
        alternativeHotkey = new Forms.ToolStripMenuItem(Alternative.Name, null, (_, _) => SelectHotkey(Alternative));
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Hurtigtast", null, defaultHotkey, alternativeHotkey));
        menu.Items.Add("Avslutt", null, (_, _) => application.Shutdown());
        using var iconStream = typeof(TrayApp).Assembly.GetManifestResourceStream("MarkdownPaste.app.ico")
            ?? throw new InvalidOperationException("The application icon is missing.");
        using var embeddedIcon = new System.Drawing.Icon(iconStream);
        trayIcon = (System.Drawing.Icon)embeddedIcon.Clone();
        icon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "Markdown Paste",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    public void Start()
    {
        var selected = LoadHotkey();
        if (!TryRegister(selected))
            Notify($"Kunne ikke registrere {selected.Name}. Velg en annen hurtigtast fra ikonet i systemstatusfeltet.");
    }

    private Hotkey LoadHotkey()
    {
        try
        {
            if (File.Exists(settingsPath) && JsonSerializer.Deserialize<string>(File.ReadAllText(settingsPath)) == Alternative.Name)
                return Alternative;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return Default;
    }

    private void SelectHotkey(Hotkey hotkey)
    {
        if (registered == hotkey) return;
        var previous = registered;
        if (previous is not null) Native.UnregisterHotKey(source.Handle, HotkeyId);
        registered = null;
        if (!TryRegister(hotkey))
        {
            if (previous is not null && !TryRegister(previous))
                Notify($"Kunne ikke registrere {hotkey.Name} eller gjenopprette {previous.Name}.");
            else
                Notify($"Kunne ikke registrere {hotkey.Name}. Den kan være reservert eller i bruk.");
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(hotkey.Name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Hurtigtasten er aktiv, men kunne ikke lagres for neste oppstart.");
        }
    }

    private bool TryRegister(Hotkey hotkey)
    {
        if (!Native.RegisterHotKey(source.Handle, HotkeyId, hotkey.Modifiers | ModNoRepeat, hotkey.Key)) return false;
        registered = hotkey;
        defaultHotkey.Checked = hotkey == Default;
        alternativeHotkey.Checked = hotkey == Alternative;
        return true;
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId && !processing)
        {
            handled = true;
            _ = PasteAsync(Native.GetForegroundWindow());
        }
        return IntPtr.Zero;
    }

    private async Task PasteAsync(IntPtr target)
    {
        processing = true;
        try
        {
            if (target == IntPtr.Zero || !Clipboard.ContainsText())
            {
                Notify("Utklippstavlen inneholder ikke tekst, eller ingen vinduer er aktive.");
                return;
            }

            var markdown = Clipboard.GetText();
            var data = new System.Windows.DataObject();
            data.SetData(DataFormats.Html, MarkdownClipboard.ToHtmlClipboard(markdown));
            data.SetData(DataFormats.UnicodeText, markdown);

            // Wait for the physical shortcut keys to be released before synthesizing Ctrl+V.
            for (var i = 0; i < 100 && Native.ShortcutKeysDown(); i++) await Task.Delay(50);
            if (Native.ShortcutKeysDown() || Native.GetForegroundWindow() != target)
            {
                Notify("Innliming avbrutt: hurtigtasten holdes fortsatt inne eller aktivt vindu er endret.");
                return;
            }

            Clipboard.SetDataObject(data, true);
            if (!Native.SendPaste()) Notify("Kunne ikke sende Ctrl+V. Lim inn manuelt med Ctrl+V.");
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            Notify("Kunne ikke lese eller skrive utklippstavlen. Prøv igjen.");
        }
        finally { processing = false; }
    }

    private void Notify(string message)
    {
        icon.ShowBalloonTip(5000, "Markdown Paste", message, Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (registered is not null) Native.UnregisterHotKey(source.Handle, HotkeyId);
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
        trayIcon.Dispose();
        source.Dispose();
    }
}

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    internal static bool ShortcutKeysDown() => new[] { 0x11, 0x12, 0x5B, 0x5C, 0x42, 0x56 }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);

    internal static bool SendPaste()
    {
        var inputs = new[] { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    private static Input Key(ushort key, bool up) => new() { Type = 1, Union = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = up ? 2u : 0u } } };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
}
