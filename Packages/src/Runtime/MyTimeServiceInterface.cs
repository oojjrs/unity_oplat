using System;

namespace oojjrs.oplat
{
    public interface MyTimeServiceInterface
    {
        bool IsSynchronized { get; }
        TimeSpan LocalClockOffset { get; }
        MyTime UtcNow { get; }
    }
}
