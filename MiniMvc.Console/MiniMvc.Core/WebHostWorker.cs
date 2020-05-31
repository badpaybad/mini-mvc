using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MiniMvc.Core
{
    internal class WebHostWorker : IDisposable
    {
        Thread _thread;
        bool _isStop;
        string _domainOrIp;
        int _port;
        MiniAsyncSocket _socket;
        public WebHostWorker(string domainOrIp, int port)
        {
            _domainOrIp = domainOrIp;
            _port = port;
            _socket = new MiniAsyncSocket();
            _isStop = false;
            _thread = new Thread(Loop);
        }

        private void Loop()
        {
            while (!_isStop)
            {
                try
                {
                    _socket.StartListening(_domainOrIp, _port);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void Start()
        {
            _thread.Start();
            Console.WriteLine("WebHostWorker Started");
        }

        public void Dispose()
        {
            _isStop = true;
        }
    }
}
