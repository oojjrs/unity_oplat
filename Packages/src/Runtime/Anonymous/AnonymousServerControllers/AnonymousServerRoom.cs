using System.Collections.Generic;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerRoom
    {
        internal sealed class RoomSecret
        {
            internal RoomSecret(string password, AnonymousNetRoomProtocol.CreateResponseArgument room)
            {
                Password = password;
                Room = room;
            }

            internal string Password { get; }
            internal AnonymousNetRoomProtocol.CreateResponseArgument Room { get; }
        }

        internal sealed class State
        {
            internal readonly HashSet<string> RoomCodes = new();
            internal readonly List<RoomSecret> Rooms = new();
        }
    }
}
