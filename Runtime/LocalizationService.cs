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
using Object = UnityEngine.Object;

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
        private readonly Dictionary<T, AssetTableSlot> _assetSlotMap;
        private readonly List<AssetTableSlot> _assetSlots;
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
            _assetSlotMap = new Dictionary<T, AssetTableSlot>();
            _assetSlots = new List<AssetTableSlot>();
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
            ClearSlots();
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
                token.ThrowIfCancellationRequested();
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

                ClearSlots();

                var lazyTables = new HashSet<string>(config.LazyTables);
                foreach (string tableName in config.Tables ?? Array.Empty<string>())
                {
                    if (TryGetNewKey(tableName, _slotMap, out T key))
                    {
                        var slot = new TableSlot(tableName, !lazyTables.Contains(tableName));
                        _slots.Add(slot);
                        _slotMap.Add(key, slot);
                    }
                }

                var preloadAssetTables = new HashSet<string>(config.PreloadAssetTables);
                foreach (string tableName in config.AssetTables)
                {
                    if (TryGetNewKey(tableName, _assetSlotMap, out T key))
                    {
                        var slot = new AssetTableSlot(tableName, preloadAssetTables.Contains(tableName));
                        _assetSlots.Add(slot);
                        _assetSlotMap.Add(key, slot);
                    }
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

        private bool TryGetNewKey<TSlot>(string tableName, Dictionary<T, TSlot> map, out T key)
        {
            key = default;
            if (string.IsNullOrEmpty(tableName))
                return false;
            key = _typeProvider.GetKey(tableName);
            return key is not null && !map.ContainsKey(key);
        }

        private void ClearSlots()
        {
            foreach (TableSlot slot in _slots)
                slot.Release();
            foreach (AssetTableSlot slot in _assetSlots)
                slot.Release();
            _slots.Clear();
            _slotMap.Clear();
            _assetSlots.Clear();
            _assetSlotMap.Clear();
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
            TableSlot slot = GetSlot(_slotMap, tableKey);
            return slot.Version == _version ? UniTask.CompletedTask : WaitForSlotAsync(slot, token);
        }

        private UniTask RequestTableAsync(TableSlot slot)
        {
            if (slot.Loading is { } loading)
                return loading;

            AsyncOperationHandle<StringTable> handle = LocalizationSettings.StringDatabase.GetTableAsync(slot.Reference);
            if (handle.IsDone)
            {
                // The table is already loaded by the database, so it is available right away without OnLocaleUpdated.
                slot.SetTable(handle.Result, handle, _version);
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
            StringTable table = await AwaitHandleAsync(handle, token);
            if (token.IsCancellationRequested || version != _version)
                return;

            slot.SetTable(table, handle, version);
            if (table)
                OnLocaleUpdated?.Invoke(this);
        }


        public UniTask<TAsset> GetLocalizedAssetAsync<TAsset>(string tableName, string entry, CancellationToken token = default)
            where TAsset : Object
        {
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));
            return GetLocalizedAssetAsync<TAsset>(_typeProvider.GetKey(tableName), entry, token);
        }

        public async UniTask<TAsset> GetLocalizedAssetAsync<TAsset>(T tableKey, string entry, CancellationToken token = default)
            where TAsset : Object
        {
            AssetTableSlot slot = GetSlot(_assetSlotMap, tableKey);
            if (string.IsNullOrEmpty(entry) || !_typeProvider.IsAvailable(tableKey))
                return null;

            while (true)
            {
                if (slot.Version != _version)
                    await WaitForSlotAsync(slot, token);

                int version = _version;
                AsyncOperationHandle<TAsset> handle = GetAssetHandle<TAsset>(slot, entry, notify: false);
                if (!handle.IsValid())
                    return null;

                TAsset asset = handle.IsDone ? handle.Result : await AwaitHandleAsync(handle, token);
                if (_isDisposed)
                    throw new OperationCanceledException();
                // If the locale is changed while loading, the asset of the new locale is loaded instead.
                if (version == _version)
                    return asset;
            }
        }

        public bool TryGetLocalizedAsset<TAsset>(string tableName, string entry, out TAsset asset) where TAsset : Object
        {
            if (tableName == null)
            {
                asset = null;
                return false;
            }

            return TryGetLocalizedAsset(_typeProvider.GetKey(tableName), entry, out asset);
        }

        public bool TryGetLocalizedAsset<TAsset>(T tableKey, string entry, out TAsset asset) where TAsset : Object
        {
            asset = null;
            if (!_isInitialized || tableKey is null || string.IsNullOrEmpty(entry) || !_typeProvider.IsAvailable(tableKey) ||
                !_assetSlotMap.TryGetValue(tableKey, out AssetTableSlot slot))
                return false;

            if (!slot.Table && slot.Loading == null)
                RequestAssetTableAsync(slot).Forget();

            AsyncOperationHandle<TAsset> handle = GetAssetHandle<TAsset>(slot, entry, notify: true);
            if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
                return false;

            asset = handle.Result;
            return asset != null;
        }

        public UniTask LoadAssetTableAsync(string tableName, CancellationToken token = default)
        {
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));
            return LoadAssetTableAsync(_typeProvider.GetKey(tableName), token);
        }

        public UniTask LoadAssetTableAsync(T tableKey, CancellationToken token = default)
        {
            AssetTableSlot slot = GetSlot(_assetSlotMap, tableKey);
            return slot.Version == _version ? UniTask.CompletedTask : WaitForSlotAsync(slot, token);
        }

        public void ReleaseAsset(string tableName, string entry)
        {
            if (tableName != null)
                ReleaseAsset(_typeProvider.GetKey(tableName), entry);
        }

        public void ReleaseAsset(T tableKey, string entry)
        {
            if (tableKey is null || string.IsNullOrEmpty(entry) || !_assetSlotMap.TryGetValue(tableKey, out AssetTableSlot slot))
                return;

            slot.ReleaseAsset(entry);
            // Tables of the previous locale are already released by the database.
            if (slot.Table && slot.Version == _version)
                slot.Table.ReleaseAsset(entry);
        }

        /// <summary>
        /// Returns the held handle of the entry asset, or starts loading it from the table of the current locale.
        /// While the locale is changing, only the assets of the previous locale that are already requested are returned.
        /// </summary>
        private AsyncOperationHandle<TAsset> GetAssetHandle<TAsset>(AssetTableSlot slot, string entry, bool notify) where TAsset : Object
        {
            var key = (entry, typeof(TAsset));
            if (slot.Assets.TryGetValue(key, out AsyncOperationHandle held) && held.IsValid())
                return held.Convert<TAsset>();

            AssetTable table = slot.Table;
            if (!table || slot.Version != _version || table.GetEntry(entry) == null)
                return default;

            AsyncOperationHandle<TAsset> handle = table.GetAssetAsync<TAsset>(entry);
            slot.Assets[key] = Hold(handle);
            if (notify && !handle.IsDone)
            {
                int version = slot.Version;
                handle.Completed += h =>
                {
                    if (!_isDisposed && version == _version && h.Status == AsyncOperationStatus.Succeeded)
                        OnLocaleUpdated?.Invoke(this);
                };
            }

            return handle;
        }

        private UniTask RequestAssetTableAsync(AssetTableSlot slot)
        {
            if (slot.Loading is { } loading)
                return loading;

            AsyncOperationHandle<AssetTable> handle = LocalizationSettings.AssetDatabase.GetTableAsync(slot.Reference);
            AssetTable table = handle.IsDone ? handle.Result : null;
            if (handle.IsDone && (!slot.Preload || !table || table.PreloadOperation.IsDone))
            {
                slot.SetTable(table, handle, _version);
                slot.Loading = UniTask.CompletedTask;
            }
            else
            {
                slot.Loading = LoadLazyAssetTableAsync(slot, handle, _version, _lifetimeCts.Token).Preserve();
            }

            return slot.Loading.Value;
        }

        private async UniTask LoadLazyAssetTableAsync(AssetTableSlot slot,
            AsyncOperationHandle<AssetTable> handle,
            int version,
            CancellationToken token)
        {
            AssetTable table = await LoadAssetTableContentAsync(handle, slot.Preload, token);
            if (token.IsCancellationRequested || version != _version)
                return;

            slot.SetTable(table, handle, version);
            if (table)
                OnLocaleUpdated?.Invoke(this);
        }

        private static async UniTask<AssetTable> LoadAssetTableContentAsync(AsyncOperationHandle<AssetTable> handle,
            bool preload,
            CancellationToken token)
        {
            AssetTable table = await AwaitHandleAsync(handle, token);
            if (!preload || !table)
                return table;

            AsyncOperationHandle preloadHandle = table.PreloadOperation;
            if (!preloadHandle.IsDone)
                await AwaitHandleAsync(preloadHandle, token);
            return table;
        }


        private TSlot GetSlot<TSlot>(Dictionary<T, TSlot> map, T tableKey)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Localization system is not initialized");
            if (tableKey is null || !map.TryGetValue(tableKey, out TSlot slot))
                throw new ArgumentException($"Table {tableKey} is not found in the {nameof(LocalizationConfig)}", nameof(tableKey));
            return slot;
        }

        /// <summary>
        /// Waits until the slot gets the table of the current locale. If the locale is changed while loading, waits for the new one.
        /// </summary>
        private async UniTask WaitForSlotAsync(SlotBase slot, CancellationToken token)
        {
            while (slot.Version != _version)
            {
                if (_isDisposed)
                    throw new OperationCanceledException();

                UniTask task = slot is AssetTableSlot assetSlot ? RequestAssetTableAsync(assetSlot) : RequestTableAsync((TableSlot) slot);
                await (token.CanBeCanceled ? task.AttachExternalCancellation(token) : task);
            }
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

        private UniTask UpdateLocaleAsync(int version, CancellationToken token)
        {
            // Lazy tables that were already requested are reloaded together with the preloaded ones.
            var slots = new List<TableSlot>(_slots.Count);
            foreach (TableSlot slot in _slots)
            {
                // A lazy load started for the previous locale is not valid anymore.
                slot.Loading = null;
                if (slot.Preload || slot.Table)
                    slots.Add(slot);
            }

            var assetSlots = new List<AssetTableSlot>(_assetSlots.Count);
            foreach (AssetTableSlot slot in _assetSlots)
            {
                slot.Loading = null;
                if (slot.Preload || slot.Table)
                    assetSlots.Add(slot);
            }

            UniTask task = SwapTablesAsync(version, slots, assetSlots, token).Preserve();
            // Requests to these tables wait for the whole swap, so tables of different locales are not mixed.
            foreach (TableSlot slot in slots)
                slot.Loading = task;
            foreach (AssetTableSlot slot in assetSlots)
                slot.Loading = task;
            return task;
        }

        private async UniTask SwapTablesAsync(int version, List<TableSlot> slots, List<AssetTableSlot> assetSlots, CancellationToken token)
        {
            LocalizedStringDatabase stringDatabase = LocalizationSettings.StringDatabase;
            var handles = new AsyncOperationHandle<StringTable>[slots.Count];
            var tasks = new UniTask<StringTable>[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                handles[i] = stringDatabase.GetTableAsync(slots[i].Reference);
                tasks[i] = AwaitHandleAsync(handles[i], token);
            }

            LocalizedAssetDatabase assetDatabase = LocalizationSettings.AssetDatabase;
            var assetHandles = new AsyncOperationHandle<AssetTable>[assetSlots.Count];
            var assetTasks = new UniTask<AssetTable>[assetSlots.Count];
            for (int i = 0; i < assetSlots.Count; i++)
            {
                assetHandles[i] = assetDatabase.GetTableAsync(assetSlots[i].Reference);
                assetTasks[i] = LoadAssetTableContentAsync(assetHandles[i], assetSlots[i].Preload, token);
            }

            // A swap canceled by the next locale change completes without an exception, so the waiting requests move to the next swap.
            var (isCanceled, (tables, assetTables)) =
                await UniTask.WhenAll(UniTask.WhenAll(tasks), UniTask.WhenAll(assetTasks)).SuppressCancellationThrow();
            if (isCanceled || token.IsCancellationRequested || version != _version)
                return;

            // Tables are replaced only when all of them are loaded, so strings and assets of different locales are not mixed.
            // Tables and assets of the previous locale are held until this moment and released here.
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetTable(tables[i], handles[i], version);
            for (int i = 0; i < assetSlots.Count; i++)
                assetSlots[i].SetTable(assetTables[i], assetHandles[i], version);

            OnLocaleUpdated?.Invoke(this);
        }

        private static async UniTask<TResult> AwaitHandleAsync<TResult>(AsyncOperationHandle<TResult> handle, CancellationToken token)
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
                return default;
            }
        }

        private static async UniTask AwaitHandleAsync(AsyncOperationHandle handle, CancellationToken token)
        {
            try
            {
                await handle.Task.AsUniTask().AttachExternalCancellation(token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogException(e);
#endif
            }
        }

        /// <summary>
        /// The database releases tables and assets of the previous locale right on the locale change,
        /// so the service holds its own references until the new ones are loaded.
        /// </summary>
        private static AsyncOperationHandle Hold(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return default;
            Addressables.ResourceManager.Acquire(handle);
            return handle;
        }

        private static void Unhold(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return;
            // Releasing an operation in progress destroys it, so it is released when completed.
            if (handle.IsDone)
                Addressables.Release(handle);
            else
                handle.Completed += h => Addressables.Release(h);
        }


        private abstract class SlotBase
        {
            public readonly TableReference Reference;
            public readonly bool Preload;

            /// <summary>
            /// The version of the locale the table is loaded for.
            /// </summary>
            public int Version;

            public UniTask? Loading;

            protected SlotBase(string tableName, bool preload)
            {
                Reference = tableName;
                Preload = preload;
            }
        }

        private sealed class TableSlot : SlotBase
        {
            /// <summary>
            /// Table of the current locale, or of the previous one until the new tables are loaded.
            /// </summary>
            public StringTable Table;

            private AsyncOperationHandle _handle;

            public TableSlot(string tableName, bool preload) : base(tableName, preload)
            {
            }

            public void SetTable(StringTable table, AsyncOperationHandle<StringTable> handle, int version)
            {
                AsyncOperationHandle previous = _handle;
                _handle = table ? Hold(handle) : default;
                Unhold(previous);
                Table = table;
                Version = version;
            }

            public void Release()
            {
                Unhold(_handle);
                _handle = default;
                Table = null;
            }
        }

        private sealed class AssetTableSlot : SlotBase
        {
            /// <summary>
            /// Table of the current locale, or of the previous one until the new tables are loaded.
            /// </summary>
            public AssetTable Table;

            /// <summary>
            /// Held asset handles of <see cref="Table"/> by entry and asset type.
            /// </summary>
            public readonly Dictionary<(string, Type), AsyncOperationHandle> Assets = new Dictionary<(string, Type), AsyncOperationHandle>();

            private AsyncOperationHandle _handle;
            private AsyncOperationHandle _preloadHandle;

            public AssetTableSlot(string tableName, bool preload) : base(tableName, preload)
            {
            }

            public void SetTable(AssetTable table, AsyncOperationHandle<AssetTable> handle, int version)
            {
                AsyncOperationHandle previous = _handle;
                AsyncOperationHandle previousPreload = _preloadHandle;
                _handle = table ? Hold(handle) : default;
                _preloadHandle = table && Preload ? Hold(table.PreloadOperation) : default;
                ReleaseAssets();
                Unhold(previousPreload);
                Unhold(previous);
                Table = table;
                Version = version;
            }

            public void ReleaseAsset(string entry)
            {
                List<(string, Type)> keys = null;
                foreach ((string, Type) key in Assets.Keys)
                {
                    if (key.Item1 == entry)
                        (keys ??= new List<(string, Type)>()).Add(key);
                }

                if (keys == null)
                    return;
                foreach ((string, Type) key in keys)
                {
                    Unhold(Assets[key]);
                    Assets.Remove(key);
                }
            }

            public void Release()
            {
                ReleaseAssets();
                Unhold(_preloadHandle);
                Unhold(_handle);
                _preloadHandle = default;
                _handle = default;
                Table = null;
            }

            private void ReleaseAssets()
            {
                foreach (AsyncOperationHandle asset in Assets.Values)
                    Unhold(asset);
                Assets.Clear();
            }
        }
    }
}