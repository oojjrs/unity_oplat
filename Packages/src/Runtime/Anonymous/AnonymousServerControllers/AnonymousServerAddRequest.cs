using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAddRequest
    {
        internal static async Task RunAsync(byte[] content, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            if (session == null)
                return;

            var room = roomState.Rooms.Find(secret => System.Array.Exists(secret.Room.Players ?? System.Array.Empty<AnonymousServerRoom.PlayerData>(), player => player.Id == session.Account));
            if (room == null)
                return;

            var request = await AnonymousServer.DeserializeAsync<MyNetRequest>(content);
            if (request == null)
                throw new System.FormatException("Invalid anonymous member request.");

            var players = room.Room.Players ?? System.Array.Empty<AnonymousServerRoom.PlayerData>();
            if (System.Array.Exists(players, player => player.Id == session.Account) == false)
                return;

            room.Requests.Enqueue(request);
        }
    }
}
