using System;
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
        private static readonly char[] s_codeSeparators = {'-', '_'};

        public event Action<ILocalizationService> OnLocaleUpdated;

        private readonly ITableTypeProvider<T> _typeProvider;
        private readonly Dictionary<T, TableSlot> _slotMap;
        private readonly List<TableSlot> _slots;
        private readonly CancellationTokenSource _lifetimeCts;

        private LocalizationSettings _settings;
        private CancellationTokenSource _loadCts;
        private UniTask? _initializeTask;
        private int _version;
        private bool _isInitialized;
        private bool _isDisposed;

        public bool IsInitialized => _isInitialized;
        public Locale CurrentLocale => _isInitialized ? _settings.GetSelectedLocale() : null;

        public IReadOnlyList<Locale> AvailableLocales =>
            _isInitialized ? _settings.GetAvailableLocales().Locales : (IReadOnlyList<Locale>) Array.Empty<Locale>();


        public LocalizationService(ITableTypeProvider<T> typeProvider)
        {
            _typeProvider = typeProvider;
            _slotMap = new Dictionary<T, TableSlot>();
            _slots = new List<TableSlot>();
            _lifetimeCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _isInitialized = false;

            if (_settings != null)
                _settings.OnSelectedLocaleChanged -= UpdateLocale;
            CancelLoading();
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            _slots.Clear();
            _slotMap.Clear();
            OnLocaleUpdated = null;
        }

        /// <summary>
        /// Concurrent calls share the same initialization. If it fails or is canceled, the next call starts it again.
        /// </summary>
        public UniTask InitializeAsync(CancellationToken token = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_initializeTask is not { } task || task.Status is UniTaskStatus.Faulted or UniTaskStatus.Canceled)
            {
                task = InitializeInternalAsync(token).Preserve();
                _initializeTask = task;
            }

            return token.CanBeCanceled ? task.AttachExternalCancellation(token) : task;
        }

        private async UniTask InitializeInternalAsync(CancellationToken token)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetimeCts.Token);
            token = linkedCts.Token;

            if (!LocalizationSettings.InitializationOperation.IsDone)
                await LocalizationSettings.InitializationOperation.Task.AsUniTask().AttachExternalCancellation(token);

            _settings = LocalizationSettings.GetInstanceDontCreateDefault();
            if (!LocalizationSettings.SelectedLocaleAsync.IsDone)
                await _settings.GetSelectedLocaleAsync().Task.AsUniTask().AttachExternalCancellation(token);

            await LoadConfigAsync(token);
            token.ThrowIfCancellationRequested();

            _settings.OnSelectedLocaleChanged += UpdateLocale;
            try
            {
                await UpdateLocaleAsync(++_version, token);
            }
            catch
            {
                _settings.OnSelectedLocaleChanged -= UpdateLocale;
                throw;
            }

            _isInitialized = true;
        }

        private async UniTask LoadConfigAsync(CancellationToken token)
        {
            AsyncOperationHandle<LocalizationConfig> handle = Addressables.LoadAssetAsync<LocalizationConfig>(LocalizationConfig.ConfigName);
            try
            {
                LocalizationConfig config = await handle.Task.AsUniTask().AttachExternalCancellation(token);
                if (!config)
                    throw new InvalidOperationException($"{nameof(LocalizationConfig)} is not loaded", handle.OperationException);

                var lazyTables = new HashSet<string>(config.LazyTables);
                _slots.Clear();
                _slotMap.Clear();
                foreach (string tableName in config.Tables ?? Array.Empty<string>())
                {
                    if (string.IsNullOrEmpty(tableName))
                        continue;
                    T key = _typeProvider.GetKey(tableName);
                    if (key is null || _slotMap.ContainsKey(key))
                        continue;

                    var slot = new TableSlot(tableName, !lazyTables.Contains(tableName));
                    _slots.Add(slot);
                    _slotMap.Add(key, slot);
                }
            }
            finally
            {
                // The config data is copied, the asset is not needed anymore.
                if (handle.IsDone)
                    Addressables.Release(handle);
                else
                    handle.Completed += h => Addressables.Release(h);
            }
        }


        public void SetLocale(SystemLanguage language)
        {
            TrySetLocale(new LocaleIdentifier(language));
        }

        public void SetLocale(LocaleIdentifier identifier)
        {
            TrySetLocale(identifier);
        }

        private void TrySetLocale(LocaleIdentifier id)
        {
            if (!_isInitialized)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogError("Localization system is not initialized");
#endif
                return;
            }

            Locale locale = FindLocale(id);
            if (locale)
            {
                _settings.SetSelectedLocale(locale);
                return;
            }
#if UNITY_EDITOR
            UnityEngine.Debug.LogException(new Exception($"Locale {id} in not available"));
#endif
        }

        /// <summary>
        /// Finds the exact locale, then the parent one ("zh-Hant-TW" -> "zh-Hant" -> "zh"), then any locale of the same language ("en" -> "en-US").
        /// </summary>
        private Locale FindLocale(LocaleIdentifier id)
        {
            ILocalesProvider provider = _settings.GetAvailableLocales();
            Locale locale = provider.GetLocale(id);
            string code = id.Code;
            if (locale || string.IsNullOrEmpty(code))
                return locale;

            for (int i = code.LastIndexOfAny(s_codeSeparators); i > 0; i = code.LastIndexOfAny(s_codeSeparators, i - 1))
            {
                locale = provider.GetLocale(new LocaleIdentifier(code.Substring(0, i)));
                if (locale)
                    return locale;
            }

            string language = GetLanguage(code);
            List<Locale> locales = provider.Locales;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i] && string.Equals(GetLanguage(locales[i].Identifier.Code), language, StringComparison.OrdinalIgnoreCase))
                    return locales[i];
            }

            return null;
        }

        private static string GetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;
            int index = code.IndexOfAny(s_codeSeparators);
            return index > 0 ? code.Substring(0, index) : code;
        }


        public string GetLocalizedString(string tableName, string entry, params object[] args)
        {
            return TryGetLocalizedString(tableName, entry, out string value, args) ? value : Empty;
        }

        public string GetLocalizedString(T tableKey, string entry, params object[] args)
        {
            return TryGetLocalizedString(tableKey, entry, out string value, args) ? value : Empty;
        }

        public bool TryGetLocalizedString(string tableName, string entry, out string value, params object[] args)
        {
            if (tableName == null)
            {
                value = null;
                return false;
            }

            return TryGetLocalizedString(_typeProvider.GetKey(tableName), entry, out value, args);
        }

        public bool TryGetLocalizedString(T tableKey, string entry, out string value, params object[] args)
        {
            value = null;
            if (!_isInitialized || tableKey is null || string.IsNullOrEmpty(entry) || !_typeProvider.IsAvailable(tableKey) ||
                !_slotMap.TryGetValue(tableKey, out TableSlot slot))
                return false;

            if (!slot.Table && slot.Loading == null)
                RequestTableAsync(slot).Forget();

            StringTable table = slot.Table;
            StringTableEntry tableEntry = table ? table.GetEntry(entry) : null;
            if (tableEntry == null)
                return false;

            value = args is {Length: > 0} ? tableEntry.GetLocalizedString(args) : tableEntry.GetLocalizedString();
            return value != null;
        }


        public UniTask LoadTableAsync(string tableName, CancellationToken token = default)
        {
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));
            return LoadTableAsync(_typeProvider.GetKey(tableName), token);
        }

        public UniTask LoadTableAsync(T tableKey, CancellationToken token = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Localization system is not initialized");
            if (tableKey is null || !_slotMap.TryGetValue(tableKey, out TableSlot slot))
                throw new ArgumentException($"Table {tableKey} is not found in the {nameof(LocalizationConfig)}", nameof(tableKey));
            if (slot.Table && slot.Version == _version)
                return UniTask.CompletedTask;

            UniTask task = RequestTableAsync(slot);
            return token.CanBeCanceled ? task.AttachExternalCancellation(token) : task;
        }

        private UniTask RequestTableAsync(TableSlot slot)
        {
            if (slot.Loading is { } loading)
                return loading;

            AsyncOperationHandle<StringTable> handle = LocalizationSettings.StringDatabase.GetTableAsync(slot.Reference);
            if (handle.IsDone)
            {
                // The table is already loaded by the database, so it is available right away without OnLocaleUpdated.
                slot.Table = handle.Result;
                slot.Version = _version;
                slot.Loading = UniTask.CompletedTask;
            }
            else
            {
                slot.Loading = LoadLazyTableAsync(slot, handle, _version, _lifetimeCts.Token).Preserve();
            }

            return slot.Loading.Value;
        }

        private async UniTask LoadLazyTableAsync(TableSlot slot, AsyncOperationHandle<StringTable> handle, int version, CancellationToken token)
        {
            StringTable table = await LoadStringTableAsync(handle, token);
            if (token.IsCancellationRequested || version != _version)
                return;

            slot.Table = table;
            slot.Version = version;
            if (table)
                OnLocaleUpdated?.Invoke(this);
        }


        private void UpdateLocale(Locale locale)
        {
            CancelLoading();
            _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            UpdateLocaleAsync(++_version, _loadCts.Token).Forget();
        }

        private void CancelLoading()
        {
            CancellationTokenSource cts = _loadCts;
            _loadCts = null;
            if (cts == null)
                return;
            cts.Cancel();
            cts.Dispose();
        }

        private async UniTask UpdateLocaleAsync(int version, CancellationToken token)
        {
            var slots = new List<TableSlot>(_slots.Count);
            foreach (TableSlot slot in _slots)
            {
                // A lazy load started for the previous locale is not valid anymore.
                slot.Loading = null;
                // Lazy tables that were already requested are reloaded together with the preloaded ones.
                if (slot.Preload || slot.Table)
                    slots.Add(slot);
            }

            LocalizedStringDatabase database = LocalizationSettings.StringDatabase;
            var tasks = new UniTask<StringTable>[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                tasks[i] = LoadStringTableAsync(database.GetTableAsync(slots[i].Reference), token);

            StringTable[] tables = await UniTask.WhenAll(tasks);
            if (token.IsCancellationRequested || version != _version)
                return;

            // Tables are replaced only when all of them are loaded, so strings of different locales are not mixed.
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Table = tables[i];
                slots[i].Version = version;
            }

            OnLocaleUpdated?.Invoke(this);
        }

        private static async UniTask<StringTable> LoadStringTableAsync(AsyncOperationHandle<StringTable> handle, CancellationToken token)
        {
            try
            {
                return await handle.Task.AsUniTask().AttachExternalCancellation(token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogException(e);
#endif
                return null;
            }
        }


        private sealed class TableSlot
        {
            public readonly TableReference Reference;
            public readonly bool Preload;

            /// <summary>
            /// Table of the current locale, or of the previous one until the new tables are loaded.
            /// </summary>
            public StringTable Table;

            public int Version;
            public UniTask? Loading;

            public TableSlot(string tableName, bool preload)
            {
                Reference = tableName;
                Preload = preload;
            }
        }
    }
}