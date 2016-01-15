namespace Nine.Storage
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstracts a key value pair object storage.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        Task<T> Get<T>(string key) where T : class, IKeyed, new();

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        /// <param name="minKey">The inclusive minimum value of the key, or null to indicate there is no lower bound.</param>
        /// <param name="maxKey">The exclusive maximum value of the key, or null to indicate there is no upper bound.</param>
        /// <param name="count">The maximum number of records to return, or null to return all records.</param>
        /// <returns>
        /// A list of valid results sorted in lexicographical order.
        /// </returns>
        /// <remarks>
        /// When a max count is specified, the results are the top N values sorted by key.
        /// The resulting collection can contain zero elements but should never be null.
        /// </remarks>
        Task<IEnumerable<T>> Range<T>(string minKey, string maxKey, int? count = null) where T : class, IKeyed, new();

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        /// <returns>Indicates whether the add is successful.</returns>
        Task<bool> Add<T>(T value) where T : class, IKeyed, new();

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        Task Put<T>(T value) where T : class, IKeyed, new();

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        /// <returns>Indicates whether the entity exists and removed.</returns>
        Task<bool> Delete<T>(string key) where T : class, IKeyed, new();
    }

    /// <summary>
    /// Abstracts a key value pair object storage.
    /// </summary>
    public interface IStorage<T> where T : class, IKeyed, new()
    {
        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        Task<T> Get(string key);

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        /// <param name="minKey">The inclusive minimum value of the key, or null to indicate there is no lower bound.</param>
        /// <param name="maxKey">The exclusive maximum value of the key, or null to indicate there is no upper bound.</param>
        /// <param name="count">The maximum number of records to return, or null to return all records.</param>
        /// <returns>
        /// A list of valid results sorted in lexicographical order.
        /// </returns>
        /// <remarks>
        /// When a max count is specified, the results are the top N values sorted by key.
        /// The resulting collection can contain zero elements but should never be null.
        /// </remarks>
        Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null);

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        /// <returns>Indicates whether the add is successful.</returns>
        Task<bool> Add(T value);

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        Task Put(T value);

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        /// <returns>Indicates whether the entity exists and removed.</returns>
        Task<bool> Delete(string key);
    }
}
