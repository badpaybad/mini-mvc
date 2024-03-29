﻿using MiniMvc.Core.HttpStandard;
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

        internal static async Task<HttpRequest> BuildNextRequestWss(byte[] wssReceivedBytes
        , HttpRequest firstRequestOfHandShake)
        {
            await Task.Yield();
            //https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
            bool fin = (wssReceivedBytes[0] & 0b10000000) != 0,
                     mask = (wssReceivedBytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
            int opcode = wssReceivedBytes[0] & 0b00001111; // expecting 1 - text message
            ulong offset = 2;
            ulong msglen = 0;
            int tempMsgLen = wssReceivedBytes[1] & 0b01111111;
            msglen = (ulong)tempMsgLen;
            if (msglen == 126)
            {
                // bytes are reversed because websocket will print them in Big-Endian, whereas
                // BitConverter will want them arranged in little-endian on windows
                msglen = BitConverter.ToUInt16(new byte[] { wssReceivedBytes[3], wssReceivedBytes[2] }, 0);
                offset = 4;
            }
            else if (msglen == 127)
            {
                // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                // websocket frame available through client.Available).
                msglen = BitConverter.ToUInt64(new byte[] { wssReceivedBytes[9], wssReceivedBytes[8], wssReceivedBytes[7], wssReceivedBytes[6], wssReceivedBytes[5], wssReceivedBytes[4], wssReceivedBytes[3], wssReceivedBytes[2] }, 0);
                offset = 10;
            }

            if (msglen == 0)
            {
                Console.WriteLine("msglen == 0");
                return null;
            }
            else if (mask)
            {
                byte[] decoded = new byte[msglen];
                byte[] masks = new byte[4] { wssReceivedBytes[offset], wssReceivedBytes[offset + 1], wssReceivedBytes[offset + 2], wssReceivedBytes[offset + 3] };
                offset += 4;

                for (ulong i = 0; i < msglen; ++i)
                    decoded[i] = (byte)(wssReceivedBytes[offset + i] ^ masks[i % 4]);

                string receivedFromClient = Encoding.UTF8.GetString(decoded);

                var requestWss = firstRequestOfHandShake.Copy();
                requestWss.UrlRelative = firstRequestOfHandShake.UrlRelative;
                requestWss.Method = "wss";
                requestWss.CreatedAt = DateTime.Now;
                requestWss.Body = receivedFromClient;

                return requestWss;

            }
            else
            {
                Console.WriteLine("ReceiveAndReplyClientMessage mask bit not set");
                return null;
            }

        }

        static void Register(string urlRelative, TcpClient tcpClient, NetworkStream clientStream)
        {

            if (!_channel.TryGetValue(urlRelative, out BlockingCollection<KeyValuePair<TcpClient, NetworkStream>> clients)
                || clients == null)
            {
                clients = new BlockingCollection<KeyValuePair<TcpClient, NetworkStream>>();
                _channel.TryAdd(urlRelative, clients);
            }

            clients.Add(new KeyValuePair<TcpClient, NetworkStream>(tcpClient, clientStream));
        }

        public static async Task RegisterHandle(string urlRelative, Func<HttpRequest, Task<IResponse>> action)
        {
            await Task.Yield();
            RoutingHandler.RegisterWss(urlRelative, action);
        }

        public static async Task Publish(string urlRelative, IResponse response)
        {
            await Task.Yield();
            if (_channel.TryGetValue(urlRelative, out BlockingCollection<KeyValuePair<TcpClient, NetworkStream>> clients)
                && clients != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var client in clients)
                {
                    tasks.Add(Task.Run(async () =>
                   {
                       if (!client.Key.Client.Connected)
                       {
                           WebsocketServerHub.Remove(urlRelative);
                           return;
                       }
                      await  WebsocketServerHub.Send(client.Key, client.Value, response);
                   }));
                }

                //want to make sure sent all to client 
                //Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
        }

        public static void Remove(string urlRelative)
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

        internal static async Task Send(TcpClient tcpClient, NetworkStream clientStream, IResponse response)
        {
            await Task.Yield();
            try
            {
                if (!tcpClient.Client.Connected)
                {
                    return;
                }
                var buf = response.RawBytes;
                if (string.IsNullOrEmpty(response.ContentType) || string.IsNullOrWhiteSpace(response.ContentType) || response.ContentType == "application/json")
                {
                    buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                }

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
