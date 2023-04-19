using MiniMvc.Core.HttpStandard;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniMvc.Core
{
    public interface IResponse
    {
        /// <summary>
        /// if contentype is empty will equal to application/json, auto json seriallize response to json string
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// if contentype is empty will equal to application/json, auto json seriallize response to json string
        /// RawBytes use for other data, eg : bitmap, jpeg ... read file into bytes to send
        /// </summary>
        byte[] RawBytes{ get; set; }

    }
}
