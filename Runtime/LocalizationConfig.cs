using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem
{
    public sealed class LocalizationConfig : ScriptableObject
    {
        public const string ConfigName = "com.catnexu.localizationsystem.config";

        [SerializeField] private LocaleIdentifier[] _locales;
        [SerializeField] private string[] _tables;

        public IReadOnlyList<LocaleIdentifier> Locales => _locales;
        public IReadOnlyList<string> Tables => _tables;
    }
}