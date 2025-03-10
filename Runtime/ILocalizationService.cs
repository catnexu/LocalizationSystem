using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem
{
    public interface ILocalizationService
    {
        string GetLocalizedString(string tableName, string entry);
        void SetLocale(SystemLanguage language);
        void SetLocale(LocaleIdentifier identifier);
        UniTask InitializeAsync();
    }

    public interface ILocalizationService<in T> : ILocalizationService
    {
        event Action<ILocalizationService<T>> OnLocaleUpdated;
        string GetLocalizedString(T tableKey, string entry);
    }
}