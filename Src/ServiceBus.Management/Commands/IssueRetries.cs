﻿namespace ServiceBus.Management.Commands
{
    using System.Collections.Generic;
    using NServiceBus;

    public class IssueRetries : ICommand
    {
        public List<string> MessageIds { get; set; }
    }
}