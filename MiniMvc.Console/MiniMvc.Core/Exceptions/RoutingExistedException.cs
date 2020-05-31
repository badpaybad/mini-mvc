using System;

namespace MiniMvc.Core.Exceptions
{
    public class RoutingExistedException : Exception
    {
        public RoutingExistedException()
        {

        }
        public RoutingExistedException(string msg) : base(msg)
        {

        }
    }
}
