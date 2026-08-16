using System;
using System.Collections.Generic;
using System.Linq;
using oojjrs.oplat.anonymous.controllers;

namespace oojjrs.oplat.anonymous
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

        internal static AnonymousServerCreateRoom.ResponseArgument GetMemberResponseArgument(AnonymousServerCreateRoom.ResponseArgument room, string account)
        {
            var isHost = string.Equals(room.HostId, account, StringComparison.Ordinal);
            return room with
            {
                Fields = (room.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || isHost).ToArray(),
                Players = (room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>()).Select(player => player with
                {
                    Fields = (player.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || string.Equals(player.Id, account, StringComparison.Ordinal)).ToArray(),
                }).ToArray(),
            };
        }
    }
}
