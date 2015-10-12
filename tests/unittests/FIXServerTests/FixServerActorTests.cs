namespace Fixity.Tests.FIXServerTests
{
    using System;

    using Akka.Actor;
    using Akka.TestKit.NUnit;
    using NUnit.Framework;

    using FIXServer.Actors;
    using Core.Actors;

    [TestFixture]
    public class FixServerActorTests : TestKit
    {
        [Test]
        public void StartListening_TellsTcpServerToStartListening_WhenReady()
        {
            var tcpServerActor = CreateTestProbe("TcpServer");
            Func<IActorRefFactory, IActorRef> tcpServerCreator = (_) => tcpServerActor;
            
            var fixInterpreterActor = CreateTestProbe("FixInterpreter");
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator = (_) => fixInterpreterActor;

            var fixServerProps = Props.Create(() => new FixServerActor(tcpServerCreator, fixInterpreterCreator));
            var fixServerActor = ActorOf(fixServerProps);

            fixServerActor.Tell(new FixServerActor.StartListening());

            tcpServerActor.IgnoreMessages((message) => message is TcpServerActor.Subscribe);
            tcpServerActor.ExpectMsg<TcpServerActor.StartListening>();
        }


    }
}
