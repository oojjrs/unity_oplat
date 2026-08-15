using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetLobbyServiceInterface
    {
        public interface ConfigInterface
        {
            CancellationToken CancellationToken { get; }
            int PollingDelaySeconds { get; }
        }

        public interface ResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(IEnumerable<MyNetRoomInterface> rooms);
        }

        Task RefreshAsync(ResultInterface result);
        Task StartAsync(ConfigInterface config, ResultInterface result);
        void Stop();
    }
}
