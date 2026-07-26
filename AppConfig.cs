using System.Runtime.InteropServices;
using System.Text.Json;

namespace EasyClipStash;

public class AppConfig
{
    public string ImageSavePath { get; set; } = KnownFolders.Downloads;
    public ImageFormatKind ImageFormat { get; set; } = ImageFormatKind.Png;
    public string Hotkey { get; set; } = "Ctrl+Alt+V";
    public bool CopyMarkdownToClipboard { get; set; } = true;
    public string MarkdownUrlPrefix { get; set; } = "/assets/img";
    public string MarkdownTemplate { get; set; } = "![]({url})";
    public Lang Language { get; set; } = L.Default;
    public bool CheckUpdateOnStartup { get; set; } = true;
    public ToastPosition NotificationPosition { get; set; } = ToastPosition.BottomRight;

    // ── 파일 이름 규칙 (이미지·텍스트 각각 독립) ──
    public NamingConfig ImageNaming { get; set; } = new();
    public NamingConfig TextNaming { get; set; } = new();

    // ── 텍스트 저장 ──
    public string TextSavePath { get; set; } = KnownFolders.Downloads;  // 비우면 이미지와 같은 폴더(ImageSavePath)
    public TextExtension TextExtension { get; set; } = TextExtension.Txt;

    /// <summary>텍스트를 실제로 저장할 폴더. TextSavePath가 비어 있으면 이미지 폴더를 쓴다.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveTextSavePath => string.IsNullOrWhiteSpace(TextSavePath) ? ImageSavePath : TextSavePath;

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// 설정을 불러온다. 어떤 이유로든 실패하면 기본값으로 시작한다.
    /// 설정을 못 읽는 것이 앱을 못 쓰는 이유가 되어서는 안 된다.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    Migrate(loaded, json);
                    return loaded;
                }
            }
        }
        catch (JsonException)
        {
            // 손상된 config.json은 기본값으로 대체하되, 원본은 남겨둔다.
            TryBackupBrokenConfig();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 파일이 잠겼거나 읽을 권한이 없는 경우. 기본값으로 진행한다.
        }

        var config = new AppConfig();
        config.Save();   // 실패해도 예외를 던지지 않는다
        return config;
    }

    /// <summary>
    /// 설정을 저장한다. 성공 여부를 돌려주고 예외를 던지지 않는다.
    ///
    /// 이 앱은 압축을 풀어 아무 폴더에서나 실행하므로 쓰기 권한이 없는 위치일 수 있다.
    /// 그때 설정 저장 실패로 앱이 죽으면 안 된다.
    /// </summary>
    public bool Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static void TryBackupBrokenConfig()
    {
        try { File.Copy(ConfigPath, ConfigPath + ".bak", overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 구버전 config를 현재 형식으로 옮긴다. 옮길 게 있었으면 새 형식으로 다시 저장한다.
    /// 속성 이름이 곧 JSON 키라서, 이름을 바꿀 때마다 여기에 옛 키를 읽는 코드를 남겨야 한다.
    /// </summary>
    private static void Migrate(AppConfig config, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        bool changed = MigrateFlatNaming(config, root) | MigrateSavePath(config, root);

        if (changed)
            config.Save();
    }

    /// <summary>
    /// v1.1 이전: 이름 규칙이 최상위에 평평하게 있었다.
    /// 이미지·텍스트 규칙 양쪽에 같은 값을 넣는다.
    /// </summary>
    private static bool MigrateFlatNaming(AppConfig config, JsonElement root)
    {
        if (root.TryGetProperty(nameof(ImageNaming), out _) || !root.TryGetProperty("NamingMode", out _))
            return false;

        var legacy = JsonSerializer.Deserialize<NamingConfig>(root.GetRawText(), JsonOptions);
        if (legacy is null) return false;

        config.ImageNaming = legacy;
        config.TextNaming = JsonSerializer.Deserialize<NamingConfig>(JsonSerializer.Serialize(legacy, JsonOptions), JsonOptions)!;
        return true;
    }

    /// <summary>
    /// v1.4 이전: 이미지 저장 경로가 "SavePath"였다(텍스트만 접두사가 붙어 비대칭이었다).
    /// 지금은 ImageSavePath다.
    /// </summary>
    private static bool MigrateSavePath(AppConfig config, JsonElement root)
    {
        if (root.TryGetProperty(nameof(ImageSavePath), out _)) return false;
        if (!root.TryGetProperty("SavePath", out var old)) return false;
        if (old.GetString() is not { Length: > 0 } path) return false;

        config.ImageSavePath = path;
        return true;
    }

    /// <summary>저장된 파일 경로에 대응하는 블로그용 마크다운 태그를 만든다.</summary>
    public string BuildMarkdown(string savedFilePath)
    {
        string url = MarkdownUrlPrefix.TrimEnd('/') + "/" + Path.GetFileName(savedFilePath);
        return MarkdownTemplate.Replace("{url}", url);
    }
}

/// <summary>Environment.SpecialFolder에 없는 알려진 폴더 경로 조회.</summary>
internal static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    /// <summary>사용자의 실제 다운로드 폴더. (위치를 옮긴 경우에도 정확) 조회 실패 시 프로필\Downloads.</summary>
    public static string Downloads
    {
        get
        {
            if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out IntPtr path) == 0)
            {
                try
                {
                    string? result = Marshal.PtrToStringUni(path);
                    if (!string.IsNullOrEmpty(result))
                        return result;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(path);
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
