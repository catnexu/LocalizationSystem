# LocalizationSystem

A simple and flexible extension for [com.unity.localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html)

## Installation

You can install this package via the Unity Package Manager using the following Git URL:
```
https://github.com/catnexu/LocalizationSystem.git
```

1. Open the Unity Editor.
2. Go to `Window > Package Manager`.
3. Click the `+` button in the top-left corner and select `Add package from git URL`.
4. Paste the URL above and click `Add`.

## Setup

### Creating a Config File
To get started, create a configuration file for the localization system:

1. In the Unity Editor, navigate to the menu:  
   `Tools > Localization > Config (Runtime)`.
2. This will generate a config file in your project folder and automatically create an additional Addressables group named `Localization-Config`.

## Usage

### Defining the Localization Service
First, define a generic interface for the localization service based on the key type used in your localization tables:

```csharp
public interface ILocalizationService<in T> : ILocalizationService
{
    string GetLocalizedString(T tableKey, string entry, params object[] args);
    bool TryGetLocalizedString(T tableKey, string entry, out string value, params object[] args);
    UniTask LoadTableAsync(T tableKey, CancellationToken token = default);

    UniTask<TAsset> GetLocalizedAssetAsync<TAsset>(T tableKey, string entry, CancellationToken token = default) where TAsset : Object;
    bool TryGetLocalizedAsset<TAsset>(T tableKey, string entry, out TAsset asset) where TAsset : Object;
    UniTask LoadAssetTableAsync(T tableKey, CancellationToken token = default);
    void ReleaseAsset(T tableKey, string entry);
}
```

`args` are [Smart String](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Smart/SmartStrings.html) arguments.
The non-generic `ILocalizationService` exposes the same methods by table name, `IsInitialized`, `CurrentLocale`, `AvailableLocales`
and the `OnLocaleUpdated` event, which is raised when the tables of the new locale (or a lazy table, or an asset requested by `TryGetLocalizedAsset`) are loaded.

### Implementing a Table Type Provider
Next, implement (or use the default) table type provider to handle key resolution:

```csharp
public interface ITableTypeProvider<T>
{
    T GetKey(string key);
    bool IsAvailable(T type);
}
```

### Resolving Dependencies
You can manually resolve the ILocalizationService and ITableTypeProvider implementations or use a Dependency Injection (DI) framework. Below is an example using a DI container:

```csharp
builder.Register<ILocalizationService<string>, ILocalizationService, LocalizationService<string>>(Lifetime.Singleton);
builder.Register<ITableTypeProvider<string>, DefaultTableTypeProvider>(Lifetime.Singleton);
```
### Initialization
Call the ILocalizationService.InitializeAsync() method in the bootstrap state of your game after initializing Addressables. This method returns a UniTask, allowing for asynchronous initialization compatible with the [Cysharp UniTask library](https://github.com/Cysharp/UniTask).

```csharp
await localizationService.InitializeAsync(cancellationToken);
```

Concurrent calls share the same initialization; if it fails or is canceled, the next call starts it again.

### Locales
`SetLocale` falls back to the parent locale (`zh-Hant-TW` → `zh-Hant` → `zh`) and then to any available locale of the same language (`en` → `en-US`).

### Lazy tables
All tables listed in the config are loaded on every locale change. Tables added to `Lazy Tables` of the config are loaded on the first request instead:
the first `GetLocalizedString` call returns the fallback and `OnLocaleUpdated` is raised when the table is loaded.
Use `LoadTableAsync` to wait for a lazy table before showing its strings. Once requested, a lazy table is reloaded on next locale changes.

On a locale change the tables of the previous locale stay in use until all tables of the new one are loaded, then they are replaced at once
and `OnLocaleUpdated` is raised.

### Localized assets (sounds, sprites, ...)
Asset table collections are added to `Asset Tables` of the config by the `Update project data` button. Use the same table key type `T` for them.

```csharp
AudioClip clip = await localizationService.GetLocalizedAssetAsync<AudioClip>("Voice", "intro_01", cancellationToken);

// Synchronous variant: returns false and starts loading if the asset is not loaded yet,
// OnLocaleUpdated is raised when it is loaded.
if (localizationService.TryGetLocalizedAsset("Voice", "intro_01", out AudioClip loaded))
    audioSource.PlayOneShot(loaded);
```

- Asset tables are lazy by default: a table is loaded on the first request and its assets are loaded one by one when requested.
  Once requested, a table is reloaded on next locale changes, but its assets are not: they are loaded again on request.
- Tables added to `Preload Asset Tables` are loaded together with all their assets on every locale change
  (unless the table has the `Preload Asset Table` metadata set to `No Preload`). `LoadAssetTableAsync` waits for the table (and its assets).
- Loaded assets stay in memory until the locale changes or `ReleaseAsset` is called. For a preloaded table `ReleaseAsset` does not unload the asset
  until the locale changes, because the asset is held by the preload operation.
- Assets of the previous locale stay loaded until the tables of the new one are loaded, so `TryGetLocalizedAsset` keeps returning them during the change.
  After `OnLocaleUpdated` they are released, so request the asset again: an `AudioSource` playing an old clip should be restarted with the new one.
- `GetLocalizedAssetAsync` returns `null` if the entry is not found or the asset fails to load. If the locale changes while loading, the asset of the
  new locale is returned.
- `[LocalizationId("Voice")]` shows keys of the asset table collection if there is no string table collection with this name.

## Importing tables (Editor)

Shared import settings are stored in `Tools > Localization > Localization Settings (Editor)`.

### Google Sheets
The Google Sheets integration lives in the optional `ExtendedLocalizationSystem.Editor.Google` assembly.
It is compiled only when `com.unity.localization` is installed (`ELS_GOOGLE_SHEETS` is defined automatically).
To exclude it, add `ELS_DISABLE_GOOGLE_SHEETS` to `Project Settings > Player > Scripting Define Symbols`.

1. Set the sheets provider, spreadsheet id and the column mapping in the settings (or copy them from a configured table).
2. Use `Add Configured Google Sheet Extension` from the `StringTableCollection` context menu.
3. `Tools > Localization > Tables > Pull all from google` pulls all collections.

### XLSX
Any `.xlsx` file located in the project can be used as a source, no extra dependencies are required.

1. Assign the file in the settings and set the columns: `Key` (required), `Key Id`, `Key Comment`, `Locale` and `Locale Comment`.
   `Extract columns from first sheet header` creates the mapping from header titles like `Key`, `Id`, `Comment`, `en`, `English`, `English Comment`.
2. Use `Add Configured Xlsx Extension` from the `StringTableCollection` context menu. The sheet with the collection name is selected by default.
3. Press `Pull` in the extension or `Tools > Localization > Tables > Pull all from xlsx` to pull all collections.

Keys are matched by id when the `Key Id` column is mapped, otherwise by name. Missing keys are kept unless `Remove Missing Keys` is enabled.
Format the id column as **text**: Excel stores numbers as doubles and loses precision of long ids.


