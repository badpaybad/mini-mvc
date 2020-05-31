﻿using System.Net.Sockets;
using System.Text;

namespace MiniMvc.Core
{
    internal class MiniSocketStateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }

}