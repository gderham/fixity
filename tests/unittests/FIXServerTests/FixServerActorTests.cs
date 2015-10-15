namespace Fixity.Tests.FIXServerTests
{
    using System;

    using Akka.Actor;
    using Akka.TestKit;
    using Akka.TestKit.Xunit2;
    using Xunit;

    using Actors;
    using Core.Actors;
    using FIXServer.Actors;
    using FixMessages;
    using System.Collections.Generic;

    public class FixServerActorTests : TestKit
    {
        private IActorRef _fixServerActor;
        private TestProbe _tcpServerActor;
        private TestProbe _fixInterpreterActor;

        public FixServerActorTests()
        {
            // Some invented FX spot rates
            var prices = new Dictionary<string, double>()
            {
                { "USDGBP", 0.65575 },
                { "USDJPY", 119.75 }
            };

            _tcpServerActor = CreateTestProbe("TcpServer");
            _tcpServerActor.IgnoreMessages(m => m is TcpServerActor.Subscribe); // Ignore wiring-up messages
            Func<IActorRefFactory, IActorRef> tcpServerCreator = (_) => _tcpServerActor;

            _fixInterpreterActor = CreateTestProbe("FixInterpreter");
            _fixInterpreterActor.IgnoreMessages(m => m is FixInterpreterActor.SetServer ||
                m is FixInterpreterActor.SetClient); // Ignore wiring-up messages
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator = (_) => _fixInterpreterActor;

            var fixServerProps = Props.Create(() => new FixServerActor(tcpServerCreator,
                fixInterpreterCreator, prices));
            _fixServerActor = ActorOf(fixServerProps, "FixServer");
        }

        private void MakeClientConnection()
        {
            _fixServerActor.Tell(new FixServerActor.StartListening());
            _tcpServerActor.FishForMessage<TcpServerActor.StartListening>(_=>true);
            _tcpServerActor.Send(_fixServerActor, new TcpServerActor.ClientConnected());
            _tcpServerActor.ExpectMsg<TcpServerActor.AcceptMessages>();
        }

        [Fact]
        public void FixServer_ServerLogsOutSuccessfully_AfterClientConnectAndLogon()
        {
            var heartbeatInterval = TimeSpan.FromMilliseconds(20);

            // Test:
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, heartbeatInterval));

            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);

            // 4. and starts to send heartbeat messages to client via the FixInterpreter
            _fixInterpreterActor.ExpectMsg<HeartbeatMessage>(heartbeatInterval.Add(TimeSpan.FromMilliseconds(50)));

            // 5. FixServer shutdown causes a logout message to be sent to the client.
            _fixServerActor.Tell(new FixServerActor.Shutdown());
            _fixInterpreterActor.FishForMessage<LogoutMessage>(_=>true);

            // 6. The client confirms with a logout message
            _fixInterpreterActor.Send(_fixServerActor, new LogoutMessage("A", "B", 2));
            _tcpServerActor.FishForMessage<TcpServerActor.Shutdown>(_ => true);
        }

        [Fact]
        public void FixServer_ServerShutsDown_AfterClientFailsToRespondToLogout()
        {
            var heartbeatInterval = TimeSpan.FromMilliseconds(20);
            var shutdownWait = TimeSpan.FromMilliseconds(1100); // > logout timeout = 1s

            // Test:
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, heartbeatInterval));

            // 3. The FixServer replies with a logon message
            //_fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);
            _fixInterpreterActor.FishForMessage<LogonMessage>(_ => true);
            // We use FishForMessage to ignore any heartbeat messages

            // 4. FixServer shutdown causes a logout message to be sent to the client.
            _fixServerActor.Tell(new FixServerActor.Shutdown());

            // 5. The client fails to confirm with a logout message and after a period
            // the server shuts down.
            _tcpServerActor.FishForMessage<TcpServerActor.Shutdown>(_ => true);

            //TODO: Verify the server logs an abnormal shutdown message
        }

        [Fact]
        public void FixServer_ReturnsQuote_ForClientsQuoteRequest()
        {
            var heartbeatInterval = TimeSpan.FromMilliseconds(20);

            // Test:
            // 1. Initial client connection
            MakeClientConnection();
            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, heartbeatInterval));

            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.FishForMessage<LogonMessage>(_=>true);

            // 4. Client requests quote
            _fixInterpreterActor.Send(_fixServerActor, new QuoteRequest("A", "B", 1, "Quote1", "USDJPY"));

            // 5. FixServer returns the corresponding quote
            _fixInterpreterActor.FishForMessage<Quote>(m => m.QuoteReqID == "Quote1");
        }

        public void FixServer_SendsResendRequest_IfGapInClientMessageSequence()
        {



        }

        // Tests
        // 2. Connect, logon, heartbeat, client logoff, shutdown
        // 3. Connect, logon, heartbeat not received
        // 4. Message received with incorrect seq number
        // 5. RFQ message received - respond with quote
        // + interpreter tests
        // + don't bother about TcpServer tests
        // + int tests with server and clients
    }
}
