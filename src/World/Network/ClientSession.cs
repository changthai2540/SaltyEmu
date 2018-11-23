﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Autofac;
using ChickenAPI.Core.i18n;
using ChickenAPI.Core.IoC;
using ChickenAPI.Core.Logging;
using ChickenAPI.Data.Character;
using ChickenAPI.Game.Data.AccessLayer.Server;
using ChickenAPI.Game.Entities.Player;
using ChickenAPI.Game.Network;
using ChickenAPI.Game.PacketHandling;
using ChickenAPI.Packets;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Groups;

namespace World.Network
{
    public class ClientSession : ChannelHandlerAdapter, ISession
    {
        private const byte PACKET_SPLIT_CHARACTER = 0xFF;

        private static readonly Logger Log = Logger.GetLogger<ClientSession>();
        private static Guid _worldServerId;
        private static IPacketFactory _packetFactory;
        private static IPacketHandler _packetHandler;
        private readonly IChannel _channel;

        #region Members

        public long CharacterId { get; private set; }
        public bool IsAuthenticated => Account != null;

        public int SessionId { get; set; }
        public IPAddress Ip { get; private set; }
        public AccountDto Account { get; private set; }
        public IPlayerEntity Player { get; private set; }
        public LanguageKey Language => LanguageKey.EN;

        public int LastKeepAliveIdentity { get; set; }

        // TODO REFACTOR THAT SHIT
        private int? _waitForPacketsAmount;
        private readonly IList<string> _waitForPacketList = new List<string>();

        public static void SetPacketFactory(IPacketFactory packetFactory)
        {
            _packetFactory = packetFactory;
        }

        public static void SetPacketHandler(IPacketHandler packetHandler)
        {
            _packetHandler = packetHandler;
        }

        public static void SetWorldServerId(Guid id)
        {
            _worldServerId = id;
        }

        public ClientSession(IChannel channel) => _channel = channel;

        #endregion

        #region Methods

        private static volatile IChannelGroup _group;
        private static ISessionService _sessionService;

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            IChannelGroup g = _group;
            if (g == null)
            {
                lock(_channel)
                {
                    if (_group == null)
                    {
                        g = _group = new DefaultChannelGroup(context.Executor);
                        _sessionService = ChickenContainer.Instance.Resolve<ISessionService>();
                    }
                }
            }

            g.Add(context.Channel);
            Ip = (_channel.RemoteAddress as IPEndPoint).Address.MapToIPv4();

#if DEBUG
            Log.Debug($"[{Ip}][SOCKET_ACCEPT]");
#endif
            SessionId = 0;
        }


        public void InitializeCharacterId(long id)
        {
            CharacterId = id;
        }

        public void InitializeEntity(IPlayerEntity ett)
        {
            Player = ett;
        }

        public void InitializeAccount(AccountDto account)
        {
            Account = account;
        }

        private static readonly string[] BlacklistedDebug = { "mv", "cond", "in", "st" };

        public void SendPacket<T>(T packet) where T : IPacket
        {
            if (packet == null)
            {
                return;
            }

            string tmp = _packetFactory.Serialize(packet);

#if DEBUG
            if (BlacklistedDebug.All(s => !tmp.StartsWith(s)))
            {
                Log.Debug($"[{Ip}][SOCKET_WRITE] {tmp}");
            }
#endif

            _channel.WriteAsync(tmp);
            _channel.Flush();
        }

        public void SendPackets<T>(IEnumerable<T> packets) where T : IPacket
        {
            if (packets == null)
            {
                return;
            }

            foreach (T packet in packets)
            {
                if (packet == null)
                {
                    continue;
                }

                string tmp = _packetFactory.Serialize(packet);
#if DEBUG
                if (BlacklistedDebug.All(s => !tmp.StartsWith(s)))
                {
                    Log.Debug($"[{Ip}][SOCKET_WRITE] {tmp}");
                }
#endif

                _channel.WriteAsync(tmp);
            }

            _channel.Flush();
        }

        public void SendPackets(IEnumerable<IPacket> packets)
        {
            foreach (IPacket packet in packets)
            {
                if (packet == null)
                {
                    continue;
                }

                string tmp = _packetFactory.Serialize(packet);
#if DEBUG
                if (BlacklistedDebug.All(s => !tmp.StartsWith(s)))
                {
                    Log.Debug($"[{Ip}][SOCKET_WRITE] {tmp}");
                }
#endif

                _channel.WriteAsync(tmp);
            }

            _channel.Flush();
        }


        public void Disconnect()
        {
#if DEBUG
            Log.Debug($"[{Ip}][SOCKET_RELEASE]");
#endif

            Log.Info($"[DISCONNECT] {Ip}");
            Player?.CurrentMap.UnregisterEntity(Player);
            Player?.Save();
            _sessionService.UnregisterSession(SessionId);
            _channel.DisconnectAsync().Wait();
        }


        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            Disconnect();
            SessionManager.Instance.UnregisterSession(context.Channel.Id.AsLongText());
            context.CloseAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Disconnect();

#if DEBUG
            Log.Error($"[{Ip}][SOCKET_EXCEPTION]", exception);
#endif
            context.CloseAsync();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (!(message is string buff))
            {
                return;
            }

            if (SessionId == 0)
            {
                BufferToUnauthedPackets(buff, context);
                return;
            }

            BufferToPackets(buff);
        }

        private void BufferToUnauthedPackets(string packets, IChannelHandlerContext context)
        {
            string[] sessionParts = packets.Split(' ');
            if (sessionParts.Length == 0)
            {
                return;
            }

            if (!int.TryParse(sessionParts[0], out int lastKeepAlive))
            {
                Disconnect();
            }

            LastKeepAliveIdentity = lastKeepAlive;
            if (sessionParts.Length < 2)
            {
                return;
            }

            if (!int.TryParse(sessionParts[1].Split('\\').FirstOrDefault(), out int sessid))
            {
                return;
            }

            SessionId = sessid;
            SessionManager.Instance.RegisterSession(context.Channel.Id.AsLongText(), SessionId);
            // CLIENT ARRIVED
            if (!_waitForPacketsAmount.HasValue)
            {
                TriggerHandler("EntryPoint", string.Empty, false);
            }
        }

        private bool WaitForPackets(string packetstring, IReadOnlyList<string> packetsplit)
        {
            _waitForPacketList.Add(packetstring);
            string[] packetssplit = packetstring.Split(' ');
            if (packetssplit.Length > 3 && packetsplit[1] == "DAC")
            {
                _waitForPacketList.Add("0 CrossServerAuthenticate");
            }

            if (_waitForPacketList.Count != _waitForPacketsAmount)
            {
                return true;
            }

            _waitForPacketsAmount = null;
            string queuedPackets = string.Join(" ", _waitForPacketList.ToArray());
            string header = queuedPackets.Split(' ', '^')[1];
            TriggerHandler(header, queuedPackets, true);
            _waitForPacketList.Clear();
            return false;
        }

        private void KeepAliveCheck(int nextKeepAlive)
        {
            if (nextKeepAlive == 0)
            {
                if (LastKeepAliveIdentity == ushort.MaxValue)
                {
                    LastKeepAliveIdentity = nextKeepAlive;
                }
            }
            else
            {
                LastKeepAliveIdentity = nextKeepAlive;
            }
        }

        private void BufferToPackets(string packets)
        {
            foreach (string packet in packets.Split((char)PACKET_SPLIT_CHARACTER, StringSplitOptions.RemoveEmptyEntries))
            {
                string packetstring = packet.Replace('^', ' ');
                string[] packetsplit = packetstring.Split(' ');

                // keep alive
                string nextKeepAliveRaw = packetsplit[0];
                if (!int.TryParse(nextKeepAliveRaw, out int nextKeepAlive) && nextKeepAlive != (LastKeepAliveIdentity + 1))
                {
                    Disconnect();
                    return;
                }

                KeepAliveCheck(nextKeepAlive);

                if (_waitForPacketsAmount.HasValue)
                {
                    if (WaitForPackets(packetstring, packetsplit))
                    {
                        continue;
                    }

                    return;
                }

                if (packetsplit.Length <= 1)
                {
                    continue;
                }

                if (packetsplit[1].Length >= 1 && ChatDelimiter.Any(s => s == packetsplit[1][0]))
                {
                    packetsplit[1] = packetsplit[1][0].ToString();
                    packetstring = packet.Insert(packet.IndexOf(' ') + 2, " ");
                }

                if (packetsplit[1] != "0")
                {
                    TriggerHandler(packetsplit[1].Replace("#", ""), packetstring, false);
                }
            }
        }

        private static readonly char[] ChatDelimiter = { '/', ':', ';' };

        private void GameHandler(string packetHeader, string packet)
        {
            GamePacketHandler gameHandler = _packetHandler.GetGamePacketHandler(packetHeader);
            if (gameHandler == null)
            {
#if DEBUG
                Log.Warn($"[{Ip}][HANDLER_NOT_FOUND] {packetHeader}");
#endif
                return;
            }

#if DEBUG
            Log.Debug($"[{Ip}][HANDLING] {packet}");
#endif

            IPacket packetT = _packetFactory.Deserialize(packet, gameHandler.PacketType, IsAuthenticated);
            _packetHandler.Handle((packetT, Player));
        }

        private void CharacterHandler(string packet, CharacterScreenPacketHandler handler)
        {
            IPacket deserializedPacketBase = _packetFactory.Deserialize(packet, handler.PacketType, IsAuthenticated);
            _packetHandler.Handle((deserializedPacketBase, this));
        }

        private void TriggerHandler(string packetHeader, string packet, bool force)
        {
            if (Player != null)
            {
                GameHandler(packetHeader, packet);
                return;
            }


            CharacterScreenPacketHandler handler = _packetHandler.GetCharacterScreenPacketHandler(packetHeader);
            if (handler == null)
            {
#if DEBUG
                Log.Warn($"[{Ip}][HANDLER_NOT_FOUND] {packetHeader}");
#endif
                return;
            }

            if (force)
            {
                CharacterHandler(packet, handler);
                return;
            }

            if (handler.PacketHeader != null && handler.PacketHeader.Amount > 1 && !_waitForPacketsAmount.HasValue)
            {
                _waitForPacketsAmount = handler.PacketHeader.Amount;
                _waitForPacketList.Add(packet != string.Empty ? packet : $"1 {packetHeader} ");
                return;
            }

            CharacterHandler(packet, handler);
        }

        #endregion
    }
}