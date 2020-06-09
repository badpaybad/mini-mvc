﻿using MiniMvc.Core.HttpStandard;
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

        int _numberOfWorker = 3;
        string _domainOrId;
        int _port;
        int? _wssPort;
        int _socketPoolSize = 0;
        int _socketBufferLength = 2048;

        public WebHostBuilder WithDomainOrIp(string domainOrId = "127.0.0.1")
        {
            _domainOrId = domainOrId;
            return this;
        }

        public WebHostBuilder WithPort(int port = 80)
        {
            _port = port;
            return this;
        }
        public WebHostBuilder WithWssPort(int? port)
        {
            _wssPort = port;
            return this;
        }
        public WebHostBuilder WithSocketPoolSize(int socketPoolSize = 0)
        {
            if (socketPoolSize < 0) socketPoolSize = int.MaxValue;
            _socketPoolSize = socketPoolSize;
            return this;
        }

        public WebHostBuilder WithSocketBufferLength(int socketBufferLength = 2048)
        {
            if (socketBufferLength <= 0) socketBufferLength = 2048;

            _socketBufferLength = socketBufferLength;
            return this;
        }

        public WebHostBuilder WithNumberOfWorker(int numberOfWorker)
        {
            if (numberOfWorker <= 0) numberOfWorker = 3;

            _numberOfWorker = numberOfWorker;
            return this;
        }

        public WebHostBuilder WithRoutingHandle(HttpMethod method, string urlRelative, Func<HttpRequest, Task<IResponse>> action)
        {
            RoutingHandler.Register(method, urlRelative, action);

            return this;
        }

        public WebHostBuilder WithRoutingHandleDefault(Func<HttpRequest, Task<IResponse>> action)
        {
            RoutingHandler.RegisterDefaultResponse(action);

            return this;
        }
        public WebHostBuilder WithWebSocketHandle(string urlRelative, Func<HttpRequest, Task<IResponse>> action)
        {
            RoutingHandler.RegisterWss(urlRelative, action);

            return this;
        }
        public void Start()
        {
            Console.WriteLine($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"WebHostBuilder start at: {_domainOrId}:{_port}");

            for (var i = 0; i < _numberOfWorker; i++)
            {
                WebHostWorker httpWorker = new WebHostWorker(_domainOrId, _port, _socketPoolSize, _socketBufferLength, async () =>
                {
                    await RoutingHandler.Ping(_domainOrId, _port);
                }, false);

                _listWorker.Add(httpWorker);

                if (_wssPort != null)
                {
                    WebHostWorker wssWorker = new WebHostWorker(_domainOrId, _wssPort.Value, _socketPoolSize, _socketBufferLength, async () =>
                    {
                        await Task.Delay(100);
                    }, true);

                    _listWorker.Add(wssWorker);
                }
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
