using System;
using System.Collections.Generic;
using System.Net.Http;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpRequest : IDisposable
    {
        public string UrlRelative { get; internal set; }
        public string UrlQueryString { get; internal set; }
        public string Url { get; internal set; }

        public string Method { get; internal set; }

        public string Header { get; internal set; }

        public string Body { get; internal set; }

        public Dictionary<string, string> HeadlerCollection { get; internal set; } 
        public List<KeyValuePair<string, string>> QueryParamCollection { get; internal set; } 
        public string HttpVersion { get; internal set; }

        public string RemoteEndPoint { get; internal set; }

        public Exception Error { get; internal set; }

        public DateTime CreatedAt { get; set; }

        public void Dispose()
        {
        }
    }
}
