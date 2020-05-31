using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;

namespace MiniMvc.Core.HttpStandard
{
    public static class HttpTransform
    {
        const char _splitCr = '\r';
        const char _splitNewLine = '\n';
        const char _splitQuery = '?';
        const char _splitAnd = '&';
        const char _splitEqual = '=';
        const char _splitSpeace = ' ';
        const char _splitSlash = '/';
        const char _splitColon = ':';

        static Dictionary<string, HttpMethod> _httpMmethod = new Dictionary<string, HttpMethod>();

        public static HttpRequest BuildHttpRequest(string receivedData)
        {
            HttpRequest request = new HttpRequest();
            List<string> allLine = receivedData.Split(_splitNewLine).ToList();
            var urlRequest = allLine[0].Split(_splitSpeace);
            if (!_httpMmethod.TryGetValue(urlRequest[0], out HttpMethod method))
            {
                method = HttpMethod.Get;
            }
            request.Method = method;
            request.Url = urlRequest[1];

            string[] urlParam = request.Url.Split(_splitQuery);

            request.UrlRelative = urlParam[0];

            if (urlParam.Length > 1)
            {
                var arrParam = urlParam[1].Split(_splitAnd);

                foreach (var p in arrParam)
                {
                    var arrVal = p.Split(_splitEqual);
                    if (arrVal.Length > 1)
                    {
                        request.QueryParamCollection.Add(arrVal[0].ToLower(), arrVal[1]);
                    }
                    else
                    {
                        request.QueryParamCollection.Add(arrVal[0].ToLower(), string.Empty);
                    }
                }
            }

            request.HttpVersion = urlRequest[2];

            var idxLineSplitBody = 0;
            string header = string.Empty;

            for (var i = 1; i < allLine.Count; i++)
            {
                var l = allLine[i].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });
                int kIndex = l.IndexOf(_splitColon);
                if (kIndex > 0)
                {
                    header += l + "\n";
                    request.HeadlerCollection.Add(l.Substring(0, kIndex).ToLower(), l.Substring(kIndex + 1));
                }
                if (string.IsNullOrEmpty(l))
                {
                    idxLineSplitBody = i;
                    break;
                }
            }

            request.Header = header;

            if (allLine.Count > idxLineSplitBody)
            {
                var body = string.Empty;
                for (var i = idxLineSplitBody; i < allLine.Count; i++)
                {
                    var l = allLine[i].Trim(new char[] { _splitSpeace, _splitCr, _splitNewLine });
                    if (!string.IsNullOrEmpty(l))
                    {
                        body = body + l + "\n";
                    }
                }
                request.Body = body;
            }

            return request;
        }

        public static HttpResponse BuildResponse(IResponse response, HttpRequest request)
        {
            HttpResponse httpResponse = new HttpResponse();

            httpResponse.Header = request.Header;

            httpResponse.Body = JsonConvert.SerializeObject(response);

            return httpResponse;
        }
    }
}
