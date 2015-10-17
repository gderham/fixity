namespace Fixity.CoreTests.Actors
{
    using Akka.Actor;
    using Akka.TestKit;
    using Akka.TestKit.Xunit2;
    using Xunit;

    using Core.Actors;
    using Core;
    using Core.FixMessages;

    /// <summary>
    /// Unit tests for the FixInterpreter actor.
    /// We don't test the FIX conversion here - just the mechanism.
    /// </summary>
    public class FixInterpreterTests : TestKit
    {
        class FakeFixParser : IFixParser
        {
            public BaseMessage ConvertFixMessageToFixObject(string text)
            {
                // Just convert all messages into Heartbeats for this test.
                return new HeartbeatMessage("Sender", "Target", 0);
            }

            public string ConvertFixObjectToFixMessage(BaseMessage message)
            {
                return "test";
            }
        }

        private TestProbe _serverActor;
        private TestProbe _clientActor;
        private IActorRef _fixInterpreterActor;

        public FixInterpreterTests()
        {
            var actorSystem = ActorSystem.Create("System");
            
            var props = Props.Create(() => new FixInterpreterActor(new FakeFixParser()));
            _fixInterpreterActor = actorSystem.ActorOf(props);

            _serverActor = CreateTestProbe("Server");
            _clientActor = CreateTestProbe("Client");

            _fixInterpreterActor.Tell(new FixInterpreterActor.SetServer(_serverActor));
            _fixInterpreterActor.Tell(new FixInterpreterActor.SetClient(_clientActor));
        }

        [Fact]
        public void FixInterpreter_SendsFixObjectToServer_WhenFixMessageReceivedFromClient()
        {
            _fixInterpreterActor.Tell(new TcpServerActor.ReceivedMessage("some message"));
            _serverActor.ExpectMsg<HeartbeatMessage>();
        }
        
        [Fact]
        public void FixInterpreter_SendsFixMessageToClient_WhenFixObjectReceivedFromServer()
        {
            _fixInterpreterActor.Tell(new HeartbeatMessage("Sender", "Target", 0));
            _clientActor.ExpectMsg<TcpServerActor.SendMessage>(m =>
                m.Text == "test");
        }

    }
}
