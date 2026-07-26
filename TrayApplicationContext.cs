using System.Diagnostics;

namespace EasyClipStash;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeys = new();
    private AppConfig _config;
    private string? _lastSavedPath;
    private SettingsForm? _settingsForm;

    private readonly ToolStripMenuItem _saveItem;
    private readonly ToolStripMenuItem _openLastItem;
    private readonly ToolStripMenuItem _openImageFolderItem;
    private readonly ToolStripMenuItem _openTextFolderItem;
    private readonly ToolStripMenuItem _copyMarkdownItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _exitItem;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();
        L.Current = _config.Language;

        _saveItem = new ToolStripMenuItem("", null, (_, _) => SaveClipboard());
        _saveItem.Font = new Font(_saveItem.Font, FontStyle.Bold);

        _openLastItem = new ToolStripMenuItem("", null, (_, _) => OpenLastFile()) { Enabled = false };
        _openImageFolderItem = new ToolStripMenuItem("", null, (_, _) => OpenFolder(_config.ImageSavePath));
        _openTextFolderItem = new ToolStripMenuItem("", null, (_, _) => OpenFolder(_config.EffectiveTextSavePath));

        _copyMarkdownItem = new ToolStripMenuItem("") { CheckOnClick = true, Checked = _config.CopyMarkdownToClipboard };
        _copyMarkdownItem.CheckedChanged += (_, _) =>
        {
            _config.CopyMarkdownToClipboard = _copyMarkdownItem.Checked;
            SaveConfig();
        };

        _startupItem = new ToolStripMenuItem("") { CheckOnClick = true, Checked = StartupManager.IsEnabled() };
        _startupItem.CheckedChanged += (_, _) => StartupManager.SetEnabled(_startupItem.Checked);

        _settingsItem = new ToolStripMenuItem("", null, (_, _) => OpenSettings());
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitThread());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_saveItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_openLastItem);
        menu.Items.Add(_openImageFolderItem);
        menu.Items.Add(_openTextFolderItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_copyMarkdownItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            // 아이콘에 마우스를 올리면 실행 중인 버전이 보인다.
            // (폴더·파일 이름에는 버전을 넣지 않으므로 여기서 확인한다)
            Text = $"EasyClipStash v{Updater.CurrentVersion}",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        _hotkeys.HotkeyPressed += SaveClipboard;
        ApplyConfig();

        Updater.CleanupPreviousUpdate();   // 지난 업데이트가 남긴 .old 파일 정리
        if (_config.CheckUpdateOnStartup)
            _ = CheckUpdateInBackgroundAsync();
    }

    /// <summary>시작 시 조용히 새 버전을 확인하고, 있으면 트레이 알림만 띄운다. 실패는 무시한다.</summary>
    private async Task CheckUpdateInBackgroundAsync()
    {
        try
        {
            if (await Updater.CheckAsync() is { } update)
                Notify("EasyClipStash", L.UpdateAvailable(update.Version), ToolTipIcon.Info, OpenSettings);
        }
        catch
        {
            // 네트워크가 없거나 조회에 실패해도 앱 사용에는 지장이 없다.
        }
    }

    /// <summary>설정값을 UI(메뉴 라벨·언어)와 단축키 등록에 반영한다.</summary>
    private void ApplyConfig()
    {
        L.Current = _config.Language;
        ApplyStrings();
        _copyMarkdownItem.Checked = _config.CopyMarkdownToClipboard;

        if (!_hotkeys.TryRegister(_config.Hotkey, out string error))
            Notify(L.HotkeyErrorTitle, error, ToolTipIcon.Warning);
    }

    /// <summary>현재 언어로 모든 트레이 메뉴 라벨을 다시 설정한다.</summary>
    private void ApplyStrings()
    {
        _saveItem.Text = L.SaveClipboard(_config.Hotkey);
        _openLastItem.Text = L.OpenLastFile;
        _openImageFolderItem.Text = L.OpenImageFolder;
        _openTextFolderItem.Text = L.OpenTextFolder;
        _copyMarkdownItem.Text = L.CopyMarkdownAuto;
        _startupItem.Text = L.RunAtStartup;
        _settingsItem.Text = L.Settings;
        _exitItem.Text = L.Exit;
    }

    private void SaveClipboard()
    {
        try
        {
            SaveResult? result = ClipboardSaver.Save(_config);
            if (result is not { } saved)
            {
                Notify(L.SaveFailedTitle, L.NothingToSave, ToolTipIcon.Warning);
                return;
            }

            _lastSavedPath = saved.Path;
            _openLastItem.Enabled = true;

            string message = L.SavedTo(Path.GetDirectoryName(saved.Path) ?? "", Path.GetFileName(saved.Path));
            // 마크다운 태그는 이미지 저장 시에만 의미가 있다.
            if (saved.Kind == SavedKind.Image && _config.CopyMarkdownToClipboard)
            {
                Clipboard.SetText(_config.BuildMarkdown(saved.Path));
                message += "\n" + L.MarkdownCopied;
            }
            // 알림을 누르면 저장된 파일이 선택된 채로 탐색기가 열린다.
            Notify("EasyClipStash", message, ToolTipIcon.Info, () => RevealInExplorer(saved.Path));
        }
        catch (Exception ex)
        {
            Notify(L.SaveFailedTitle, ex.Message, ToolTipIcon.Error);
        }
    }

    private void OpenLastFile()
    {
        if (_lastSavedPath is not null && File.Exists(_lastSavedPath))
            Process.Start(new ProcessStartInfo(_lastSavedPath) { UseShellExecute = true });
    }

    private void OpenFolder(string folder)
    {
        if (Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        else
            Notify(L.FolderMissingTitle, L.FolderMissing(folder), ToolTipIcon.Warning);
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        // 설정 창의 단축키 캡처 칸에서 현재 단축키 조합도 누를 수 있도록 잠시 해제한다.
        _hotkeys.Unregister();
        try
        {
            using var form = new SettingsForm(_config);
            _settingsForm = form;
            if (form.ShowDialog() == DialogResult.OK)
            {
                _config = form.Result;
                SaveConfig();
            }
        }
        finally
        {
            _settingsForm = null;
            ApplyConfig(); // 저장이든 취소든 단축키를 복원/재등록한다.
        }
    }

    /// <summary>설정을 저장하고, 실패하면 조용히 넘어가지 않고 알린다.</summary>
    private void SaveConfig()
    {
        if (!_config.Save())
            Notify(L.SettingsSaveFailedTitle, L.SettingsSaveFailed(AppConfig.ConfigPath), ToolTipIcon.Warning);
    }

    /// <summary>
    /// 알림을 띄운다. onClick을 주면 사용자가 알림을 눌렀을 때 그 동작이 실행된다.
    ///
    /// 윈도우 토스트 대신 자체 창을 쓴다. 토스트는 시스템 전체에서 하나씩만 표시되어
    /// 캡처 도구 알림 뒤에 몇 초씩 밀리는데, 이 앱은 캡처 직후 저장이 주 흐름이라 매번 걸린다.
    /// </summary>
    private void Notify(string title, string message, ToolTipIcon icon, Action? onClick = null)
    {
        var kind = icon switch
        {
            ToolTipIcon.Error => ToastKind.Error,
            ToolTipIcon.Warning => ToastKind.Warning,
            _ => ToastKind.Info,
        };
        ToastWindow.Show(title, message, kind, _config.NotificationPosition, 3000, onClick);
    }

    /// <summary>탐색기에서 해당 파일을 선택한 채로 폴더를 연다. 파일이 없으면 폴더만 연다.</summary>
    private static void RevealInExplorer(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return;
        }

        string? folder = Path.GetDirectoryName(path);
        if (folder is not null && Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        ToastWindow.CloseCurrent();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeys.Dispose();
        base.ExitThreadCore();
    }

    /// <summary>어셈블리에 포함된 앱 아이콘. exe 아이콘과 동일한 파일을 쓴다.</summary>
    private const string IconResourceName = "EasyClipStash.assets.icon.ico";

    /// <summary>
    /// 트레이용 아이콘을 불러온다. 현재 DPI에 맞는 작은 크기 프레임을 .ico에서 골라 쓰므로
    /// 고DPI에서도 흐려지지 않는다.
    /// </summary>
    public static Icon LoadAppIcon()
    {
        using var stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream(IconResourceName);
        if (stream is null)
            return (Icon)SystemIcons.Application.Clone();   // 리소스 누락 시에도 앱은 계속 뜬다
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
