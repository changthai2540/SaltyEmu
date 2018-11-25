﻿using ChickenAPI.Core.Logging;
using ChickenAPI.Game.Entities.Player;
using ChickenAPI.Game.NpcDialog.Events;
using ChickenAPI.Game.Permissions;
using ChickenAPI.Packets.Game.Client.Npcs;
using ChickenAPI.Packets.Game.Client.Shops;

namespace ChickenAPI.Game.NpcDialog.Handlers
{
    public class WopenHandler
    {
        private static readonly Logger Log = Logger.GetLogger<WopenHandler>();

        [NpcDialogHandler(2)]
        public static void UpgradeFromNpc(IPlayerEntity player, NpcDialogEventArgs args)
        {
            player.SendPacket(new WopenPacket
            {
                Type = 1,
                Unknow = 0
            });
        }

        [NpcDialogHandler(10)]
        public static void Cellon(IPlayerEntity player, NpcDialogEventArgs args)
        {
            player.SendPacket(new WopenPacket
            {
                Type = 3,
                Unknow = 0
            });
        }

        [NpcDialogHandler(12)]
        public static void Idk(IPlayerEntity player, NpcDialogEventArgs args)
        {
            player.SendPacket(new WopenPacket
            {
                Type = args.Type,
                Unknow = 0
            });
        }

        [NpcDialogHandler(14)]
        public static void Recipe(IPlayerEntity player, NpcDialogEventArgs args)
        {
            player.SendPacket(new WopenPacket
            {
                Type = 27,
                Unknow = 0
            });
            // Recipe
            /*string recipelist = "m_list 2";
                    if (npc != null)
                    {
                        List<Recipe> tps = npc.Recipes;
                        recipelist = tps.Where(s => s.Amount > 0).Aggregate(recipelist, (current, s) => current + $" {s.ItemVNum}");
                        recipelist += " -100";
                        Session.SendPacket(recipelist);
                    }
                    */
        }

        /// <summary>
        /// This method show the Menu ( TimeCircle ) for enter in x instance { arena / arena familly / raimbow battle }
        /// </summary>
        /// <param name="player"></param>
        /// <param name="args"></param>
        [PermissionsRequirements(PermissionType.NPC_DIALOG_TELEPORT)]
        [NpcDialogHandler(18)]
        public static void TimeCircle(IPlayerEntity player, NpcDialogEventArgs args)
        {
            player.SendPacket(new SentNpcReqPacket
            {
                VisualType = Enums.Game.Entity.VisualType.Character,
                VisualId = player.Id,
                Dialog = 17
            });
        }

        [NpcDialogHandler(60)]
        public static void OpenBazzar(IPlayerEntity player, NpcDialogEventArgs args)
        {
            /*
             * StaticBonusDTO medal = Session.Character.StaticBonusList.Find(s => s.StaticBonusType == StaticBonusType.BazaarMedalGold || s.StaticBonusType == StaticBonusType.BazaarMedalSilver);
                    byte Medal = 0;
                    int Time = 0;
                    if (medal != null)
                    {
                        Medal = medal.StaticBonusType == StaticBonusType.BazaarMedalGold ? (byte)MedalType.Gold : (byte)MedalType.Silver;
                        Time = (int)(medal.DateEnd - DateTime.Now).TotalHours;
                    }
                    Session.SendPacket($"wopen 32 {Medal} {Time}");
                    */
            player.SendPacket(new WopenPacket
            {
                Type = 32,
                Unknow = 1
            });
        }
    }
}