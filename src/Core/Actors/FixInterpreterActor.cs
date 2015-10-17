namespace Fixity.Core.Actors
{
    using Akka.Actor;
    using log4net;

    using FixMessages;

    // TODO: Validate the checksum of incoming messages

    /// <summary>
    /// Bidirectional message type converter between client (ASCII text FIX
    /// messages) and server (typed subclasses of BaseMessage).
    /// </summary>
    public class FixInterpreterActor : ReceiveActor
    {
        #region Incoming messages

        public class SetClient
        {
            public SetClient(IActorRef actor)
            {
                Actor = actor;
            }
            public IActorRef Actor { get; private set; }
        }

        public class SetServer
        {
            public SetServer(IActorRef actor)
            {
                Actor = actor;
            }
            public IActorRef Actor { get; private set; }
        }

        #endregion

        private static readonly ILog _log = LogManager.GetLogger(typeof(FixInterpreterActor));

        private IActorRef _client;
        private IActorRef _server;
        private IFixParser _parser;

        public FixInterpreterActor(IFixParser parser)
        {
            _parser = parser;
            Processing();
        }

        public void Processing()
        {
            Receive<SetClient>(message => //TODO: Do something better
            {
                _client = message.Actor;
            });

            Receive<SetServer>(message =>
            {
                _server = message.Actor;
            });
            
            // Text messages from the client to the server
            Receive<TcpServerActor.ReceivedMessage>(message =>
            {
                _server.Tell(_parser.ConvertFixMessageToFixObject(message.Text));
            });

            // Typed messages from the server to the client
            Receive<LogonMessage>(message =>
            {
                ConvertAndSendMessage(message);
            });

            Receive<HeartbeatMessage>(message =>
            {
                ConvertAndSendMessage(message);
            });

            Receive<LogoutMessage>(message =>
            {
                ConvertAndSendMessage(message);
            });

        }

        private void ConvertAndSendMessage(BaseMessage message)
        {
            string fixMessage = _parser.ConvertFixObjectToFixMessage(message);
            _client.Tell(new TcpServerActor.SendMessage(fixMessage));
        }
    }
}
