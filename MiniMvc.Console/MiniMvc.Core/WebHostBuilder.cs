using MiniMvc.Core.HttpStandard;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    public class WebHostBuilder : IDisposable
    {
        List<WebHostWorker> _listWorker = new List<WebHostWorker>();

        int _numberOfWorker = 1;
        string _domainOrId;
        int _port;

        public WebHostBuilder WithDomainOrIp(string domainOrId="127.0.0.1")
        {
            _domainOrId = domainOrId;
            return this;
        }
        public WebHostBuilder WithPort(int port=80)
        {
            _port = port;
            return this;
        }
        public WebHostBuilder WithNumberOfWorker(int numberOfWorker)
        {
            if (numberOfWorker <= 0) numberOfWorker = 3;

            _numberOfWorker = numberOfWorker;
            return this;
        }
        public WebHostBuilder AddRoutingHandler(HttpMethod method, string urlRelative, Func<HttpRequest, Task<IResponse>> action)
        {
            RoutingHandler.Register(method, urlRelative, action);

            return this;
        }
        public void Start()
        {

            Console.WriteLine($"WebHostBuilder start at: {_domainOrId}:{_port}");

            Console.WriteLine($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");

            for (var i = 0; i < _numberOfWorker; i++)
            {
                _listWorker.Add(new WebHostWorker(_domainOrId, _port));
            }

            foreach (var w in _listWorker)
            {
                w.Start();
            }

        }

        public void Dispose()
        {
            foreach (var w in _listWorker)
            {
                w.Dispose();
            }
        }
    }
}
