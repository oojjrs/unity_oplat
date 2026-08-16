using System;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerResponse
    {
        internal AnonymousServerResponse(AnonymousTransport.ResultCodeEnum resultCode, byte[] content)
        {
            Content = content ?? Array.Empty<byte>();
            ResultCode = resultCode;
        }

        internal byte[] Content { get; }
        internal AnonymousTransport.ResultCodeEnum ResultCode { get; }

        internal static AnonymousServerResponse Create(AnonymousTransport.ResultCodeEnum resultCode)
        {
            return new AnonymousServerResponse(resultCode, Array.Empty<byte>());
        }

        internal static AnonymousServerResponse Create(AnonymousTransport.ResultCodeEnum resultCode, object content)
        {
            return new AnonymousServerResponse(resultCode, AnonymousTransport.Serialize(content));
        }

        internal void EnsureSuccess()
        {
            if (ResultCode != AnonymousTransport.ResultCodeEnum.Success)
                throw new InvalidOperationException($"Anonymous server request failed: {ResultCode}.");
        }
    }
}
