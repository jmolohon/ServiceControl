﻿namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class ArchiveOperationFinalizing : IEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public ArchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
    }
}