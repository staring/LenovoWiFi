﻿using System;
using System.Runtime.Serialization;

namespace HostedNetworkManager.Wlan
{
    public class WlanException : ApplicationException
    {
        public WlanException()
        {
        }

        public WlanException(string message) : base(message)
        {
        }

        public WlanException(string message, Exception inner) : base(message, inner)
        {
        }

        protected WlanException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
