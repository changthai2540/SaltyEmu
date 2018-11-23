﻿using ChickenAPI.Data.Families;
using ChickenAPI.Enums;
using ChickenAPI.Enums.Game.Character;
using ChickenAPI.Packets.Game.Server.Player;

namespace ChickenAPI.Game.Entities.Player.Extensions
{
    public static class CInfoPacketExtension
    {
        public static CInfoPacket GenerateCInfoPacket(this IPlayerEntity player)
        {
            FamilyDto family = player.Family;
            return new CInfoPacket
            {
                Name = player.Character.Name,
                Unknown1 = "-", //TODO: Find signification
                GroupId = -1,
                FamilyId = family?.Id ?? -1, // todo : family system
                FamilyName = family?.Name ?? "-",
                CharacterId = player.Character.Id,
                NameAppearance = player.NameAppearance,
                Gender = player.Character.Gender,
                HairStyle = player.Character.HairStyle,
                HairColor = player.Character.HairColor,
                Class = player.Character.Class,
                Icon = (byte)player.GetReputIcon(), // todo
                Compliment = player.Character.Compliment,
                Morph = player.MorphId,
                Invisible = player.IsInvisible,
                FamilyLevel = 0,
                SpUpgrade = player.Sp?.Upgrade ?? 0,
                ArenaWinner = player.Character.ArenaWinner
            };
        }
    }
}