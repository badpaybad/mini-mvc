using MiniMvc.Core.Exceptions;
using MiniMvc.Core.HttpStandard;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MiniMvc.Core
{
    internal class RoutingHandler
    {
        static ConcurrentDictionary<string, Func<HttpRequest, Task<IResponse>>> _handler = new ConcurrentDictionary<string, Func<HttpRequest, Task<IResponse>>>();

        static List<string> _listRelativeUrl = new List<string>();

        static Func<HttpRequest, Task<IResponse>> _defaultAction;

        static object _locker = new object();

        internal static async Task<IResponse> Hanlde(HttpRequest request)
        {
            string key = string.Empty;
            if (request.Error == null && !string.IsNullOrEmpty(request.UrlRelative))
            {
                key = $"{request.Method}:{request.UrlRelative.ToLower()}";
            }

            if (!string.IsNullOrEmpty(key) && _handler.TryGetValue(key, out Func<HttpRequest, Task<IResponse>> action) && action != null)
            {
                //may be you want do chain responsibility here, middleware here
                var response = await action(request);
                return response;
            }

            if (_defaultAction != null)
            {
                var response = await _defaultAction(request);
                return response;
            }

            //may be 404 page
            return null;
        }

        public static void Register(HttpMethod httpMethod, string urlRelative, Func<HttpRequest, Task<IResponse>> action)
        {
            string key = $"{httpMethod.Method.ToUpper()}:{urlRelative.ToLower()}";

            if (_handler.ContainsKey(key)) throw new RoutingExistedException($"Existed routing: {key}");

            _listRelativeUrl.Add(urlRelative);

            _handler[key] = action;
        }

        public static void RegisterDefaultResponse(Func<HttpRequest, Task<IResponse>> action)
        {
            lock (_locker)
                _defaultAction = action;
        }

        public static async Task Ping(string ipOrDomain)
        {
            try
            {
                var url = _listRelativeUrl.Where(i => i == "" || i == "/").FirstOrDefault();

                if (ipOrDomain.IndexOf("://") <= 0)
                {
                    ipOrDomain = $"{ipOrDomain.Trim(new[] { ' ', ':', '/' })}";
                }

                url = $"http://{ipOrDomain}/{url.Trim(new[] { ' ', ':', '/' })}";

                Console.WriteLine($"Ping GET:{url}");
                Stopwatch sw = Stopwatch.StartNew();
                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(url);
                    var response = await httpClient.GetAsync(url);
                    var content = response.Content;
                }
                sw.Stop();
                Console.WriteLine($"Pong GET:{url} in: {sw.ElapsedMilliseconds} miliseconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
