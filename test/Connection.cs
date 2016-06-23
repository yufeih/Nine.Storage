namespace Nine.Storage
{
    using System.IO;
    using Newtonsoft.Json;

    public class Connection
    {
        public string AzureStorage;
        public string ElasticSearch;
        public string Mongo;
        public string Memcached;
        public string Redis;
        public string Sql;

        public static Connection Current;

        static Connection()
        {
            try
            {
                Current = JsonConvert.DeserializeObject<Connection>(File.ReadAllText("connection.json"));
            }
            catch
            {
                Current = new Connection();
            }
        }
    }
}
