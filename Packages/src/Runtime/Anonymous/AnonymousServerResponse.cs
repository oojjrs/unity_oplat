using System;
using System.IO;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerResponse
    {
        internal enum ResultCodeEnum : byte
        {
            Success = 1,
            Unauthenticated = 2,
            NotFound = 3,
            Forbidden = 4,
            Conflict = 5,
            UnsupportedOperation = 6,
        }

        internal AnonymousServerResponse(ResultCodeEnum resultCode, byte[] content)
        {
            Content = content ?? Array.Empty<byte>();
            ResultCode = resultCode;
        }

        internal byte[] Content { get; }
        internal ResultCodeEnum ResultCode { get; }

        internal static AnonymousServerResponse Create(ResultCodeEnum resultCode)
        {
            return new AnonymousServerResponse(resultCode, Array.Empty<byte>());
        }

        internal static async Task<AnonymousServerResponse> CreateAsync(ResultCodeEnum resultCode, object content)
        {
            return new AnonymousServerResponse(resultCode, await Task.Run(() => MyNetSerializer.Serialize(content)));
        }

        internal async Task<T> GetContentAsync<T>()
        {
            return await Task.Run(() =>
            {
                using (var stream = new MemoryStream(Content))
                    return (T)MyNetDeserializer.Deserialize(stream);
            });
        }

        internal void EnsureSuccess()
        {
            if (ResultCode != ResultCodeEnum.Success)
                throw new InvalidOperationException($"Anonymous server request failed: {ResultCode}.");
        }
    }
}
