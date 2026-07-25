# Contributing to EasyClipStash

Thanks for taking a look. This is a small tool with a deliberately narrow scope, so the most useful thing you can do before writing code is check that your idea fits.

**English** | [한국어](CONTRIBUTING.ko.md)

## Before you start

Read [What this tool does not do](README.md#what-this-tool-does-not-do). Screen capture, image editing, screen recording, uploads, and clipboard history are all out of scope on purpose — the goal is to do one thing well: save what is on the clipboard to a file.

If you are unsure whether something fits, open an issue or a [discussion](https://github.com/JeongJongMun/EasyClipStash/discussions) first. That is faster than writing a pull request that has to be turned down.

## Ways to help

- **Report a bug** — include your Windows version, the app version (hover the tray icon), and the steps to reproduce.
- **Suggest a feature** — describe the problem you hit, not only the solution you have in mind.
- **Improve the docs** — typos, unclear wording, and translation fixes are all welcome.
- **Send a pull request** — see below.

## Development setup

You need **Windows** and the **.NET 10 SDK**. The project uses WinForms, so it cannot be built on Linux or macOS.

```bash
git clone https://github.com/JeongJongMun/EasyClipStash.git
cd EasyClipStash
dotnet build -c Release
```

Run it:

```bash
.\bin\Release\net10.0-windows\EasyClipStash.exe
```

The app has no main window — it starts in the system tray. Double-click the tray icon to open settings.

Settings are written to `config.json` next to the executable. Delete that file to get a fresh install state.

To reproduce what a release actually ships:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Project layout

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point, single-instance lock |
| `TrayApplicationContext.cs` | Tray icon, menu, notifications |
| `ClipboardSaver.cs` | Reads the clipboard and writes the file |
| `FileNamer.cs` | Builds the next filename from the naming rules |
| `AppConfig.cs` | `config.json` load/save, defaults, migration |
| `SettingsForm.cs`, `NamingPanel.cs` | Settings window |
| `Theme.cs`, `DarkComboBox.cs`, `DarkInputs.cs` | Dark theme |
| `Localization.cs` | All user-facing strings |
| `Updater.cs` | Update check, download, verify, self-replace |
| `HotkeyManager.cs`, `StartupManager.cs` | Global hotkey, run-at-startup |

## Things that are easy to miss

**Every user-facing string lives in `Localization.cs`, in both languages.** The `L` class returns Korean or English depending on `L.Current`. If you add a string, add both — a missing translation is a bug.

**Single-file publish has to keep working.** A change can build fine and still break the single-file build (embedded resources, trimming, native libraries). CI runs the publish command for this reason.

**The auto-updater depends on the executable name and the release assets.** It looks for `EasyClipStash.exe` inside the release zip and verifies the download against the published SHA256. Renaming the executable or changing how releases are packaged will break updates for existing users.

**There are no tests yet.** `FileNamer` is pure logic and would be a good place to start if you want to add some — CI would then catch naming regressions.

## Commit messages

One line, lowercase after the type, no trailing period:

```
type : short description in the imperative
```

Types used in this repo:

| Type | For |
|---|---|
| `feat` | New behaviour a user can notice |
| `fix` | Bug fix |
| `docs` | README, this file, comments-only changes |
| `ci` | Workflows |
| `refactor` | Restructuring with no behaviour change |
| `change` | A deliberate change to existing behaviour or a default |

Examples from the history:

```
feat : split tray folder menu into image and text
fix : use /assets/img as default markdown url path
ci : add build workflow for main and pull requests
```

## Pull requests

1. Branch off `main`.
2. Keep the change focused — one concern per pull request.
3. Build locally and try the affected flow in the running app. This is a GUI tool; a clean build does not mean the feature works.
4. Describe what you changed and how you verified it.
5. CI must pass. It builds the project and verifies the single-file publish.

Match the style of the file you are editing — indentation, naming, and comment density. Existing comments are in Korean; either language is fine for new ones, so stay consistent within a file.

## Releases

Releases are cut by the maintainer. Pushing a `v*` tag triggers the workflow that builds, packages, publishes the SHA256 checksum, and creates the GitHub release. The version comes from the tag, so `EasyClipStash.csproj` does not need editing.

## License

By contributing you agree that your work is licensed under the [MIT License](LICENSE).
