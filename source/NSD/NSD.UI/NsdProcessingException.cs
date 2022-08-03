using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSD.UI
{
    public class NsdProcessingException : Exception
    {
        public NsdProcessingException() { }
        public NsdProcessingException(string message) : base(message) { }
    }
}
