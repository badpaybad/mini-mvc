using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

        public static async Task<HttpRequest> BuildHttpRequest(string receivedData)
        {
            if (string.IsNullOrEmpty(receivedData))
            {
                return new HttpRequest { Error = new Exception("No request data") };
            }
            try
            {
                HttpRequest request = new HttpRequest();
                List<string> allLine = receivedData.Split(_splitNewLine).ToList();
                var urlRequest = allLine[0].Split(_splitSpeace);

                string[] urlParam = request.Url.Split(_splitQuery);

                var tbody = Task<string>.Run(() =>
                {
                    var idxLineSplitBody = 0;
                    for (var i = 1; i < allLine.Count; i++)
                    {
                        if (string.IsNullOrEmpty(allLine[i]))
                        {
                            idxLineSplitBody = i;
                            break;
                        }
                    }
                    var body = string.Empty;
                    if (allLine.Count > idxLineSplitBody)
                    {
                        for (var i = idxLineSplitBody; i < allLine.Count; i++)
                        {
                            var l = allLine[i].Trim(new char[] { _splitSpeace, _splitCr, _splitNewLine });
                            if (!string.IsNullOrEmpty(l))
                            {
                                body = body + l + "\n";
                            }
                        }

                    }
                    return body;
                });

                var theader = Task<string>.Run(() =>
                {
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
                            break;
                        }
                    }
                    return header;
                });

                var tQueryParam = Task<Dictionary<string, string>>.Run(() =>
                {
                    Dictionary<string, string> prams = new Dictionary<string, string>();

                    if (urlParam.Length > 1)
                    {

                        request.UrlQueryString = urlParam[1];

                        var arrParam = urlParam[1].Split(_splitAnd);

                        foreach (var p in arrParam)
                        {
                            var arrVal = p.Split(_splitEqual);
                            if (arrVal.Length > 1)
                            {
                                prams.Add(arrVal[0].ToLower(), arrVal[1]);
                            }
                            else
                            {
                                prams.Add(arrVal[0].ToLower(), string.Empty);
                            }
                        }
                    }
                    return prams;
                });

                if (!_httpMmethod.TryGetValue(urlRequest[0], out HttpMethod method))
                {
                    method = HttpMethod.Get;
                }
                request.HttpVersion = urlRequest[2].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });
                request.UrlRelative = urlParam[0];
                request.Method = method;
                request.Url = urlRequest[1];
                request.Error = null;

                request.QueryParamCollection = await tQueryParam;
                request.Header = await theader;
                request.Body = await tbody;

                return request;
            }
            catch (Exception ex)
            {
                return new HttpRequest { Error = ex };
            }
        }

        public static async Task<HttpResponse> BuildResponse(IResponse response, HttpRequest request)
        {
            var tBody = Task.Run<HttpResponse>(() =>
            {
                HttpResponse tempRes = new HttpResponse();
                if (request.Error == null && response != null)
                {
                    tempRes.Body = JsonConvert.SerializeObject(response);
                }
                else
                {
                    tempRes.Body = JsonConvert.SerializeObject(request);
                }

                tempRes.BodyInByte = Encoding.UTF8.GetBytes(tempRes.Body);

                return tempRes;
            });

            HttpResponse httpResponse = new HttpResponse();
            if (request.Error == null && response != null)
            {
                httpResponse.Header = $"{request.HttpVersion} 200\r\n";
            }
            else
            {
                httpResponse.Header = $"{request.HttpVersion} 404\r\n";
            }
            httpResponse.Header += "Server: MiniMvc-v1\r\n";
            httpResponse.Header += "Content-Type: application/json\r\n";
            httpResponse.Header += "Connection: close\r\n";
            //other header will here

            var tempResponseBody = await tBody;
            httpResponse.Body = tempResponseBody.Body;
            httpResponse.BodyInByte = tempResponseBody.BodyInByte;

            httpResponse.Header += $"Content-Length: {httpResponse.BodyInByte.Length}\r\n\r\n";
            httpResponse.HeaderInByte = Encoding.UTF8.GetBytes(httpResponse.Header);

            return httpResponse;
        }

    }

}
