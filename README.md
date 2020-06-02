# mini-mvc
Try to build something dispatched url to an function

# startup project 
MiniMvc.HostConsole

# do simple parse http request then do simple response in json
use c# TcpListener


# compare to asp.net core
MiniMvc.AspDotnetCore

# usage check program.cs
Simply defind your routing and function to handle it, your response must inherit IResponse


                 public class IndexResponse : IResponse
                {
                    public string Title { get; set; }
                    public HttpRequest RequestContext { get; set; }
                }



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

                 static async Task<IResponse> Index(HttpRequest request)
                {
                    await Task.Delay(1);

                    return new IndexResponse()
                    {
                        Title = "Hello world",
                        RequestContext = request
                    };
                }
