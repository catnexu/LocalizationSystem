using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem
{
    internal sealed class LocalizationConfig : ScriptableObject
    {
        internal const string ConfigName = "com.catnexu.localizationsystem.config";

        [SerializeField] private LocaleIdentifier[] _locales;
        [SerializeField] private string[] _tables;

        public IReadOnlyList<LocaleIdentifier> Locales => _locales;
        public IReadOnlyList<string> Tables => _tables;
    }
}