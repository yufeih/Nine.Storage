namespace Nine.Storage.Caching
{
    public interface ICache<T>
    {
        bool TryGet(string key, out T value);
        void Put(string key, T value);
        bool Delete(string key);
    }
}
