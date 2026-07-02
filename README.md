# ComicLayout Editor

Windows desktop editor for laying out comics: A4 pages, panels, images and text
balloons. It lets you save/reopen projects and print the result.

See [PLAN.md](PLAN.md) for the phased development plan.

## Stack

- **Language/Framework:** C# .NET 8, WPF
- **MVVM:** [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- **Persistence:** `.comicproj` package (ZIP) = `manifest.json` + `assets/` folder
- **Distribution:** `dotnet publish` self-contained, single-file, for `win-x64`

## Structure

```
ComicLayoutEditor.sln
src/
  ComicLayoutEditor.App/     # WPF UI (Views, ViewModels, Controls, Converters)
  ComicLayoutEditor.Core/    # Data model, serialization, printing (no WPF)
  ComicLayoutEditor.Tests/   # Unit tests (xUnit)
```

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes the Windows Desktop SDK)
- Windows 10/11 (WPF)

## Build and run

```powershell
# Restore and build the whole solution
dotnet build

# Run the desktop application
dotnet run --project src/ComicLayoutEditor.App

# Run the tests
dotnet test
```

## Build the distributable executable

The publish settings (self-contained, single-file, compression) live in the
`.csproj` and turn on automatically when publishing with a concrete runtime, so
this is enough:

```powershell
dotnet publish src/ComicLayoutEditor.App -c Release -r win-x64
```

It is equivalent to passing the parameters by hand:

```powershell
dotnet publish src/ComicLayoutEditor.App -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

Result: **a single file** of ~70 MB at
`src/ComicLayoutEditor.App/bin/Release/net8.0-windows/win-x64/publish/ComicLayoutEditor.App.exe`.

It is **self-contained**: it bundles the .NET runtime and WPF, so it runs on a
clean Windows 10/11 **without installing anything**. To distribute it, copy just
that `.exe`.

> The first launch extracts the native libraries into a temporary user folder;
> subsequent launches are instant.

## Installer (optional)

`installer/ComicLayoutEditor.iss` contains an
[Inno Setup](https://jrsoftware.org/isinfo.php) script that builds an installer
with shortcuts and an uninstaller. After publishing the `.exe`, compile it with:

```powershell
ISCC installer\ComicLayoutEditor.iss
```

The installer is produced at `installer\Output\ComicLayoutEditorSetup.exe`. It is
not required to use the app (the published `.exe` is already standalone).

## Privacy

The application runs **100% locally**: it uses no online API or cloud service.
Projects, images and exports are saved only on your machine.
