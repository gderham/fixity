namespace Fixity.FixServerTests
{
    using System;
    using System.Collections.Generic;

    using Akka.Actor;
    using Akka.TestKit;
    using Akka.TestKit.Xunit2;
    using Xunit;

    using Core.Actors;
    using Core.FixMessages;
    using FixServer.Actors;

    public class FixServerActorTests : TestKit
    {
        private IActorRef _fixServerActor;
        private TestProbe _tcpServerActor;
        private TestProbe _fixInterpreterActor;

        private TimeSpan _heartbeatInterval = TimeSpan.FromMilliseconds(20);

        public FixServerActorTests()
        {
            var instrumentPrices = new Dictionary<string, double>()
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
                fixInterpreterCreator, instrumentPrices));
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
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, _heartbeatInterval));

            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);

            // 4. and starts to send heartbeat messages to client via the FixInterpreter
            _fixInterpreterActor.ExpectMsg<HeartbeatMessage>(_heartbeatInterval.Add(TimeSpan.FromMilliseconds(50)));

            // 5. FixServer shutdown causes a logout message to be sent to the client.
            _fixServerActor.Tell(new FixServerActor.Shutdown());
            _fixInterpreterActor.FishForMessage<LogoutMessage>(_=>true);

            // 6. The client confirms with a logout message
            _fixInterpreterActor.Send(_fixServerActor, new LogoutMessage("A", "B", 2));
            _tcpServerActor.FishForMessage<TcpServerActor.Shutdown>(_ => true);
        }

        [Fact]
        public void FixServer_ClientLogsOutSuccessfully_AfterClientConnectAndLogon()
        {
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, _heartbeatInterval));

            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);

            // 4. Client requests logout.
            _fixInterpreterActor.Send(_fixServerActor, new LogoutMessage("A", "B", 2));

            // 5. The server should reciprocate with a Logout messasge.
            _fixInterpreterActor.ExpectMsg<LogoutMessage>();

            // 6. The server shuts down the TCP socket
            _tcpServerActor.FishForMessage<TcpServerActor.Shutdown>(_ => true);
        }

        [Fact]
        public void FixServer_ServerSendsTestRequest_IfClientFailsToHeartbeat()
        {
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, _heartbeatInterval));

            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);

            // 4. Wait for the server to notice the lack of heartbeat and
            // send a TestRequest
            _fixInterpreterActor.FishForMessage<TestRequest>(_ => true);

            // 6. The client doesn't respond so the server shuts down the connection
            _tcpServerActor.FishForMessage<TcpServerActor.Shutdown>(_ => true);
        }

        [Fact]
        public void FixServer_ServerShutsDown_AfterClientFailsToRespondToLogout()
        {
            var shutdownWait = TimeSpan.FromMilliseconds(1100); // > logout timeout = 1s

            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, _heartbeatInterval));

            // 3. The FixServer replies with a logon message
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
            // 1. Initial client connection
            MakeClientConnection();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, _heartbeatInterval));

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

        // More tests
        // 2. Connect, logon, heartbeat not received
        // 3. Message received with incorrect seq number - log error for now
        // 4. Interpreter tests
        // 5. TcpServerActor tests - would need to swap out the actual TcpListener/client for an interface
    }
}
