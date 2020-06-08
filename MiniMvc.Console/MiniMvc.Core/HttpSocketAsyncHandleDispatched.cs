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
        static ConcurrentDictionary<string, TcpListener> _listTcpListener = new ConcurrentDictionary<string, TcpListener>();

        bool _isStop = false;
        int _bufferLength = 2048;
        int _poolSize = -1;
        bool _isWss;

        event Action _onStart;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipOrDomain"></param>
        /// <param name="port"></param>
        /// <param name="poolSize">-1 unlimit depend on OS</param>
        /// <param name="bufferLength"></param>
        public HttpSocketAsyncHandleDispatched(string ipOrDomain, int port, int poolSize = -1, int bufferLength = 2048, Action onStart = null)
        {
            _bufferLength = bufferLength;

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
                        Console.WriteLine($"{ip}:{port} Listening ...");

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

        public async Task StartAcceptIncommingAsync()
        {
            _onStart?.Invoke();

            List<Task> listTask = new List<Task>();

            foreach (var tcp in _listTcpListener)
            {
                listTask.Add(Task.Run(async () => await InternalStartAcceptIncommingAsync(tcp.Value)));
                //await InternalStartAcceptIncommingAsync(tcp.Value);
                //listTask.Add(InternalStartAcceptIncommingAsync(tcp.Value));
            }

            await Task.WhenAll(listTask);
        }

        async Task InternalStartAcceptIncommingAsync(TcpListener tcpListener)
        {
            while (!_isStop)
            {
                try
                {

                    if (_isWss)
                    {
                        //https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
                        TcpClient client = tcpListener.AcceptTcpClient();
                        NetworkStream stream = client.GetStream();

                        while (!_isStop)
                        {
                            while (!stream.DataAvailable) ;
                            while (client.Available < 3) ; // match against "get"

                            byte[] bytes = new byte[client.Available];
                            stream.Read(bytes, 0, client.Available);
                            string s = Encoding.UTF8.GetString(bytes);

                            if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                            {
                                //do handshaking
                                string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
                                byte[] response = Encoding.UTF8.GetBytes(
                  "HTTP/1.1 101 Switching Protocols\r\n" +
                  "Connection: Upgrade\r\n" +
                  "Upgrade: websocket\r\n" +
                  "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                                stream.Write(response, 0, response.Length);
                            }
                            else
                            {
                                bool fin = (bytes[0] & 0b10000000) != 0,
                  mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                                int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                                    msglen = bytes[1] - 128, // & 0111 1111
                                    offset = 2;

                                if (msglen == 126)
                                {
                                    // was ToUInt16(bytes, offset) but the result is incorrect
                                    msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                                    offset = 4;
                                }
                                else if (msglen == 127)
                                {
                                    Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                                    // i don't really know the byte order, please edit this
                                    // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                                    // offset = 10;
                                }

                                if (msglen == 0)
                                    Console.WriteLine("msglen == 0");
                                else if (mask)
                                {
                                    byte[] decoded = new byte[msglen];
                                    byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                                    offset += 4;

                                    for (int i = 0; i < msglen; ++i)
                                        decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                                    string text = Encoding.UTF8.GetString(decoded);
                                    Console.WriteLine("{0}", text);
                                }
                                else
                                    Console.WriteLine("mask bit not set");

                                Console.WriteLine();
                            }
                        }

                        continue;
                    }

                    //WebHostWorker will try accept its job
                    Socket clientSocket = tcpListener.AcceptSocket();
                    //parse request then dispatched by RoutingHandler
                    var t = Task.Run(async () =>
                    {
                        Task<HttpRequest> tRequest = ReadByteFromClientSocketAndBuildRequest(clientSocket);

                        HttpRequest request = new HttpRequest();

                        request.CreatedAt = DateTime.Now;
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

                        Console.WriteLine($"{request.RemoteEndPoint}@{request.CreatedAt}=>{request.Method}:{request.Url}");
                    });
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

        private async Task<HttpRequest> ReadByteFromClientSocketAndBuildRequest(Socket socketAccepted)
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