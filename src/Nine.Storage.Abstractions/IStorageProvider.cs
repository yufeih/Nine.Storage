namespace Nine.Storage
{
    using System.Threading.Tasks;

    public interface IStorageProvider
    {
        Task<IStorage<T>> GetAsync<T>();
    }
}
