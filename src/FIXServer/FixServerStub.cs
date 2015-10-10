namespace Fixity.FIXServer
{
    using Akka.Actor;

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
            
            var fixServerProps = Props.Create(() => new Actors.FixServerActor(port));
            _fixServerActor = _actorSystem.ActorOf(fixServerProps);
            
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
