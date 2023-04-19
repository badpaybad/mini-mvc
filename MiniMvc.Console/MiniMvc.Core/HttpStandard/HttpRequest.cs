using System;
using System.Collections.Generic;
using System.Net.Http;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpRequest : IDisposable, ICloneable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
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

        public DateTime CreatedAt { get; internal set; } = DateTime.Now;

        public object Clone()
        {
            return Copy();
        }
        public HttpRequest Copy()
        {
            return new HttpRequest
            {
                Body = this.Body,
                CreatedAt = this.CreatedAt,
                Error = this.Error,
                Header = this.Header,
                HeadlerCollection = this.HeadlerCollection,
                HttpVersion = this.HttpVersion,
                Method = this.Method,
                QueryParamCollection = this.QueryParamCollection,
                RemoteEndPoint = this.RemoteEndPoint,
                Url = this.Url,
                UrlQueryString = this.UrlQueryString,
                UrlRelative = this.UrlRelative,
            };
        }

        public void Dispose()
        {
        }
    }
}
