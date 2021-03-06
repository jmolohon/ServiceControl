﻿namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using Contexts;
    using Microsoft.AspNet.SignalR.Client;
    using Microsoft.AspNet.SignalR.Client.Transports;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Plugin.CustomChecks;

    [TestFixture]
    public class When_a_periodic_custom_check_fails : AcceptanceTest
    {
        [Test]
        public void Should_result_in_a_custom_check_failed_event()
        {
            var context = new MyContext
            {
                SignalrStarted = true
            };
            EventLogItem entry = null;

            Define(context)
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .Done(c => TryGetSingle("/api/eventlogitems/", out entry, e => e.EventType == typeof(CustomCheckFailed).Name))
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"));
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/CustomChecks.EndpointWithFailingCustomCheck")));
        }

        [Test]
        public void Should_raise_a_signalr_event()
        {
            var context = Define(() => new MyContext
            {
                Handler = () => Handler
            })
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run();

            Assert.IsNotNull(context.SignalrData);
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public string SignalrData { get; set; }
            public Func<HttpMessageHandler> Handler { get; set; }
            public bool SignalrStarted { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SignalrStarter : IWantToRunWhenBusStartsAndStops
            {
                private readonly MyContext context;
                Connection connection;

                public SignalrStarter(MyContext context)
                {
                    this.context = context;
                    connection = new Connection("http://localhost/api/messagestream")
                    {
                        JsonSerializer = Newtonsoft.Json.JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                    };
                }

                public void Start()
                {
                    connection.Received += ConnectionOnReceived;
                    connection.StateChanged += change => { context.SignalrStarted = change.NewState == ConnectionState.Connected; };

                    connection.Start(new ServerSentEventsTransport(new SignalRHttpClient(context.Handler()))).Wait();
                }

                private void ConnectionOnReceived(string s)
                {
                    if (s.IndexOf("\"CustomCheckFailed\"") > 0)
                    {
                        context.SignalrData = s;
                        context.SignalrEventReceived = true;
                    }
                }

                public void Stop()
                {
                    connection.Stop();
                }
            }
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithFailingCustomCheck()
            {
                EndpointSetup<DefaultServerWithoutAudit>().IncludeAssembly(typeof(PeriodicCheck).Assembly);
            }

            class FailingCustomCheck : PeriodicCheck
            {
                private readonly MyContext context;
                bool executed;

                public FailingCustomCheck(MyContext context) : base("MyCustomCheckId", "MyCategory", TimeSpan.FromSeconds(5))
                {
                    this.context = context;
                }

                public override CheckResult PerformCheck()
                {
                    if (executed && context.SignalrStarted)
                    {
                        return CheckResult.Failed("Some reason");
                    }

                    executed = true;

                    return CheckResult.Pass;
                }
            }
        }
    }
}