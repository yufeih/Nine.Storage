namespace Nine.Storage
{
    using System.IO;
    using Nine.Formatting;

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
                Current = new JsonFormatter().FromText<Connection>(File.ReadAllText("connection.json"));
            }
            catch
            {
                Current = new Connection();
            }
        }
    }
}
