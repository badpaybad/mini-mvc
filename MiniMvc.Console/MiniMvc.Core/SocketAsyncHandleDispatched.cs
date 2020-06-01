using MiniMvc.Core.HttpStandard;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    internal class SocketAsyncHandleDispatched : IDisposable
    {
        static ConcurrentDictionary<string, TcpListener> _listTcpListener = new ConcurrentDictionary<string, TcpListener>();

        bool _isStop = false;
        int _bufferLength = 2048;
        int _poolSize = -1;

        event Action _onStart;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipOrDomain"></param>
        /// <param name="port"></param>
        /// <param name="poolSize">-1 unlimit depend on OS</param>
        /// <param name="bufferLength"></param>
        public SocketAsyncHandleDispatched(string ipOrDomain, int port, int poolSize = -1, int bufferLength = 2048, Action onStart=null)
        {
            if (string.IsNullOrEmpty(ipOrDomain)) ipOrDomain = Dns.GetHostName();

            if (ipOrDomain.Equals("localhost")) { ipOrDomain = "127.0.0.1"; }

            _bufferLength = bufferLength;

            IPHostEntry ipHostInfo = Dns.GetHostEntry(ipOrDomain);

            _poolSize = poolSize;
            _isStop = false;

            _onStart = onStart;

            var listIp = new List<IPAddress>();

            listIp.AddRange(ipHostInfo.AddressList);

            if (listIp.Count == 0)
            {
                if (ipOrDomain.Equals("127.0.0.1"))
                {
                    listIp.Add(IPAddress.Parse("127.0.0.1"));
                }
                else
                {
                    listIp.Add(IPAddress.Any);
                }
            }
            foreach (var ip in listIp)
            {
                try
                {
                    var key = $"{ip}:{port}";

                    if (_listTcpListener.ContainsKey(key) == false)
                    {
                        var listener = new TcpListener(ip, port);
                        listener.Start(_poolSize);

                        _listTcpListener.TryAdd(key, listener);
                        Console.WriteLine($"{ip}:{port} Listening ...");

                        _onStart?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ip}:{port} Error: {ex.Message}");
                }
            }
        }

        public async Task StartAcceptIncommingAsync()
        {
            List<Task> listTask = new List<Task>();

            foreach (var tcp in _listTcpListener)
            {
                //listTask.Add(Task.Run(async () => await InternalStartListeningAsync(tcp.Value)));
                //await InternalStartAcceptIncommingAsync(tcp.Value);
                listTask.Add(InternalStartAcceptIncommingAsync(tcp.Value));
            }

            await Task.WhenAll(listTask);
        }

        async Task InternalStartAcceptIncommingAsync(TcpListener tcpListener)
        {
            while (!_isStop)
            {
                try
                {
                    Socket clientSocket = tcpListener.AcceptSocket();

                    Task<HttpRequest> tRequest = ReadByteFromSocketAndBuildRequest(clientSocket);

                    HttpRequest request = new HttpRequest();

                    request.RemoteEndPoint = clientSocket.RemoteEndPoint.ToString();

                    var tempRequest = await tRequest;

                    request.Body = tempRequest.Body;
                    request.Error = tempRequest.Error;
                    request.Header = tempRequest.Header;
                    request.HeadlerCollection = tempRequest.HeadlerCollection;
                    request.HttpVersion = tempRequest.HttpVersion;
                    request.Method = tempRequest.Method;
                    request.QueryParamCollection = tempRequest.QueryParamCollection;
                    request.Url = tempRequest.Url;
                    request.UrlRelative = tempRequest.UrlRelative;
                    request.UrlQueryString = tempRequest.UrlQueryString;

                    //dispatched routing here
                    var processedResult = await RoutingHandler.Hanlde(request);

                    HttpResponse response = await HttpTransform.BuildHttpResponse(processedResult, request);

                    await SendResponseToClientSocket(clientSocket, request, response);

                    await Shutdown(clientSocket, request);

                    HttpLogger.Log(request);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(JsonConvert.SerializeObject(ex));
                }
                finally
                {
                    await Task.Delay(0);
                }
            }
        }

        private static async Task SendResponseToClientSocket(Socket socketAccepted, HttpRequest request, HttpResponse response)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (socketAccepted.Connected && response != null)
                    {
                        var rh = socketAccepted.Send(response.HeaderInByte, response.HeaderInByte.Length, SocketFlags.None);

                        if (rh == -1)
                        {
                            Console.WriteLine($"Can not send header to {socketAccepted.RemoteEndPoint}");
                        }
                        else
                        {
                            var rb = socketAccepted.Send(response.BodyInByte, response.BodyInByte.Length, SocketFlags.None);
                            if (rb == -1)
                            {
                                Console.WriteLine($"Can not send body to {socketAccepted.RemoteEndPoint}");
                            }
                        }
                    }
                }
                catch (Exception socketEx)
                {
                    Console.WriteLine(socketEx.Message);
                    Console.WriteLine(JsonConvert.SerializeObject(request));
                }
            });
        }

        private static async Task Shutdown(Socket socketAccepted, HttpRequest request)
        {
            try
            {
                await Task.Delay(0);

                socketAccepted.Shutdown(SocketShutdown.Both);
                socketAccepted.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(JsonConvert.SerializeObject(request));
            }
        }

        private async Task<HttpRequest> ReadByteFromSocketAndBuildRequest(Socket socketAccepted)
        {
            byte[] bufferReceive = new byte[_bufferLength];

            using (var received = new MemoryStream())
            {
                while (true)
                {
                    int receiveLength = socketAccepted.Receive(bufferReceive);
                    if (receiveLength <= 0)
                    {
                        break;
                    }

                    received.Write(bufferReceive, 0, receiveLength);

                    if (receiveLength <= _bufferLength)
                    {
                        break;
                    }
                }

                //Console.WriteLine($"Received {received.Length} from {socketAccepted.RemoteEndPoint}");

                string data = Encoding.UTF8.GetString(received.ToArray());

                var tempReq = await HttpTransform.BuildHttpRequest(data);

                Console.WriteLine($"{tempReq.Method}:{tempReq.Url}");

                return tempReq;
            }
        }

        public void Dispose()
        {
            _isStop = true;
            foreach (var listener in _listTcpListener)
            {
                listener.Value.Stop();
            }
        }
    }

}