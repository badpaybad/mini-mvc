using MiniMvc.Core;
using MiniMvc.Core.HttpStandard;
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
                .WithDomainOrIp("127.0.0.1")
                .WithPort(8888)
                .WithNumberOfWorker(3)
                .WithSocketPoolSize(int.MaxValue)
                .WithSocketBufferLength(1024*2)
                .WithRoutingHandlerDefault(Index)
                .WithRoutingHandler(HttpMethod.Get, "", Index)
                .WithRoutingHandler(HttpMethod.Get, "/", Index)
                .WithRoutingHandler(HttpMethod.Get, "/index", Index)
                .WithRoutingHandler(HttpMethod.Get, "/about", async (request) =>
                 {
                     await Task.Delay(0);
                     return new IndexResponse()
                     {
                         Title = "badpaybad@gmail.com",
                         RequestContext = request

                     };
                 })                
                .Start();

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
}
