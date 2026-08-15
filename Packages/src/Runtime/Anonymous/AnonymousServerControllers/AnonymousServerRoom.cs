using System.Collections.Generic;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerRoom
    {
        internal sealed class RoomSecret
        {
            internal RoomSecret(string password, AnonymousServerCreateRoom.ResponseArgument room)
            {
                Password = password;
                Room = room;
            }

            internal string Password { get; }
            internal AnonymousServerCreateRoom.ResponseArgument Room { get; }
        }

        internal sealed class State
        {
            internal readonly HashSet<string> RoomCodes = new();
            internal readonly List<RoomSecret> Rooms = new();
        }
    }
}
