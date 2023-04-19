using MiniMvc.Core;
using MiniMvc.Core.HttpStandard;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.HostConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            //TestAsync();

            new WebHostBuilder()
                .WithDomainOrIp("0.0.0.0")
                .WithPort(8777)
                .WithWssPort(8776)
                .WithWebSocketHandle("/channel1", async (request) =>
                {

                    Console.WriteLine($"received from web browser client: {JsonConvert.SerializeObject(request)}");

                    await Task.Delay(1);

                    return null;// Return null if dont want send back to client, in clien :  websocket.onmessage = function (e) {}

                    // return new WebSocketSampleResponse()
                    // {
                    //     Message = "If want send back to client , shold come with request.Id: " + request.Body,
                    //     RequestContext = request
                    // };
                })
                .WithNumberOfWorker(3)
                .WithSocketPoolSize(int.MaxValue)
                .WithSocketBufferLength(1024 * 2)
                .WithRoutingHandleDefault(Index)
                .WithRoutingHandle(HttpMethod.Get, "", Index)
                .WithRoutingHandle(HttpMethod.Get, "/", Index)
                .WithRoutingHandle(HttpMethod.Get, "/index", Index)
                .WithRoutingHandle(HttpMethod.Get, "/about", async (request) =>
                 {
                     await Task.Delay(1);
                     return new AboutResponse()
                     {
                         Copyright = "badpaybad@gmail.com",
                         Title = "Hello, you can add request context to response by define class inherit IResponse"
                     };
                 })
               .WithRoutingHandle(HttpMethod.Get, "/sendtosocket", async (r) =>
               {
                   var msg = new WebSocketSampleResponse
                   {
                       Message = "sendtosocket " + DateTime.Now + $" {r.UrlQueryString}"
                   };

                   WebsocketServerHub.Publish("/channel1", msg);
                   return msg;
               })

                .Start();

            _ = Task.Run(async () =>
              {
                  while (true)
                  {
                      // .WithWebSocketHandle("/channel1", async (request) // to create server channel
                      // client usage /public/TestWebsocket.html

                      //server push to client usage, you can use this everywhere in your prj
                      WebsocketServerHub.Publish("/channel1", new WebSocketSampleResponse
                      {
                          Message = "Sent to web brower from backend " + DateTime.Now
                      });
                      await Task.Delay(5000);
                  }
              });

            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd == "quit")
                {
                    Environment.Exit(0);
                    return;
                }
            }
        }

        static async Task<IResponse> Index(HttpRequest request)
        {
            await Task.Delay(1);

            return new IndexResponse()
            {
                Title = "Hello world",
                RequestContext = request
            };
        }


        #region test async
        private static void TestAsync()
        {
            new Thread(async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                Console.WriteLine("AwaitTest Before: " + sw.ElapsedMilliseconds);
                await Test("AwaitTest");
                Console.WriteLine("AwaitTest After: " + sw.ElapsedMilliseconds);

                sw = Stopwatch.StartNew();
                Console.WriteLine("Run Before: " + sw.ElapsedMilliseconds);
                var t = Task.Run(() => Test("Run"));
                Console.WriteLine("Run After: " + sw.ElapsedMilliseconds);

                List<Task> list = new List<Task> { Test("List1"), Test("List2"), Test("List3") };


                sw = Stopwatch.StartNew();
                Console.WriteLine("Sleep Before: " + sw.ElapsedMilliseconds);
                Thread.Sleep(1000);
                Console.WriteLine("Sleep After: " + sw.ElapsedMilliseconds);

                sw = Stopwatch.StartNew();
                Console.WriteLine("Await t Before: " + sw.ElapsedMilliseconds);
                await t;
                Console.WriteLine("Await t After: " + sw.ElapsedMilliseconds);

                sw = Stopwatch.StartNew();
                Console.WriteLine("Await list Before: " + sw.ElapsedMilliseconds);
                await Task.WhenAll(list);
                Console.WriteLine("Await list After: " + sw.ElapsedMilliseconds);
            }).Start();
        }

        static async Task Test(string msg)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Console.WriteLine("Inside: " + msg + ": " + sw.ElapsedMilliseconds);
            await Task.Delay(1000);
            Console.WriteLine("Inside: " + msg + ": " + sw.ElapsedMilliseconds);
        }
        #endregion
    }

    public class IndexResponse : IResponse
    {
        public string Title { get; set; }
        public HttpRequest RequestContext { get; set; }
    }

    public class AboutResponse : IResponse
    {
        public string Title { get; set; }
        public string Copyright { get; set; }
    }


    public class WebSocketSampleResponse : IResponse
    {
        public string Message { get; set; }
        public HttpRequest RequestContext { get; set; }
    }
}
