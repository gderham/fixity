namespace Fixity.Tests.FIXServerTests
{
    using System;

    using Akka.Actor;
    using Akka.TestKit.NUnit;
    using NUnit.Framework;

    using FIXServer.Actors;
    using Core.Actors;
    using FixMessages;
    using Actors;

    [TestFixture]
    public class FixServerActorTests : TestKit
    {
        // These tests could be DRYer if the actors used the FSM base class.

        [Test]
        public void FixServer_ServerLogsOutSuccessfully_AfterClientConnectAndLogon()
        { 
            // Set up
            var tcpServerActor = CreateTestProbe("TcpServer");
            Func<IActorRefFactory, IActorRef> tcpServerCreator = (_) => tcpServerActor;
            var fixInterpreterActor = CreateTestProbe("FixInterpreter");
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator = (_) => fixInterpreterActor;

            var fixServerProps = Props.Create(() => new FixServerActor(tcpServerCreator, fixInterpreterCreator));
            var fixServerActor = ActorOf(fixServerProps, "FixServer");

            var heartbeatInterval = TimeSpan.FromMilliseconds(20);

            // Ignore wiring-up type messages
            //  Each time IgnoreMessages is called it replaces the previous ignores for that test actor.
            tcpServerActor.IgnoreMessages((message) => message is TcpServerActor.Subscribe);
            fixInterpreterActor.IgnoreMessages((message) => message is FixInterpreterActor.SetServer
                || message is FixInterpreterActor.SetClient);

            // Test:
            // 1. Initial client connection
            fixServerActor.Tell(new FixServerActor.StartListening());
            tcpServerActor.ExpectMsg<TcpServerActor.StartListening>();
            tcpServerActor.Send(fixServerActor, new TcpServerActor.ClientConnected());
            tcpServerActor.ExpectMsg<TcpServerActor.AcceptMessages>();

            // 2. The FixServer receives a logon message from the client via the FixInterpreter
            fixInterpreterActor.Send(fixServerActor, new LogonMessage("A", "B" , 0, heartbeatInterval));
            // 3. The FixServer replies with a logon message
            fixInterpreterActor.ExpectMsgFrom<LogonMessage>(fixServerActor);
            // 4. and starts to send heartbeat messages to client via the FixInterpreter
            fixInterpreterActor.ExpectMsg<HeartbeatMessage>(heartbeatInterval.Add(TimeSpan.FromMilliseconds(5)));
            // 5. FixServer shutdown causes a logout message to be sent to the client.
            fixServerActor.Tell(new FixServerActor.Shutdown());
            fixInterpreterActor.ExpectMsg<LogoutMessage>();
            // 6. The client confirms with a logout message
            fixInterpreterActor.Send(fixServerActor, new LogoutMessage("A", "B", 2));
            tcpServerActor.ExpectMsg<TcpServerActor.Shutdown>();
        }

        // Tests
        // 1a. Connect, logon, heartbeat, server logoff and client replies, shutdown
        // 1b. Connect, logon, heartbeat, server logoff and client doesn't reply, shutdown
        // 2. Connect, logon, heartbeat, client logoff, shutdown
        // 3. Connect, logon, heartbeat not received
        // 4. Message received with incorrect seq number
        // 5. RFQ message received - respond with quote
        // + interpreter tests
        // + don't bother about TcpServer tests
        // + int tests with server and clients
    }
}
