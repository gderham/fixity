namespace Fixity.FIXServer
{
    using System;

    using Akka.Actor;
    using Core.Actors;
    using Core;
    using Fixity.Actors;

    /// <summary>
    /// An ersatz FIX server for use in testing.
    /// Handles connection, logon, heartbeating and canned responses
    /// to requests from a FIX client.
    /// </summary>
    public class FixServerStub
    {
        private ActorSystem _actorSystem;
        private IActorRef _fixServerActor;

        public FixServerStub(int port)
        {
            _actorSystem = ActorSystem.Create("FIXServer");

            
            var tcpServerProps = Props.Create(() => new TcpServerActor(port,
                FIXUtilities.ParseFixMessagesFromText));
            Func<IActorRefFactory, IActorRef> tcpServerCreator =
                (context) => context.ActorOf(tcpServerProps, "TcpServer");

            var fixInterpreterProps = Props.Create(() => new FixInterpreterActor());
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator =
                (context) => context.ActorOf(fixInterpreterProps, "FixInterpreter");

            var fixServerProps = Props.Create(() => new Actors.FixServerActor(tcpServerCreator,
                fixInterpreterCreator));
            _fixServerActor = _actorSystem.ActorOf(fixServerProps, "FixServer");
            
            //actorSystem.AwaitTermination();

        }

        /// <summary>
        /// Start listening for a client connection.
        /// </summary>
        public void Start()
        {
            _fixServerActor.Tell(new Actors.FixServerActor.StartListening());
            _actorSystem.AwaitTermination();
        }

        /// <summary>
        /// Stop the server running and disconnect the socket.
        /// </summary>
        public void Stop()
        {
            _fixServerActor.Tell(new Actors.FixServerActor.Shutdown());
        }
        
    }
}
