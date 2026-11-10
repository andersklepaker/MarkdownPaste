# Markdown Paste

A small Windows app that pastes Markdown as formatted text, primarily in Microsoft Teams.

## Usage

1. Install the .NET 10 Desktop Runtime for Windows, or publish a self-contained app as described below.
2. Start the app. It runs in the system tray; its icon may be under the hidden icons arrow.
3. Copy Markdown, place the cursor in a Teams message, and press **Win + Ctrl + B**. Release the keys to paste.
4. Right-click the icon to select **Ctrl + Alt + V** instead, or to exit. The selection is saved in `%AppData%\MarkdownPaste\settings.json`.

Windows or other apps may reserve a hotkey. If it cannot be registered, a notification appears and you can select the other one from the menu. If the active window changes before the keys are released, pasting is canceled.

The app places HTML and the original Markdown text on the clipboard. Previous clipboard content is replaced and not restored. Applications that do not support HTML receive the original text. Teams determines which formatting is preserved. Raw HTML and images are not rendered as HTML; external images are not downloaded by the app. Links are limited to HTTP, HTTPS, and email.

## Build and run

From this directory on Windows with the .NET 10 SDK:

```powershell
dotnet run --project MarkdownPaste/MarkdownPaste.csproj
```

Publish for Windows x64 without requiring an installed .NET runtime:

```powershell
dotnet publish MarkdownPaste/MarkdownPaste.csproj -c Release -r win-x64 --self-contained true
```

The output is in `MarkdownPaste/bin/Release/net10.0-windows/win-x64/publish/`. Run `MarkdownPaste.exe` from that directory.

Conversion and the HTML clipboard format can be tested on any platform with `dotnet run --project MarkdownPaste.Tests`. The hotkey and pasting must be tested on Windows, including in Teams.

## Tests and coverage

The test program checks Markdown formatting, links, handling of images and raw HTML, and UTF-8 offsets in the HTML clipboard format. Run it with:

```powershell
dotnet run --project MarkdownPaste.Tests
```

The latest measurement with `dotnet-coverage` showed **MarkdownPaste.Core** at 89.29% line coverage (25 lines fully covered and 3 partially covered) and 98.41% block coverage (62 of 63 blocks). This applies only to the conversion library, not the entire app. The hotkey, system tray, Windows clipboard, and actual pasting into Teams are not covered by these tests.

To measure coverage again, install [dotnet-coverage](https://learn.microsoft.com/dotnet/core/additional-tools/dotnet-coverage) and run:

```powershell
dotnet-coverage collect 'dotnet run --project MarkdownPaste.Tests' --output-format xml --output coverage.xml
```

## Code size

At the last count, the project contained 295 lines of C#, including blank lines:

| Component | Lines |
| --- | ---: |
| Windows app | 223 |
| Markdown conversion | 47 |
| Tests | 25 |

Excluding blank lines, the total was 260. Project files, icon artwork, and the icon generation script are not included. These figures are a snapshot and will change as the code changes.
