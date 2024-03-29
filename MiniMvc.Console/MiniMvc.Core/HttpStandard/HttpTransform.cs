﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpTransform
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

        static HttpTransform()
        {
            _httpMmethod[HttpMethod.Get.ToString().ToUpper()] = HttpMethod.Get;
            _httpMmethod[HttpMethod.Head.ToString().ToUpper()] = HttpMethod.Head;
            _httpMmethod[HttpMethod.Post.ToString().ToUpper()] = HttpMethod.Post;
            _httpMmethod[HttpMethod.Put.ToString().ToUpper()] = HttpMethod.Put;
            _httpMmethod[HttpMethod.Patch.ToString().ToUpper()] = HttpMethod.Patch;
            _httpMmethod[HttpMethod.Delete.ToString().ToUpper()] = HttpMethod.Delete;
            _httpMmethod[HttpMethod.Trace.ToString().ToUpper()] = HttpMethod.Trace;
            _httpMmethod[HttpMethod.Options.ToString().ToUpper()] = HttpMethod.Options;
        }

        public static async Task<HttpRequest> BuildHttpRequest(string receivedData)
        {
            if (string.IsNullOrEmpty(receivedData))
            {
                return new HttpRequest { Error = new Exception("No request data") };
            }
            try
            {
                List<string> allLine = receivedData.Split(_splitNewLine).ToList();

                var firstLineRequest = allLine[0].Split(_splitSpeace);

                var theader = Task<KeyValuePair<string, Dictionary<string, string>>>.Run(() =>
                {
                    string header = string.Empty;
                    Dictionary<string, string> headerCollection = new Dictionary<string, string>();
                    for (var i = 1; i < allLine.Count; i++)
                    {
                        var l = allLine[i].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });
                        int kIndex = l.IndexOf(_splitColon);
                        if (kIndex > 0)
                        {
                            header += l + "\n";
                            headerCollection[l.Substring(0, kIndex).ToLower().Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace })] = l.Substring(kIndex + 1).Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });
                        }
                        if (string.IsNullOrEmpty(l))
                        {
                            break;
                        }
                    }
                    return new KeyValuePair<string, Dictionary<string, string>>(header, headerCollection);
                });

                string requestUrl = firstLineRequest[1];
                string[] urlParam = requestUrl.Split(_splitQuery);

                var tQueryParam = Task<Dictionary<string, string>>.Run(() =>
                {
                    List<KeyValuePair<string, string>> prams = new List<KeyValuePair<string, string>>();

                    if (urlParam.Length > 1)
                    {
                        var arrParam = urlParam[1].Split(_splitAnd);

                        foreach (var p in arrParam)
                        {
                            var arrVal = p.Split(_splitEqual);
                            if (arrVal.Length > 1)
                            {
                                prams.Add(new KeyValuePair<string, string>(arrVal[0].ToLower().Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace }), arrVal[1].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace })));
                            }
                            else
                            {
                                prams.Add(new KeyValuePair<string, string>(arrVal[0].ToLower().Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace }), string.Empty));
                            }
                        }
                    }
                    return prams;
                });

                var tbody = Task<string>.Run(() =>
                {
                    var trimedAlline = allLine.Select(i => i.Trim(new char[] { _splitSpeace, _splitCr, _splitNewLine })).ToList();

                    var idxLineSplitBody = 0;
                    for (var i = 1; i < trimedAlline.Count; i++)
                    {
                        if (string.IsNullOrEmpty(trimedAlline[i]))
                        {
                            idxLineSplitBody = i;
                            break;
                        }
                    }
                    var body = string.Empty;
                    if (trimedAlline.Count > idxLineSplitBody)
                    {
                        for (var i = idxLineSplitBody; i < trimedAlline.Count; i++)
                        {
                            var l = trimedAlline[i];
                            if (!string.IsNullOrEmpty(l))
                            {
                                body = body + l + "\n";
                            }
                        }

                    }
                    return body;
                });

                HttpRequest request = new HttpRequest();

                request.CreatedAt = DateTime.Now;

                request.Url = requestUrl;

                request.UrlRelative = urlParam[0];

                if (urlParam.Length > 1)
                {
                    request.UrlQueryString = urlParam[1];
                }

                request.Method = firstLineRequest[0].ToUpper();

                request.HttpVersion = firstLineRequest[2].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });

                request.Error = null;

                var theaderResult = await theader;
                request.Header = theaderResult.Key;
                request.HeadlerCollection = theaderResult.Value;

                request.QueryParamCollection = await tQueryParam;

                request.Body = await tbody;

                return request;
            }
            catch (Exception ex)
            {
                return new HttpRequest { Error = ex };
            }
        }

        public static async Task<HttpResponse> BuildHttpResponse(IResponse response, HttpRequest request)
        {
            var tBody = Task.Run<KeyValuePair<string, byte[]>>(() =>
            {
                string body = string.Empty;
                byte[] bodyInByte = response.RawBytes;

                if (string.IsNullOrEmpty(response.ContentType) || string.IsNullOrWhiteSpace(response.ContentType) || response.ContentType == "application/json")
                {

                    if (request.Error == null && response != null)
                    {
                        body = JsonConvert.SerializeObject(response);
                    }
                    else
                    {
                        body = JsonConvert.SerializeObject(request);
                    }
                    bodyInByte = Encoding.UTF8.GetBytes(body);
                }

                return new KeyValuePair<string, byte[]>(body, bodyInByte);
            });

            HttpResponse httpResponse = new HttpResponse();
            if (request.Error == null && response != null)
            {
                httpResponse.Header = $"{request.HttpVersion} 200\r\n";
                httpResponse.Status = System.Net.HttpStatusCode.OK;
            }
            else
            {
                httpResponse.Header = $"{request.HttpVersion} 404\r\n";
                httpResponse.Status = System.Net.HttpStatusCode.NotFound;
            }
            httpResponse.Header += "Server: MiniMvc-v1\r\n";
            if (string.IsNullOrEmpty(response.ContentType) || string.IsNullOrWhiteSpace(response.ContentType) || response.ContentType == "application/json")
            {
                httpResponse.Header += "Content-Type: application/json\r\n";
            }
            else
            {
                httpResponse.Header += $"Content-Type: {response.ContentType}\r\n";
            }
            httpResponse.Header += "Connection: close\r\n";
            //other header will here

            var tBodyRes = await tBody;
            httpResponse.Body = tBodyRes.Key;
            httpResponse.BodyInByte = tBodyRes.Value;

            httpResponse.Header += $"Content-Length: {httpResponse.BodyInByte.Length}\r\n\r\n";
            httpResponse.HeaderInByte = Encoding.UTF8.GetBytes(httpResponse.Header);

            return httpResponse;
        }
    }
}
