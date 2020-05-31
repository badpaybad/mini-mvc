using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    internal class WebHostWorker : IDisposable
    {
        Thread _thread;
        bool _isStop;
        string _domainOrIp;
        int _port;
        SocketAsyncHandleDispatched _socket;
        public WebHostWorker(string domainOrIp, int port)
        {
            _domainOrIp = domainOrIp;
            _port = port;
            _socket = new SocketAsyncHandleDispatched(_domainOrIp, port);
            _isStop = false;
            _thread = new Thread(async () => await Loop());
        }

        private async Task Loop()
        {
            while (!_isStop)
            {
                try
                {
                    await _socket.StartListening();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    await Task.Delay(1);
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
