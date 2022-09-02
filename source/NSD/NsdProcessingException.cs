using System;

namespace NSD
{
    public class NsdProcessingException : Exception
    {
        public NsdProcessingException() { }
        public NsdProcessingException(string message) : base(message) { }
    }
}
