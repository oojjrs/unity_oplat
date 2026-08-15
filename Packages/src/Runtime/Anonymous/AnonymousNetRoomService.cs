using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly HttpClient _client;
        private readonly CancellationToken _lifetimeCancellationToken;

        internal AnonymousNetRoomService(HttpClient client, CancellationToken lifetimeCancellationToken)
        {
            _client = client;
            _lifetimeCancellationToken = lifetimeCancellationToken;
        }

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            var cancellationToken = config.CancellationToken;
            MyNetRoomInterface room;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();

                using (var content = new StringContent(new AnonymousNetRoomProtocol.RoomRequestArgument(config).ToJson(), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _client.PostAsync(AnonymousServer.GetUri(AnonymousServer.ApiCreateRoom), content, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        response.EnsureSuccessStatusCode();

                        var responseContent = await response.Content.ReadAsStringAsync();
                        cancellationToken.ThrowIfCancellationRequested();

                        room = AnonymousNetRoomProtocol.GetRoomFromJson(responseContent);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetimeCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                result.OnException(new MyNetSessionException("Failed to create anonymous room.", exception));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();
            result.OnOk(room);
        }

        Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            throw new NotImplementedException();
        }

        Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            throw new NotImplementedException();
        }

        Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            throw new NotImplementedException();
        }
    }
}
