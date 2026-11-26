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
}
```

`args` are [Smart String](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Smart/SmartStrings.html) arguments.
The non-generic `ILocalizationService` exposes the same methods by table name, `IsInitialized`, `CurrentLocale`, `AvailableLocales`
and the `OnLocaleUpdated` event, which is raised when the tables of the new locale (or a lazy table) are loaded.

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


