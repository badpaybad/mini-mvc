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

        int _numberOfThread = 1;
        string _domainOrId;
        int _port;

        public WebHostBuilder WithDomainOrIp(string domainOrId)
        {
            _domainOrId = domainOrId;
            return this;
        }
        public WebHostBuilder WithPort(int port)
        {
            _port = port;
            return this;
        }
        public WebHostBuilder WithThread(int numberOfThread)
        {
            if (numberOfThread <= 0) numberOfThread = 1;

            _numberOfThread = numberOfThread;
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

            for (var i = 0; i < _numberOfThread; i++)
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
