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

        [SerializeField, Tooltip("Tables that are not preloaded on locale change. They are loaded on the first request.")]
        private string[] _lazyTables;

        public IReadOnlyList<LocaleIdentifier> Locales => _locales;
        public IReadOnlyList<string> Tables => _tables;
        public IReadOnlyList<string> LazyTables => _lazyTables ?? System.Array.Empty<string>();
    }
}