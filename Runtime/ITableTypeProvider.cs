namespace LocalizationSystem
{
    public interface ITableTypeProvider<T>
    {
        T GetKey(string key);
        bool IsAvailable(T type);
    }
}