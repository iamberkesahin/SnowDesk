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

        var lightsToggle = new ToolStripMenuItem("Sıcak Peri Işıkları") { Checked = _settings.ShowStringLights };
        lightsToggle.Click += (_, _) =>
        {
            _settings.ShowStringLights = !_settings.ShowStringLights;
            lightsToggle.Checked = _settings.ShowStringLights;
            _settings.Save();
        };

        var treeToggle = new ToolStripMenuItem("Yılbaşı Ağaçları") { Checked = _settings.ShowTrees };
        treeToggle.Click += (_, _) =>
        {
            _settings.ShowTrees = !_settings.ShowTrees;
            treeToggle.Checked = _settings.ShowTrees;
            _settings.Save();
        };

        var santaToggle = new ToolStripMenuItem("Noel Baba Geçişi") { Checked = _settings.ShowSanta };
        santaToggle.Click += (_, _) =>
        {
            _settings.ShowSanta = !_settings.ShowSanta;
            santaToggle.Checked = _settings.ShowSanta;
            _settings.Save();
        };

        var gamePauseToggle = new ToolStripMenuItem("Tam Ekran Oyunda Duraklat") { Checked = _settings.AutoPauseFullscreen };
        gamePauseToggle.Click += (_, _) =>
        {
            _settings.AutoPauseFullscreen = !_settings.AutoPauseFullscreen;
            gamePauseToggle.Checked = _settings.AutoPauseFullscreen;
            _settings.Save();
        };

        var densityMenu = new ToolStripMenuItem("Kar Yoğunluğu");
        densityMenu.DropDownItems.Add("Hafif (150)", null, (_, _) => { _settings.TargetFlakeCount = 150; _settings.Save(); });
        densityMenu.DropDownItems.Add("Normal (450)", null, (_, _) => { _settings.TargetFlakeCount = 450; _settings.Save(); });
        densityMenu.DropDownItems.Add("Tipi (850)", null, (_, _) => { _settings.TargetFlakeCount = 850; _settings.Save(); });

        var callSantaItem = new ToolStripMenuItem("Noel Baba'yı Şimdi Çağır", null, (_, _) => _overlay?.Engine.TriggerSantaFlyby());
        var spawnSnowmanItem = new ToolStripMenuItem("Kardan Adam Ekle", null, (_, _) => _overlay?.Engine.SpawnSnowmanAtRandom());
        var clearItem = new ToolStripMenuItem("Biriken Karları Temizle", null, (_, _) => _overlay?.Engine.ClearAllSnow());

        var startupToggle = new ToolStripMenuItem("Windows Başlangıcında Çalıştır") { Checked = IsStartupEnabled() };
        startupToggle.Click += (_, _) =>
        {
            bool next = !IsStartupEnabled();
            SetStartup(next);
            startupToggle.Checked = next;
        };

        var exitItem = new ToolStripMenuItem("Çıkış", null, (_, _) => Shutdown());

        menu.Items.Add(lightsToggle);
        menu.Items.Add(treeToggle);
        menu.Items.Add(santaToggle);
        menu.Items.Add(gamePauseToggle);
        menu.Items.Add(densityMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(callSantaItem);
        menu.Items.Add(spawnSnowmanItem);
        menu.Items.Add(clearItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupToggle);
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "SnowDesk - Masaüstü Kar Efekti",
            Visible = true,
            ContextMenuStrip = menu
        };
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
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }
}