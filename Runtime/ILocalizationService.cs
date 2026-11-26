using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem
{
    public interface ILocalizationService
    {
        /// <summary>
        /// Raised when tables of the new locale are loaded, and when a lazy table is loaded.
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
    }

    public interface ILocalizationService<in T> : ILocalizationService
    {
        string GetLocalizedString(T tableKey, string entry, params object[] args);
        bool TryGetLocalizedString(T tableKey, string entry, out string value, params object[] args);
        UniTask LoadTableAsync(T tableKey, CancellationToken token = default);
    }
}