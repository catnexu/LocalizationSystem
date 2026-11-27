using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using Object = UnityEngine.Object;

namespace LocalizationSystem
{
    public interface ILocalizationService
    {
        /// <summary>
        /// Raised when tables of the new locale are loaded, when a lazy table is loaded,
        /// and when an asset requested by <see cref="TryGetLocalizedAsset{TAsset}(string,string,out TAsset)"/> is loaded.
        /// </summary>
        event Action<ILocalizationService> OnLocaleUpdated;

        bool IsInitialized { get; }
        Locale CurrentLocale { get; }
        IReadOnlyList<Locale> AvailableLocales { get; }

        UniTask InitializeAsync(CancellationToken token = default);
        void SetLocale(SystemLanguage language);
        void SetLocale(LocaleIdentifier identifier);

        /// <param name="args">Smart String arguments.</param>
        string GetLocalizedString(string tableName, string entry, params object[] args);

        bool TryGetLocalizedString(string tableName, string entry, out string value, params object[] args);

        /// <summary>
        /// Loads the table for the current locale. Use it to wait for a lazy table before showing its strings.
        /// </summary>
        UniTask LoadTableAsync(string tableName, CancellationToken token = default);

        /// <summary>
        /// Loads the asset of the current locale. Returns null if the entry is not found or the asset is not loaded.
        /// The asset stays loaded until the locale is changed or <see cref="ReleaseAsset(string,string)"/> is called.
        /// </summary>
        UniTask<TAsset> GetLocalizedAssetAsync<TAsset>(string tableName, string entry, CancellationToken token = default) where TAsset : Object;

        /// <summary>
        /// Returns the asset if it is already loaded, otherwise starts loading it and returns false.
        /// <see cref="OnLocaleUpdated"/> is raised when the asset is loaded.
        /// </summary>
        bool TryGetLocalizedAsset<TAsset>(string tableName, string entry, out TAsset asset) where TAsset : Object;

        /// <summary>
        /// Loads the asset table for the current locale (and all its assets if the table is preloaded).
        /// </summary>
        UniTask LoadAssetTableAsync(string tableName, CancellationToken token = default);

        /// <summary>
        /// Releases the loaded asset of the entry, so it can be unloaded from memory.
        /// </summary>
        void ReleaseAsset(string tableName, string entry);
    }

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
}