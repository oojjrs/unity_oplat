using System;

namespace oojjrs.oplat
{
    public sealed class PlatformAuthenticationTicket : IDisposable
    {
        readonly Action _release;

        public string Identity { get; }
        public string Provider { get; }
        public string Value { get; private set; }

        public PlatformAuthenticationTicket(string provider, string identity, string value, Action release = null)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _release = release;
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        void IDisposable.Dispose()
        {
            if (string.IsNullOrEmpty(Value))
                return;

            Value = string.Empty;
            _release?.Invoke();
        }
    }
}
