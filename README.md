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
    string GetLocalizedString(T tableKey, string entry);
}
```

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
await localizationService.InitializeAsync();
```


