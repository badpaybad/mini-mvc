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
    internal class SocketAsyncHandleDispatched : IDisposable
    {
        TcpListener _tcpListener;

        bool _isStop = false;

        const string _eof = "<EOF>";

        public SocketAsyncHandleDispatched(string ipOrDomain, int port,int poolSize=1000)
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
            _tcpListener.Start(poolSize);

            Console.WriteLine("Socket started");
        }
        public async Task StartListening()
        {
            while (!_isStop)
            {
                try
                {
                    Socket socketAccepted = _tcpListener.AcceptSocket();


                    byte[] bReceive = new byte[1024];

                    int i = socketAccepted.Receive(bReceive);

                    if (i == 0)
                    {
                        Console.WriteLine($"Received Empty from {socketAccepted.RemoteEndPoint}");
                    }
                    else
                    {
                        Console.WriteLine($"Received from {socketAccepted.RemoteEndPoint}");
                    }

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

                    try
                    {
                        if (socketAccepted.Connected)
                            if (socketAccepted.Send(response.HeaderInByte, response.HeaderInByte.Length, SocketFlags.None) == -1)
                            {                                
                                Console.WriteLine($"Can not send header to {socketAccepted.RemoteEndPoint}");
                            }

                        if (request.Method != HttpMethod.Head)
                        {
                            if (socketAccepted.Connected)
                                if (socketAccepted.Send(response.BodyInByte, response.BodyInByte.Length, SocketFlags.None) == -1)
                                {
                                    Console.WriteLine($"Can not send body to {socketAccepted.RemoteEndPoint}");
                                }
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
            }
        }

        public void Dispose()
        {
            _isStop = true;
            _tcpListener.Stop();
        }
    }

}