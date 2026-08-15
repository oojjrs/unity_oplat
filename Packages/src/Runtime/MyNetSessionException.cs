using System;

namespace oojjrs.oplat
{
    public class MyNetSessionException : Exception
    {
        internal MyNetSessionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
