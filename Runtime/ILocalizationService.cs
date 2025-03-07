using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem
{
    public interface ILocalizationService
    {
        event Action<ILocalizationService> OnLocaleUpdate;
        string GetLocalizedString(string tableName, string entry);
        void SetLocale(SystemLanguage language);
        void SetLocale(LocaleIdentifier identifier);
        UniTask InitializeAsync();
    }
    
    public interface ILocalizationService<in T> : ILocalizationService
    {
        string GetLocalizedString(T tableKey, string entry);
    }
}