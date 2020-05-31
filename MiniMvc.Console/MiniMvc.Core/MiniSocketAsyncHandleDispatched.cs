using MiniMvc.Core.HttpStandard;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    internal class MiniSocketAsyncHandleDispatched : IDisposable
    {
        TcpListener _tcpListener;

        bool _isStop = false;

        const string _eof = "<EOF>";

        public MiniSocketAsyncHandleDispatched(string ipOrDomain, int port)
        {
            if (string.IsNullOrEmpty(ipOrDomain)) ipOrDomain = Dns.GetHostName();

            IPHostEntry ipHostInfo = Dns.GetHostEntry(ipOrDomain);
            IPAddress ipAddress = IPAddress.Any;
            if (ipHostInfo.AddressList.Length > 0)
            {
                ipAddress = ipHostInfo.AddressList[0];
            }

            if (ipOrDomain.Equals("127.0.0.1") || ipOrDomain.Equals("localhost"))
            {
                ipAddress = IPAddress.Parse("127.0.0.1");
            }
            _isStop = false;
            //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            //Socket serverListener = new Socket(ipAddress.AddressFamily,
            //    SocketType.Stream, ProtocolType.Tcp);

            _tcpListener = new TcpListener(ipAddress, port);
            _tcpListener.Start();
        }
        public async Task StartListening()
        {
            while (!_isStop)
            {
                try
                {
                    Socket socketAccepted = _tcpListener.AcceptSocket();

                    Console.WriteLine($"Received Connection from {socketAccepted.RemoteEndPoint}");

                    byte[] bReceive = new byte[1024 * 1024 * 2];

                    int i = socketAccepted.Receive(bReceive);

                    byte[] requestData = new byte[i];

                    for (int j = 0; j < i; j++)
                    {
                        requestData[j] = bReceive[j];
                    }

                    HttpRequest request = HttpTransform.BuildHttpRequest(Encoding.UTF8.GetString(requestData));

                    request.RemoteEndPoint = socketAccepted.RemoteEndPoint.ToString();

                    Console.WriteLine($"{request.Method}:{request.UrlRelative}");

                    var processedResult = await RoutingHandler.Hanlde(request);

                    HttpResponse response = HttpTransform.BuildResponse(processedResult, request);

                    Console.WriteLine("RESPONSE");
                    Console.WriteLine(response.Body);

                    try
                    {
                        if (socketAccepted.Connected)
                            socketAccepted.Send(response.HeaderInByte);

                        if (request.Method != HttpMethod.Head)
                        {
                            if (socketAccepted.Connected)
                                socketAccepted.Send(response.BodyInByte);
                        }

                    }
                    catch (SocketException socketEx)
                    {
                        Console.WriteLine(socketEx.Message);
                        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(request));
                    }

                    try
                    {
                        socketAccepted.Shutdown(SocketShutdown.Both);
                        socketAccepted.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(JsonConvert.SerializeObject(request));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(JsonConvert.SerializeObject(ex));
                }
                finally
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void Dispose()
        {
            _isStop = true;
            _tcpListener.Stop();
        }
    }

}