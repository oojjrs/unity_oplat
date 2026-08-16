using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerResponse
    {
        internal AnonymousServerResponse(AnonymousTransport.ResultCodeEnum resultCode, string content)
        {
            Content = content ?? string.Empty;
            ResultCode = resultCode;
        }

        internal string Content { get; }
        internal AnonymousTransport.ResultCodeEnum ResultCode { get; }

        internal static AnonymousServerResponse Create(AnonymousTransport.ResultCodeEnum resultCode)
        {
            return new AnonymousServerResponse(resultCode, string.Empty);
        }

        internal static async Task<AnonymousServerResponse> CreateAsync(AnonymousTransport.ResultCodeEnum resultCode, object content, CancellationToken cancellationToken)
        {
            return new AnonymousServerResponse(resultCode, await AnonymousTransport.ToJsonAsync(content, cancellationToken));
        }

        internal async Task<T> FromJsonAsync<T>(CancellationToken cancellationToken)
        {
            return await AnonymousTransport.FromJsonAsync<T>(Content, cancellationToken);
        }

        internal void EnsureSuccess()
        {
            if (ResultCode != AnonymousTransport.ResultCodeEnum.Success)
                throw new InvalidOperationException($"Anonymous server request failed: {ResultCode}.");
        }
    }
}
