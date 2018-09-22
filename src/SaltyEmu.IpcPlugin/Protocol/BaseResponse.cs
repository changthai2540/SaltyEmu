﻿using System;
using ChickenAPI.Core.IPC.Protocol;

namespace SaltyEmu.IpcPlugin.Protocol
{
    public class BaseResponse : IIpcResponse
    {
        private Guid _id;

        public Guid Id
        {
            get => _id == Guid.Empty ? _id = Guid.NewGuid() : _id;
            set => _id = value;
        }

        public Guid RequestId { get; set; }
    }
}