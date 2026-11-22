using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using SnowDesk.Models;
using SnowDesk.UI;
using Application = System.Windows.Application;

namespace SnowDesk;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private OverlayWindow? _overlay;
    private WardrobeWindow? _wardrobe;
    private AppSettings _settings = new();
    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SnowDesk";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _settings = AppSettings.Load();
        _overlay = new OverlayWindow(_settings);
        _overlay.Show();
        SetupSystemTray();
    }

    private void SetupSystemTray()
    {
        var menu = new ContextMenuStrip();

        ToolStripMenuItem MakeToggle(string label, bool initial, Action<bool> onChanged)
        {
            var item = new ToolStripMenuItem(label) { Checked = initial };
            item.Click += (_, _) => { item.Checked = !item.Checked; onChanged(item.Checked); _settings.Save(); };
            return item;
        }

        menu.Items.Add(MakeToggle("Sıcak Peri Işıkları", _settings.ShowStringLights, v => _settings.ShowStringLights = v));
        menu.Items.Add(MakeToggle("Yılbaşı Ağaçları", _settings.ShowTrees, v => _settings.ShowTrees = v));
        menu.Items.Add(MakeToggle("Büyüyen Buz Sarkıtları", _settings.ShowIcicles, v => _settings.ShowIcicles = v));
        menu.Items.Add(MakeToggle("İmleç Parıltı İzi", _settings.ShowCursorTrail, v => _settings.ShowCursorTrail = v));
        menu.Items.Add(MakeToggle("Noel Baba Geçişi", _settings.ShowSanta, v => _settings.ShowSanta = v));
        menu.Items.Add(MakeToggle("Tam Ekran Oyunda Kar Yağsın", !_settings.AutoPauseFullscreen, v => _settings.AutoPauseFullscreen = !v));

        var densityMenu = new ToolStripMenuItem("Kar Yoğunluğu");
        densityMenu.DropDownItems.Add("Hafif (150)", null, (_, _) => { _settings.TargetFlakeCount = 150; _settings.Save(); });
        densityMenu.DropDownItems.Add("Normal (450)", null, (_, _) => { _settings.TargetFlakeCount = 450; _settings.Save(); });
        densityMenu.DropDownItems.Add("Tipi (850)", null, (_, _) => { _settings.TargetFlakeCount = 850; _settings.Save(); });
        menu.Items.Add(densityMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("👔 Kardan Adam Gardırobu...", null, (_, _) => OpenWardrobe());
        menu.Items.Add("🎅 Noel Baba'yı Şimdi Çağır", null, (_, _) => _overlay?.Engine.TriggerSantaFlyby());
        menu.Items.Add("⛄ Kardan Adam Ekle", null, (_, _) => _overlay?.Engine.SpawnSnowmanAtRandom());
        menu.Items.Add("🚜 Kar Küreme Aracıyla Temizle", null, (_, _) => _overlay?.Engine.StartSnowplowClear());
        menu.Items.Add(new ToolStripSeparator());

        var startupToggle = new ToolStripMenuItem("Windows Başlangıcında Çalıştır") { Checked = IsStartupEnabled() };
        startupToggle.Click += (_, _) => { bool next = !IsStartupEnabled(); SetStartup(next); startupToggle.Checked = next; };
        menu.Items.Add(startupToggle);
        menu.Items.Add("Çıkış", null, (_, _) => Shutdown());

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = "SnowDesk - Masaüstü Kar Efekti",
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    private void OpenWardrobe()
    {
        if (_overlay == null) return;
        if (_wardrobe is { IsVisible: true }) { _wardrobe.Activate(); return; }
        _wardrobe = new WardrobeWindow(_overlay.Engine);
        _wardrobe.Show();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    private static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        if (key == null) return;
        if (enable)
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath)) key.SetValue(AppName, $"\"{exePath}\"");
        }
        else key.DeleteValue(AppName, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
        base.OnExit(e);
    }
}
