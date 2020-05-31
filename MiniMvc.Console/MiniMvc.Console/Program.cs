using MiniMvc.Core;
using MiniMvc.Core.HttpStandard;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiniMvc.HostConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            new WebHostBuilder().WithDomainOrIp("127.0.0.1").WithPort(8888).WithThread(1)
                .AddRoutingHandler(HttpMethod.Get, "/", Index)
                .Start();

            var cmd =Console.ReadLine();

            if (cmd == "quit")
            {
               
            }
        }

        static async Task<IResponse> Index(HttpRequest request)
        {
            await Task.Delay(0);

            return new IndexResponse()
            {
                Title = "Hello world"
            };           
        }
    }

    public class IndexResponse : IResponse
    {
        public string Title { get; set; }
    }
}
