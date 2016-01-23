namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Elasticsearch.Net;
    using Nest;
    using Nine.Storage.Batching;

    public class ElasticSearchStorage<T> : IBulkStorage<T> where T : class, IKeyed, new()
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

        public async Task<bool> Add(T value)
        {
            var response = await client.IndexAsync(value, t => t.Id(value.GetKey()).OpType(OpType.Create)).ConfigureAwait(false);
            if (response.ServerError != null && response.ServerError.Status == 409) return false;
            EnsureSuccess(response);
            return response.Created;
        }

        public async Task Put(T value)
        {
            var response = await client.IndexAsync(value, t => t.Id(value.GetKey())).ConfigureAwait(false);
            EnsureSuccess(response);
        }

        public async Task<bool> Delete(string key)
        {
            var response = await client.DeleteAsync(null, typeName, key).ConfigureAwait(false);
            EnsureSuccess(response);
            return response.Found;
        }

        public async Task<IEnumerable<bool>> Add(IEnumerable<T> values)
        {
            var descriptor = new BulkDescriptor();
            foreach (var value in values)
            {
                descriptor.Create<T>(op => op.Document(value).Id(value.GetKey()));
            }
            var response = await client.BulkAsync(descriptor).ConfigureAwait(false);
            // TODO:
            return null;
        }

        public Task Put(IEnumerable<T> values)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<bool>> Delete(IEnumerable<string> keys)
        {
            throw new NotImplementedException();
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
