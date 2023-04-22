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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    internal class HttpSocketAsyncHandleDispatched : IDisposable
    {
        /// <summary>
        /// keep TcpListener listent only one time for each port
        /// </summary>
        static ConcurrentDictionary<string, TcpListener> _listTcpListener = new ConcurrentDictionary<string, TcpListener>();

        bool _isStop = false;
        int _bufferLength = 8192;

        static Random _bufRandom = new Random();
        //todo play some fun,  _bufferLength=8192 commonly better
        static List<int> _bufList = new List<int> { 256, 512, 1024, 1024 * 2, 1024 * 4, 1024 * 8, 1024 * 16, 1024 * 32, 1024 * 64 };

        int _poolSize = -1;
        bool _isWss = false;

        event Action _onStart;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipOrDomain"></param>
        /// <param name="port"></param>
        /// <param name="poolSize">-1 unlimit depend on OS</param>
        /// <param name="bufferLength"></param>
        public HttpSocketAsyncHandleDispatched(string ipOrDomain, int port, int poolSize = -1, int bufferLength = 8192
            , Action onStart = null, bool isWss = false)
        {
            _bufferLength = bufferLength;
            _isWss = isWss;
            _poolSize = poolSize;
            _isStop = false;

            _onStart = onStart;

            if (string.IsNullOrEmpty(ipOrDomain)) ipOrDomain = Dns.GetHostName();

            List<IPAddress> listIp = FindIpAddressByDomain(ipOrDomain);

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
                        Console.WriteLine($"TcpListener {ip}:{port} wss: {_isWss} Listening ...");

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ip}:{port} Error: {ex.Message}");
                }
            }
        }

        private static List<IPAddress> FindIpAddressByDomain(string ipOrDomain)
        {

            if (ipOrDomain.Equals("0.0.0.0")) { return new List<IPAddress> { IPAddress.Any }; };
            if (ipOrDomain.Equals("::0")) { return new List<IPAddress> { IPAddress.IPv6Any }; };

            if (ipOrDomain.Equals("localhost")) { ipOrDomain = "127.0.0.1"; }

            IPHostEntry ipHostInfo = Dns.GetHostEntry(ipOrDomain);

            var listIp = new List<IPAddress>();

            listIp.AddRange(ipHostInfo.AddressList);

            if (listIp.Count == 0)
            {
                listIp.Add(IPAddress.Any);
            }

            return listIp;
        }

        List<Task> _listTaskNetworkAcceptIncomming = new List<Task>();
        public async Task StartAcceptIncommingAsync()
        {
            _onStart?.Invoke();


            foreach (var tcp in _listTcpListener)
            {
                _listTaskNetworkAcceptIncomming.Add(Task.Run(async () =>
                {
                    await LoopInternalStartAcceptIncommingAsync(tcp.Value);
                }));
                //await InternalStartAcceptIncommingAsync(tcp.Value);
                //listTask.Add(InternalStartAcceptIncommingAsync(tcp.Value));
            }

            await Task.WhenAll(_listTaskNetworkAcceptIncomming);
        }

        async Task LoopInternalStartAcceptIncommingAsync(TcpListener tcpListener)
        {
            //you may want to do with ssl
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?redirectedfrom=MSDN&view=netcore-3.1
            while (!_isStop)
            {
                try
                {
                    if (_isWss)
                    {
                        await WebsocketBuildRequestThenHandle(tcpListener);

                    }
                    else
                    {
                        await WebHttpBuildRequestThenHandle(tcpListener);

                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoopInternalStartAcceptIncommingAsync {ex.Message}");
                    //Console.WriteLine(JsonConvert.SerializeObject(ex));
                }
                finally
                {
                    //await Task.Delay(1);
                }
            }
        }

        async Task WebsocketBuildRequestThenHandle(TcpListener tcpListener)
        {
            //todo: can do semaphore lock here to keep total concurrent request need process at a time

            //https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
            TcpClient clientWssAccepted = await tcpListener.AcceptTcpClientAsync();

            var sendBuffSize = _bufList[_bufRandom.Next(0, 8)];
            clientWssAccepted.SendBufferSize = sendBuffSize;
            clientWssAccepted.ReceiveBufferSize = sendBuffSize;

            NetworkStream clientStream = clientWssAccepted.GetStream();
            HttpRequest wss1stRequestOfHandShake = null;

            Task twss = Task.Run(async () =>
            {
                while (!_isStop)
                {
                    try
                    {
                        if (!clientWssAccepted.Client.Connected)
                        {
                            if (wss1stRequestOfHandShake != null)
                            {
                                WebsocketServerHub.Remove(wss1stRequestOfHandShake.UrlRelative);
                            }
                            await Shutdown(clientWssAccepted.Client, wss1stRequestOfHandShake);
                            break;
                        }

                        while (!clientStream.DataAvailable) ;
                        while (clientWssAccepted.Available < 3) ; // match against "get"

                        byte[] wssReceivedBytes = new byte[clientWssAccepted.Available];
                        await clientStream.ReadAsync(wssReceivedBytes, 0, clientWssAccepted.Available);

                        var handShakeRequest = await WebsocketServerHub.DoHandShaking(clientWssAccepted, clientStream, wssReceivedBytes);

                        if (handShakeRequest != null && wss1stRequestOfHandShake == null)
                        {
                            wss1stRequestOfHandShake = handShakeRequest.Copy();
                            wss1stRequestOfHandShake.RemoteEndPoint = clientWssAccepted.Client.RemoteEndPoint.ToString();
                        }

                        var requestWss = await WebsocketServerHub.BuildNextRequestWss(wssReceivedBytes, wss1stRequestOfHandShake);
                        if (requestWss != null)
                        {
                            var wssResponse = await RoutingHandler.HandleWss(requestWss);

                            if (wssResponse != null)
                            {
                                await WebsocketServerHub.Send(clientWssAccepted, clientStream, wssResponse);
                            }
                            else
                            {
                                clientStream.Flush();
                            }
                        }

                    }
                    catch (Exception exws)
                    {
                        Console.WriteLine($"WebsocketBuildRequestThenHandle {exws.Message}");
                        //Console.WriteLine(JsonConvert.SerializeObject(exws));
                    }
                    finally
                    {
                        //await Task.Delay(1);
                    }
                }

            });
        }

        async Task WebHttpBuildRequestThenHandle(TcpListener tcpListener)
        {
            //todo: can do semaphore lock here to keep total concurrent request need process at a time

            //WebHostWorker will try accept its job
            Socket clientSocket = await tcpListener.AcceptSocketAsync();

            var sendBuffSize = _bufList[_bufRandom.Next(0, 8)];
            clientSocket.SendBufferSize = sendBuffSize;
            clientSocket.ReceiveBufferSize = sendBuffSize;

            //parse request then dispatched by RoutingHandler
            var t = Task.Run(async () =>
            {
                Task<HttpRequest> tRequest = ReadByteFromClientSocketAndBuildRequest(clientSocket);

                HttpRequest request = new HttpRequest
                {
                    CreatedAt = DateTime.Now,
                    RemoteEndPoint = clientSocket.RemoteEndPoint.ToString()
                };

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
                var processedResult = await RoutingHandler.Handle(request);

                HttpResponse response = await HttpTransform.BuildHttpResponse(processedResult, request);

                await SendResponseToClientSocket(clientSocket, request, response);

                await Shutdown(clientSocket, request);

                //HttpLogger.Log(request);

                Console.WriteLine($"{request.RemoteEndPoint}@{request.CreatedAt}=>{request.Method}:{request.Url}");
            });
        }

        private static async Task SendResponseToClientSocket(Socket socketAccepted, HttpRequest request, HttpResponse response)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (socketAccepted.Connected && response != null)
                    {
                        var rh = socketAccepted.Send(response.HeaderInByte);//, response.HeaderInByte.Length, SocketFlags.None);

                        if (rh == -1)
                        {
                            Console.WriteLine($"Can not send header to {socketAccepted.RemoteEndPoint}");
                        }
                        else
                        {
                            var rb = socketAccepted.Send(response.BodyInByte);//, response.BodyInByte.Length, SocketFlags.None);
                            if (rb == -1)
                            {
                                Console.WriteLine($"Can not send body to {socketAccepted.RemoteEndPoint}");
                            }
                        }
                    }
                }
                catch (Exception socketEx)
                {
                    Console.WriteLine($"SendResponseToClientSocket {socketEx.Message}");
                    if (request != null)
                        Console.WriteLine(JsonConvert.SerializeObject(request));
                }
            });
        }

        private static async Task Shutdown(Socket socketAccepted, HttpRequest request)
        {
            try
            {
                socketAccepted.Shutdown(SocketShutdown.Both);
                socketAccepted.Close();
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (request != null)
                    Console.WriteLine(JsonConvert.SerializeObject(request));
            }
        }

        private async Task<HttpRequest> ReadByteFromClientSocketAndBuildRequest(Socket socketAccepted)
        {

            var bufLen = socketAccepted.ReceiveBufferSize;

            byte[] bufferReceive = new byte[bufLen];

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

                    if (receiveLength <= bufLen)
                    {
                        break;
                    }
                }

                string data = Encoding.UTF8.GetString(received.ToArray());

                var tempReq = await HttpTransform.BuildHttpRequest(data);

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