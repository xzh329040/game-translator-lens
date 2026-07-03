# Publish Layout Notes

Game Translator Lens is published as a self-contained Windows x64 WPF app. The real app must keep its .NET runtime files, native dependencies, OneOCR files, and resources together. Do not manually move DLLs out of the app publish directory.

The v1.0.0 portable package uses an outer launcher so the user-facing root stays clean while the real self-contained app layout remains intact:

```text
GameTranslatorLens/
  GameTranslatorLens.exe      # small outer launcher
  GameTranslatorLensUpdater.exe # small outer updater
  GameTranslatorLensUninstall.exe # small outer uninstaller
  README.md
  app/
    GameTranslatorLens.exe    # real self-contained WPF app
    *.dll
    OneOcr/
    Resources/
    cs/
    de/
    ...
```

## Packaging

Use the packaging script from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/PackageRelease.ps1
```

The script:

- reads `<Version>` from `OwTranslateLite.csproj`;
- publishes the real app into `dist/GameTranslatorLens/app/`;
- builds the outer launcher as `dist/GameTranslatorLens/GameTranslatorLens.exe`;
- builds the outer updater as `dist/GameTranslatorLens/GameTranslatorLensUpdater.exe`;
- builds the outer uninstaller as `dist/GameTranslatorLens/GameTranslatorLensUninstall.exe`;
- copies the matching `Docs/Release-vX.Y.Z.md` as `README.md` when present;
- creates `dist/GameTranslatorLens-vX.Y.Z-portable-win-x64.zip`;
- creates `dist/GameTranslatorLens-vX.Y.Z-portable-win-x64.zip.sha256.txt`.

## Rules

- Keep `app/GameTranslatorLens.exe` and all published .NET files together.
- Keep OneOCR native files under `app/OneOcr/`.
- Keep glossary, UI, and QuickStart resources under `app/Resources/`.
- Do not publish or zip from routine code changes; package only for a tester/release build.
- If the launcher changes, build and smoke-test the package from the outer `GameTranslatorLens.exe`, not only from `app/GameTranslatorLens.exe`.
- If the updater changes, test both paths: automatic update from a release asset URL and manual update with a zip placed in the outer package folder.
- If the uninstaller changes, test it only from a disposable package copy; it deletes the package root and `%AppData%\GameTranslatorLens`.
