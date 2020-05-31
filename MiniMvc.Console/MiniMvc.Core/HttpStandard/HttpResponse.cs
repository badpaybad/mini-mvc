using System.Collections.Generic;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpResponse
    {
        //public Dictionary<string,string> HeaderCollection { get; set; }
        public string Header { get; set; }
        public string Body { get; set; }

        public byte[] HeaderInByte { get; set; }
        public byte[] BodyInByte { get; set; }
        //public byte[] FullHttpResponseInByte { get; set; }
    }

}
