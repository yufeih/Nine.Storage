namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Elasticsearch.Net;
    using Nest;

    public class ElasticSearchStorage<T> : IStorage<T> where T : class
    {
        private readonly ElasticClient client;
        private readonly string typeName = typeof(T).Name.ToLowerInvariant();

        public ElasticSearchStorage(string endpoint, string indexName = "default")
        {
            if (string.IsNullOrEmpty(endpoint)) throw new ArgumentException("endpoint");
            if (string.IsNullOrEmpty(indexName)) throw new ArgumentException("indexName");

            this.client = new ElasticClient(new ConnectionSettings(new Uri(endpoint), indexName.ToLowerInvariant()));
        }

        public async Task<T> Get(string key)
        {
            var response = await this.client.GetAsync<T>(key, null, typeName).ConfigureAwait(false);
            EnsureSuccess(response);
            return response.Source;
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            throw new NotSupportedException();
        }

        public async Task<bool> Add(string key, T value)
        {
            var response = await client.IndexAsync(value, t => t.Id(key).OpType(OpType.Create)).ConfigureAwait(false);
            if (response.ServerError != null && response.ServerError.Status == 409) return false;
            EnsureSuccess(response);
            return response.Created;
        }

        public async Task Put(string key, T value)
        {
            var response = await client.IndexAsync(value, t => t.Id(key)).ConfigureAwait(false);
            EnsureSuccess(response);
        }

        public async Task<bool> Delete(string key)
        {
            var response = await client.DeleteAsync(null, typeName, key).ConfigureAwait(false);
            EnsureSuccess(response);
            return response.Found;
        }

        private IResponse EnsureSuccess(IResponse response)
        {
            if (!response.IsValid)
            {
                throw new InvalidOperationException(string.Join(", ",
                    response.ServerError.Status,
                    response.ServerError.ExceptionType,
                    response.ServerError.Error));
            }
            return response;
        }
    }
}
