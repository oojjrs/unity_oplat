using System;
using System.Globalization;

namespace oojjrs.oplat
{
    public readonly struct MyTime : IComparable<MyTime>, IEquatable<MyTime>
    {
        private readonly long _utcTicks;

        public bool IsDefined => _utcTicks != 0;
        public long UtcTicks => _utcTicks;

        private MyTime(long utcTicks)
        {
            _utcTicks = utcTicks;
        }

        public static MyTime FromDateTimeOffset(DateTimeOffset value)
        {
            return new MyTime(value.UtcDateTime.Ticks);
        }

        public static MyTime FromUnixTimeMilliseconds(long value)
        {
            return FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(value));
        }

        public static MyTime FromUnixTimeSeconds(long value)
        {
            return FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(value));
        }

        public static MyTime FromUtcDateTime(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The DateTime value must be UTC.", nameof(value));

            return new MyTime(value.Ticks);
        }

        public static MyTime FromUtcTicks(long value)
        {
            if ((value < DateTime.MinValue.Ticks) || (value > DateTime.MaxValue.Ticks))
                throw new ArgumentOutOfRangeException(nameof(value));

            return new MyTime(value);
        }

        public static bool TryParse(string value, out MyTime result)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var utcTicks) && (utcTicks >= DateTime.MinValue.Ticks) && (utcTicks <= DateTime.MaxValue.Ticks))
            {
                result = new MyTime(utcTicks);
                return true;
            }

            result = default;
            return false;
        }

        public static MyTime operator +(MyTime time, TimeSpan duration)
        {
            return FromUtcTicks(checked(time._utcTicks + duration.Ticks));
        }

        public static MyTime operator -(MyTime time, TimeSpan duration)
        {
            return FromUtcTicks(checked(time._utcTicks - duration.Ticks));
        }

        public static TimeSpan operator -(MyTime left, MyTime right)
        {
            return TimeSpan.FromTicks(checked(left._utcTicks - right._utcTicks));
        }

        public static bool operator <(MyTime left, MyTime right)
        {
            return left._utcTicks < right._utcTicks;
        }

        public static bool operator <=(MyTime left, MyTime right)
        {
            return left._utcTicks <= right._utcTicks;
        }

        public static bool operator >(MyTime left, MyTime right)
        {
            return left._utcTicks > right._utcTicks;
        }

        public static bool operator >=(MyTime left, MyTime right)
        {
            return left._utcTicks >= right._utcTicks;
        }

        public static bool operator ==(MyTime left, MyTime right)
        {
            return left._utcTicks == right._utcTicks;
        }

        public static bool operator !=(MyTime left, MyTime right)
        {
            return left._utcTicks != right._utcTicks;
        }

        public override bool Equals(object obj)
        {
            return obj is MyTime other && (this == other);
        }

        public override int GetHashCode()
        {
            return _utcTicks.GetHashCode();
        }

        public override string ToString()
        {
            return _utcTicks.ToString(CultureInfo.InvariantCulture);
        }

        int IComparable<MyTime>.CompareTo(MyTime other)
        {
            return _utcTicks.CompareTo(other._utcTicks);
        }

        bool IEquatable<MyTime>.Equals(MyTime other)
        {
            return this == other;
        }

        public DateTimeOffset ToDateTimeOffset()
        {
            return new DateTimeOffset(ToUtcDateTime());
        }

        public DateTime ToLocalDateTime()
        {
            return ToUtcDateTime().ToLocalTime();
        }

        public long ToUnixTimeMilliseconds()
        {
            return ToDateTimeOffset().ToUnixTimeMilliseconds();
        }

        public long ToUnixTimeSeconds()
        {
            return ToDateTimeOffset().ToUnixTimeSeconds();
        }

        public DateTime ToUtcDateTime()
        {
            return new DateTime(_utcTicks, DateTimeKind.Utc);
        }
    }
}
