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
        int _socketPoolSize;
        int _bufferLength;
        HttpSocketAsyncHandleDispatched _socket;
        bool _isWss;

        event Action _onSocketReady;

        public bool IsWebSocketServer { get { return _isWss; } }

        public WebHostWorker(string domainOrIp, int port, int socketPoolSize = 0, int bufferLength = 2048
            , Action onSocketReady = null, bool isWss=false)
        {
            _bufferLength = bufferLength;
            _socketPoolSize = socketPoolSize;
            _domainOrIp = domainOrIp;
            _port = port;
            _isStop = false;
            _isWss = isWss;
            _onSocketReady = onSocketReady;

            _socket = new HttpSocketAsyncHandleDispatched(_domainOrIp, _port, _socketPoolSize, _bufferLength, onSocketReady, isWss);

            _thread = new Thread(async () => await Loop());
        }

        private async Task Loop()
        {
            while (!_isStop)
            {
                try
                {
                    await _socket.StartAcceptIncommingAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    await Task.Delay(0);
                }
            }
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Dispose()
        {
            _isStop = true;
        }
    }
}
