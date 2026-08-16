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
            var isHost = room.HostId == account;
            return room with
            {
                Fields = (room.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || isHost).ToArray(),
                Players = (room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>()).Select(player => player with
                {
                    Fields = (player.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || player.Id == account).ToArray(),
                }).ToArray(),
            };
        }

        internal static AnonymousServerCreateRoom.FieldData[] MergeFields(AnonymousServerCreateRoom.FieldData[] fields, AnonymousServerCreateRoom.FieldData[] updatedFields)
        {
            fields ??= Array.Empty<AnonymousServerCreateRoom.FieldData>();
            updatedFields ??= Array.Empty<AnonymousServerCreateRoom.FieldData>();
            if (updatedFields.Length == 0)
                return fields;

            return fields.Where(field => updatedFields.Any(updatedField => updatedField.Key == field.Key) == false).Concat(updatedFields).ToArray();
        }
    }
}
