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

        [SerializeField] private string[] _assetTables;

        [SerializeField, Tooltip("Asset tables that are loaded together with all their assets on locale change. " +
             "Other asset tables are loaded on the first request, and their assets are loaded one by one.")]
        private string[] _preloadAssetTables;

        public IReadOnlyList<LocaleIdentifier> Locales => _locales;
        public IReadOnlyList<string> Tables => _tables;
        public IReadOnlyList<string> LazyTables => _lazyTables ?? System.Array.Empty<string>();
        public IReadOnlyList<string> AssetTables => _assetTables ?? System.Array.Empty<string>();
        public IReadOnlyList<string> PreloadAssetTables => _preloadAssetTables ?? System.Array.Empty<string>();
    }
}