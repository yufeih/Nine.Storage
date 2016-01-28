namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Enyim.Caching;
    using Enyim.Caching.Configuration;
    using Enyim.Caching.Memcached;
    using Nine.Formatting;

    public class MemcachedStorage<T> : IDisposable, IStorage<T>
    {
        private static readonly IFormatter Formatter = new JsonFormatter();
        private readonly MemcachedClient _cache;
        private readonly string _prefix;

        public MemcachedStorage(string connection, string prefix = null)
        {
            var parts = connection.Split(',');
            var servers = from x in parts where !x.Contains("=") select x;
            var username = parts.Where(x => x.StartsWith("username=")).Select(x => x.Substring("username=".Length)).FirstOrDefault();
            var password = parts.Where(x => x.StartsWith("password=")).Select(x => x.Substring("password=".Length)).FirstOrDefault();
            var zone = parts.Where(x => x.StartsWith("zone=")).Select(x => x.Substring("zone=".Length)).FirstOrDefault();

            var configuration = new MemcachedClientConfiguration();
            foreach (var server in servers)
            {
                configuration.Servers.Add(Parse(server));
            }

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                configuration.Authentication.Type = typeof(PlainTextAuthenticator);
                configuration.Authentication.Parameters.Add("zone", zone);
                configuration.Authentication.Parameters.Add("userName", username);
                configuration.Authentication.Parameters.Add("password", password);
            }

            _cache = new MemcachedClient(configuration);
            _prefix = (prefix ?? typeof(T).ToString()) + "/";
        }

        public Task<T> Get(string key)
        {
            var result = _cache.Get(key) as byte[];
            if (result == null) return Task.FromResult<T>(default(T));
            return Task.FromResult(Formatter.FromBytes<T>(result));
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Add(string key, T value)
        {
            return _cache.Store(StoreMode.Add, key, Formatter.ToBytes(value)) ? CommonTasks.True : CommonTasks.False;
        }

        public Task Put(string key, T value)
        {
            _cache.Store(StoreMode.Set, key, Formatter.ToBytes(value));
            return Task.CompletedTask;
        }

        public Task<bool> Delete(string key)
        {
            return _cache.Remove(key) ? CommonTasks.True : CommonTasks.False;
        }

        // http://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
        private static IPEndPoint Parse(string endpointstring)
        {
            return Parse(endpointstring, -1);
        }

        private static IPEndPoint Parse(string endpointstring, int defaultport)
        {
            if (string.IsNullOrEmpty(endpointstring) || endpointstring.Trim().Length == 0)
            {
                throw new ArgumentException("Endpoint descriptor may not be empty.");
            }
            if (defaultport != -1 && (defaultport < IPEndPoint.MinPort || defaultport > IPEndPoint.MaxPort))
            {
                throw new ArgumentException(string.Format("Invalid default port '{0}'", defaultport));
            }

            string[] values = endpointstring.Split(new char[] { ':' });
            IPAddress ipaddy;
            int port = -1;

            //check if we have an IPv6 or ports
            if (values.Length <= 2) // ipv4 or hostname
            {
                if (values.Length == 1)
                    //no port is specified, default
                    port = defaultport;
                else
                    port = getPort(values[1]);

                //try to use the address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out ipaddy))
                    ipaddy = getIPfromHost(values[0]);
            }
            else if (values.Length > 2) //ipv6
            {
                //could [a:b:c]:d
                if (values[0].StartsWith("[") && values[values.Length - 2].EndsWith("]"))
                {
                    string ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipaddy = IPAddress.Parse(ipaddressstring);
                    port = getPort(values[values.Length - 1]);
                }
                else //[a:b:c] or a:b:c
                {
                    ipaddy = IPAddress.Parse(endpointstring);
                    port = defaultport;
                }
            }
            else
            {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
            }

            if (port == -1)
                throw new ArgumentException(string.Format("No port specified: '{0}'", endpointstring));

            return new IPEndPoint(ipaddy, port);
        }

        private static int getPort(string p)
        {
            int port;

            if (!int.TryParse(p, out port)
             || port < IPEndPoint.MinPort
             || port > IPEndPoint.MaxPort)
            {
                throw new FormatException(string.Format("Invalid end point port '{0}'", p));
            }

            return port;
        }

        private static IPAddress getIPfromHost(string p)
        {
            var hosts = Dns.GetHostAddresses(p);

            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException(string.Format("Host not found: {0}", p));

            return hosts[0];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_cache != null) _cache.Dispose();
            }
        }
    }
}
