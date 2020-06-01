using System;
using System.Collections.Generic;
using System.Net;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpResponse:IDisposable
    {
        public HttpStatusCode Status { get; internal set; }
        public string Header { get; internal set; }
        public string Body { get; internal set; }

        public byte[] HeaderInByte { get; internal set; }
        public byte[] BodyInByte { get; internal set; }

        public void Dispose()
        {
            
        }
    }
}
