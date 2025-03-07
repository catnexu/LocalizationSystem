namespace LocalizationSystem
{
    public sealed class DefaultTableTypeProvider : ITableTypeProvider<string>
    {
        public string GetKey(string key) => key;
        public bool IsAvailable(string type) => true;
    }
}