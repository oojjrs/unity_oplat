using System;

namespace oojjrs.oplat
{
    public class PlatformException : Exception
    {
        public PlatformException(string message) : base(message)
        {
        }

        public PlatformException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
