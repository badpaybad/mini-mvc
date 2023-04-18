using MiniMvc.Core.HttpStandard;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    public class WebsocketServerHub
    {
        static ConcurrentDictionary<string, BlockingCollection<KeyValuePair<TcpClient, NetworkStream>>> _channel =
          new ConcurrentDictionary<string, BlockingCollection<KeyValuePair<TcpClient, NetworkStream>>>();

        internal static void Register(string urlRelative, TcpClient tcpClient, NetworkStream clientStream)
        {

            if (!_channel.TryGetValue(urlRelative, out BlockingCollection<KeyValuePair<TcpClient, NetworkStream>> clients)
                || clients == null)
            {
                clients = new BlockingCollection<KeyValuePair<TcpClient, NetworkStream>>();
                _channel.TryAdd(urlRelative, clients);
            }

            clients.Add(new KeyValuePair<TcpClient, NetworkStream>(tcpClient, clientStream));
        }

        internal static async Task<HttpRequest> DoHandShaking(TcpClient clientWssAccepted, NetworkStream clientStream, byte[] wssReceivedBytes)
        {
            string wss1stData = Encoding.UTF8.GetString(wssReceivedBytes);
            HttpRequest firstRequest = null;
            if (Regex.IsMatch(wss1stData, "^GET", RegexOptions.IgnoreCase))
            {
                firstRequest = await HttpTransform.BuildHttpRequest(wss1stData);

                WebsocketServerHub.Register(firstRequest.UrlRelative, clientWssAccepted, clientStream);

                //do handshaking
                string swk = Regex.Match(wss1stData, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
                byte[] response = Encoding.UTF8.GetBytes(
  "HTTP/1.1 101 Switching Protocols\r\n" +
  "Connection: Upgrade\r\n" +
  "Upgrade: websocket\r\n" +
  "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                await clientStream.WriteAsync(response, 0, response.Length);

                return firstRequest;
            }

            return firstRequest;
        }

        internal static async Task ReceiveAndReplyClientMessage(TcpClient clientWssAccepted, NetworkStream clientStream, byte[] wssReceivedBytes, HttpRequest firstRequestOfHandShake)
        {
            bool fin = (wssReceivedBytes[0] & 0b10000000) != 0;
            bool mask = (wssReceivedBytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

            int opcode = wssReceivedBytes[0] & 0b00001111, // expecting 1 - text message
                msglen = wssReceivedBytes[1] - 128, // & 0111 1111
                offset = 2;

            if (msglen == 126)
            {
                // was ToUInt16(bytes, offset) but the result is incorrect
                msglen = BitConverter.ToUInt16(new byte[] { wssReceivedBytes[3], wssReceivedBytes[2] }, 0);
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
            {
                Console.WriteLine("msglen == 0");
            }
            else if (mask)
            {
                byte[] decoded = new byte[msglen];
                byte[] masks = new byte[4] { wssReceivedBytes[offset], wssReceivedBytes[offset + 1], wssReceivedBytes[offset + 2], wssReceivedBytes[offset + 3] };
                offset += 4;

                for (int i = 0; i < msglen; ++i)
                    decoded[i] = (byte)(wssReceivedBytes[offset + i] ^ masks[i % 4]);

                string receivedFromClient = Encoding.UTF8.GetString(decoded);

                var wssResponse = await RoutingHandler.HandleWss(new HttpRequest()
                {
                    UrlRelative = firstRequestOfHandShake.UrlRelative,
                    Method = "wss",
                    CreatedAt = DateTime.Now,
                    Body = receivedFromClient,
                    RemoteEndPoint = clientWssAccepted.Client.RemoteEndPoint.ToString()
                });

                WebsocketServerHub.Send(clientWssAccepted, clientStream, wssResponse);

            }
            else
            {
                Console.WriteLine("ReceiveAndReplyClientMessage mask bit not set");
            }

        }

        public static void Publish(string urlRelative, IResponse response)
        {
            if (_channel.TryGetValue(urlRelative, out BlockingCollection<KeyValuePair<TcpClient, NetworkStream>> clients)
                && clients != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var client in clients)
                {
                    tasks.Add(Task.Run(() =>
                   {
                       if (!client.Key.Client.Connected)
                       {
                           WebsocketServerHub.Remove(urlRelative);
                           return;
                       }
                       WebsocketServerHub.Send(client.Key, client.Value, response);
                   }));
                }

                //want to make sure sent all to client 
                //Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
        }

        internal static void Remove(string urlRelative)
        {
            _channel.TryRemove(urlRelative, out BlockingCollection<KeyValuePair<TcpClient, NetworkStream>> olds);
            if (olds == null) return;

            try
            {
                List<Task> tasks = new List<Task>();
                foreach (var old in olds)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        if (!old.Key.Client.Connected)
                        {
                            old.Key.Client.Shutdown(SocketShutdown.Both);
                            old.Key.Client.Close();
                        }
                    }));
                }

                //Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
            catch { }

        }

        internal static void Send(TcpClient tcpClient, NetworkStream clientStream, IResponse response)
        {
            try
            {
                if (!tcpClient.Client.Connected)
                {
                    return;
                }
                var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                int frameSize = 64;

                var parts = buf.Select((b, i) => new { b, i })
                                .GroupBy(x => x.i / (frameSize - 1))
                                .Select(x => x.Select(y => y.b).ToArray())
                                .ToList();

                for (int i = 0; i < parts.Count; i++)
                {
                    byte cmd = 0;
                    if (i == 0) cmd |= 1;
                    if (i == parts.Count - 1) cmd |= 0x80;

                    clientStream.WriteByte(cmd);
                    clientStream.WriteByte((byte)parts[i].Length);
                    clientStream.Write(parts[i], 0, parts[i].Length);
                }

                clientStream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
