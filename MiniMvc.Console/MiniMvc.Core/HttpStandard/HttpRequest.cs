using System;
using System.Collections.Generic;
using System.Net.Http;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpRequest:IDisposable
    {
        public string UrlRelative { get; set; }
        public string UrlQueryString { get; set; }
        public string Url { get; set; }

        public HttpMethod Method { get; set; }

        public string Header { get; set; }

        public string Body { get; set; }

        public Dictionary<string, string> HeadlerCollection { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> QueryParamCollection { get; set; } = new Dictionary<string, string>();
        public string HttpVersion { get; set; }

        public string RemoteEndPoint { get; set; }

        public Exception Error { get; set; }

        public void Dispose()
        {
        }
    }
}
