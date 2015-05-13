namespace Nine.Storage
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstracts a key value pair object storage that supports bulk operations.
    /// </summary>
    public interface IBulkStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        /// <returns>Indicates whether the add is successful.</returns>
        Task<IEnumerable<bool>> Add(IEnumerable<T> values);

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        Task Put(IEnumerable<T> values);

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        /// <returns>Indicates whether the entity exists and removed.</returns>
        Task<IEnumerable<bool>> Delete(IEnumerable<string> keys);
    }
}
