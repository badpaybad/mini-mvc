using MiniMvc.Core.HttpStandard;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MiniMvc.Core
{
    internal class MiniAsyncSocket
    {
        static ManualResetEvent _allDone = new ManualResetEvent(false);
        const string _eof = "<EOF>";
        public void StartListening(string ipOrDomain, int port)
        {
            if (string.IsNullOrEmpty(ipOrDomain)) ipOrDomain = Dns.GetHostName();

            IPHostEntry ipHostInfo = Dns.GetHostEntry(ipOrDomain);
            IPAddress ipAddress = IPAddress.Any;
            if (ipHostInfo.AddressList.Length > 0)
            {
                ipAddress = ipHostInfo.AddressList[0];
            }

            if (ipOrDomain.Equals("127.0.0.1")|| ipOrDomain.Equals("localhost"))
            {
                ipAddress = IPAddress.Parse("127.0.0.1");
            }

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                Console.WriteLine($"Listening {ipAddress}:{port}");

                while (true)
                {
                    _allDone.Reset();

                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    _allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        void AcceptCallback(IAsyncResult ar)
        {
            _allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            MiniSocketStateObject state = new MiniSocketStateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, MiniSocketStateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        void ReadCallback(IAsyncResult ar)
        {
            string contentReceived = string.Empty;

            MiniSocketStateObject state = (MiniSocketStateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = handler.EndReceive(ar);

            if (bytesRead <= 0)
            {
                contentReceived = state.sb.ToString();
             
                ProcessToSendBackToClient(handler, contentReceived);

                return;
            }

            state.sb.Append(Encoding.UTF8.GetString(
                state.buffer, 0, bytesRead));

            contentReceived = state.sb.ToString();

            if (contentReceived.IndexOf(_eof) > -1)
            {
                ProcessToSendBackToClient(handler, contentReceived);
            }
            else
            {
                handler.BeginReceive(state.buffer, 0, MiniSocketStateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }
        }

        private void ProcessToSendBackToClient(Socket handler, string dataReceived)
        {
            if (string.IsNullOrEmpty(dataReceived)) return;

            HttpRequest request = HttpTransform.BuildHttpRequest(dataReceived);

            Console.WriteLine($"{request.Method}:{request.UrlRelative}");

            var processed = RoutingHandler.Hanlde(request);

            HttpResponse response = HttpTransform.BuildResponse(processed.GetAwaiter().GetResult(), request);

            if (request.Method == HttpMethod.Head)
            {
                var dataHeader = Encoding.UTF8.GetBytes(response.Header);

                handler.BeginSend(dataHeader, 0, dataHeader.Length, 0,
                 new AsyncCallback(SendResponseToClient), handler);
            }
            else
            {
                var fullHttpResponse = Encoding.UTF8.GetBytes(response.Header + "\r\n" + response.Body);

                handler.BeginSend(fullHttpResponse, 0, fullHttpResponse.Length, 0,
               new AsyncCallback(SendResponseToClient), handler);
            }
        }

        private void SendResponseToClient(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }

}