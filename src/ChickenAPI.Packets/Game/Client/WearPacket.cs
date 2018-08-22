﻿using ChickenAPI.Enums.Game.Items;

namespace ChickenAPI.Game.Packets.Game.Client
{
    [PacketHeader("wear")]
    public class WearPacket : PacketBase
    {
        [PacketIndex(0)]
        public InventoryType InventoryType { get; set; }

        [PacketIndex(1)]
        public short ItemSlot { get; set; }
    }
}