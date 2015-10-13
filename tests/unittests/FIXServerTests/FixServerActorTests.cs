namespace Fixity.Tests.FIXServerTests
{
    using System;

    using Akka.Actor;
    using Akka.TestKit;
    using Akka.TestKit.NUnit;
    using NUnit.Framework;

    using Actors;
    using Core.Actors;
    using FIXServer.Actors;
    using FixMessages;


    [TestFixture]
    public class FixServerActorTests : TestKit
    {
        // These tests could be DRYer if the actors used the FSM base class.

        private IActorRef _fixServerActor;
        private TestProbe _tcpServerActor;
        private TestProbe _fixInterpreterActor;

        [SetUp]
        public void SetUp()
        {
            _tcpServerActor = CreateTestProbe("TcpServer");
            Func<IActorRefFactory, IActorRef> tcpServerCreator = (_) => _tcpServerActor;

            _fixInterpreterActor = CreateTestProbe("FixInterpreter");
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator = (_) => _fixInterpreterActor;

            var fixServerProps = Props.Create(() => new FixServerActor(tcpServerCreator, fixInterpreterCreator));
            _fixServerActor = ActorOf(fixServerProps, "FixServer");
        }

        [Test]
        public void FixServer_ServerLogsOutSuccessfully_AfterClientConnectAndLogon()
        { 
            var heartbeatInterval = TimeSpan.FromMilliseconds(20);

            // Ignore wiring-up type messages
            //  Each time IgnoreMessages is called it replaces the previous ignores for that test actor.
            _tcpServerActor.IgnoreMessages((message) => message is TcpServerActor.Subscribe);
            _fixInterpreterActor.IgnoreMessages((message) => message is FixInterpreterActor.SetServer
                || message is FixInterpreterActor.SetClient);

            // Test:
            // 1. Initial client connection
            _fixServerActor.Tell(new FixServerActor.StartListening());
            _tcpServerActor.ExpectMsg<TcpServerActor.StartListening>();
            _tcpServerActor.Send(_fixServerActor, new TcpServerActor.ClientConnected());
            _tcpServerActor.ExpectMsg<TcpServerActor.AcceptMessages>();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B" , 0, heartbeatInterval));
            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);
            // 4. and starts to send heartbeat messages to client via the FixInterpreter
            _fixInterpreterActor.ExpectMsg<HeartbeatMessage>(heartbeatInterval.Add(TimeSpan.FromMilliseconds(5)));
            // 5. FixServer shutdown causes a logout message to be sent to the client.
            _fixServerActor.Tell(new FixServerActor.Shutdown());
            _fixInterpreterActor.ExpectMsg<LogoutMessage>();
            // 6. The client confirms with a logout message
            _fixInterpreterActor.Send(_fixServerActor, new LogoutMessage("A", "B", 2));
            _tcpServerActor.ExpectMsg<TcpServerActor.Shutdown>();
        }

        [Test]
        public void FixServer_ServerShutsDown_AfterClientFailsToRespondToLogout()
        {
            var heartbeatInterval = TimeSpan.FromMilliseconds(20);
            var shutdownWait = TimeSpan.FromMilliseconds(1100); // > logout timeout = 1s

            // Ignore wiring-up type messages
            _tcpServerActor.IgnoreMessages((message) => message is TcpServerActor.Subscribe);
            _fixInterpreterActor.IgnoreMessages((message) => message is FixInterpreterActor.SetServer
                || message is FixInterpreterActor.SetClient || message is HeartbeatMessage);

            // Test:
            // 1. Initial client connection
            _fixServerActor.Tell(new FixServerActor.StartListening());
            _tcpServerActor.ExpectMsg<TcpServerActor.StartListening>();
            _tcpServerActor.Send(_fixServerActor, new TcpServerActor.ClientConnected());
            _tcpServerActor.ExpectMsg<TcpServerActor.AcceptMessages>();
            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            _fixInterpreterActor.Send(_fixServerActor, new LogonMessage("A", "B", 0, heartbeatInterval));
            // 3. The FixServer replies with a logon message
            _fixInterpreterActor.ExpectMsgFrom<LogonMessage>(_fixServerActor);
            // 4. FixServer shutdown causes a logout message to be sent to the client.
            _fixServerActor.Tell(new FixServerActor.Shutdown());
            _fixInterpreterActor.ExpectMsg<LogoutMessage>();
            // 5. The client fails to confirm with a logout message and after a period
            // the server shuts down.
            _tcpServerActor.ExpectMsg<TcpServerActor.Shutdown>(shutdownWait);
            //TODO: Verify the server logs an abnormal shutdown message
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
