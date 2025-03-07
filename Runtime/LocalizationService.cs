﻿using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LocalizationSystem
{
    public class LocalizationService<T> : ILocalizationService, ILocalizationService<T>, IDisposable
    {
        private const string Empty = "Localization not found";
        public event Action<ILocalizationService> OnLocaleUpdate;

        private readonly ITableTypeProvider<T> _typeProvider;
        private readonly Dictionary<T, StringTable> _tableMap;
        private readonly HashSet<LocaleIdentifier> _availableLocales;
        private TableReference[] _tableReferences;
        private T[] _tableKeys;


        private LocalizationSettings _settings;
        private CancellationTokenSource _loadCts;
        private bool _isInitialized;

        private AsyncOperationHandle<ILocalizationService> _initializeHandle;


        public LocalizationService(ITableTypeProvider<T> typeProvider)
        {
            _typeProvider = typeProvider;
            _tableMap = new Dictionary<T, StringTable>();
            _availableLocales = new HashSet<LocaleIdentifier>();
            _tableReferences = Array.Empty<TableReference>();
            _tableKeys = Array.Empty<T>();
        }

        public void Dispose()
        {
            _settings.OnSelectedLocaleChanged -= UpdateLocale;
        }


        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
                return;
            _isInitialized = true;
            if (!LocalizationSettings.InitializationOperation.IsDone)
                await LocalizationSettings.InitializationOperation.Task.AsUniTask();

            _settings = LocalizationSettings.GetInstanceDontCreateDefault();
            _settings.OnSelectedLocaleChanged += UpdateLocale;

            if (!LocalizationSettings.SelectedLocaleAsync.IsDone)
                await _settings.GetSelectedLocaleAsync().Task.AsUniTask();


            LocalizationConfig config = await Addressables.LoadAssetAsync<LocalizationConfig>(LocalizationConfig.ConfigName).Task.AsUniTask();
            IReadOnlyList<string> validatedTables = config.Tables;
            _tableReferences = new TableReference[validatedTables.Count];
            _tableKeys = new T[validatedTables.Count];

            for (int i = 0; i < validatedTables.Count; i++)
            {
                string tableName = validatedTables[i];
                _tableReferences[i] = tableName;
                T key = _typeProvider.GetKey(tableName);
                _tableKeys[i] = key;
                _tableMap[key] = null;
            }

            for (int index = 0; index < config.Locales.Count; index++)
            {
                LocaleIdentifier localeId = config.Locales[index];
                _availableLocales.Add(localeId);
            }

            await UpdateLocaleTables();
        }
        

        public void SetLocale(SystemLanguage language)
        {
            if (!_isInitialized)
                return;
            if (!TrySetLocale(new LocaleIdentifier(language)))
            {
                throw new Exception($"Language {language} in not available");
            }
        }

        public void SetLocale(LocaleIdentifier identifier)
        {
            if (!_isInitialized)
                return;
            if (!TrySetLocale(identifier))
            {
                throw new Exception($"Locale {identifier} in not available");
            }
        }

        private bool TrySetLocale(LocaleIdentifier id)
        {
            var list = _settings.GetAvailableLocales().Locales;
            for (int i = 0; i < list.Count; i++)
            {
                Locale locale = list[i];
                if (locale.Identifier == id)
                {
                    _settings.SetSelectedLocale(locale);
                    return true;
                }
            }

            return false;
        }


        public string GetLocalizedString(string tableName, string entry)
        {
            T tableType = _typeProvider.GetKey(tableName);
            return _typeProvider.IsAvailable(tableType) ? GetLocalizedString(tableType, entry) : Empty;
        }

        public string GetLocalizedString(T tableKey, string entry)
        {
            return TryGetLocalizedString(tableKey, entry) ?? Empty;
        }

        private string TryGetLocalizedString(T type, string entry)
        {
            if (_isInitialized && !string.IsNullOrEmpty(entry))
                return TryGetEntry(type, entry);
            return null;
        }


        private string TryGetEntry(T type, string entry)
        {
            if (_tableMap.TryGetValue(type, out StringTable value))
            {
                try
                {
                    return GetLocalizedString(value, entry);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
        }

        private static string GetLocalizedString(StringTable table, string entry) => table ? table.GetEntry(entry)?.GetLocalizedString() : null;

        private async UniTask UpdateLocaleTables()
        {
            if (_loadCts != null)
            {
                if (!_loadCts.IsCancellationRequested)
                    _loadCts.Cancel();
                _loadCts.Dispose();
            }

            _loadCts = new CancellationTokenSource();
            await UpdateLocaleAsync(_loadCts.Token);
        }

        private void UpdateLocale(Locale locale)
        {
            UpdateLocaleTables().Forget();
        }


        private async UniTask UpdateLocaleAsync(CancellationToken token)
        {
            LocalizedStringDatabase database = LocalizationSettings.StringDatabase;
            var tasks = new List<UniTask>();
            for (int i = 0; i < _tableKeys.Length; i++)
            {
                T tableKey = _tableKeys[i];
                tasks.Add(UpdateTableAsync(_tableMap, tableKey, database, _tableReferences[i], token));
            }

            await UniTask.WhenAll(tasks);
            OnLocaleUpdate?.Invoke(this);
        }


        private static async UniTask UpdateTableAsync(Dictionary<T, StringTable> map, T key, LocalizedStringDatabase database,
            TableReference reference, CancellationToken token)
        {
            StringTable table = await database.GetTableAsync(reference).Task.AsUniTask().AttachExternalCancellation(token);
            if (!token.IsCancellationRequested)
                map[key] = table;
        }
    }
}