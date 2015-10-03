using System;

using Akka.Actor;
using log4net;

namespace Fixity.FIXClient.Actors
{
    /// <summary>
    /// An instance of the FixSessionActor class creates and manages a 
    /// persistent connection to a FIX server specified by host and port.
    /// It handles connection logon and heartbeating.
    /// </summary>
    class FixSessionActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(FixSessionActor));

        private readonly Uri _fixServerAddress;

        /// <summary>
        /// The mechanics of the connection are delegated to a separate
        /// actor - this allows the specifics to be varied and hidden from
        /// this session actor.
        /// </summary>
        private IActorRef _fixConnectionActor;

        /// <summary>Connects to the FIX server immediately.</summary>
        /// <param name="fixServerAddress">The address of the FIX server to
        /// connect to is fixed for the life of this session.</param>
        public FixSessionActor(Uri fixServerAddress)
        {
            _fixServerAddress = fixServerAddress;       
        }

        protected override void PreStart()
        {
            _log.Debug("Creating FixConnectionActor"); // remove this
            _fixConnectionActor = Context.ActorOf(Props.Create(() => new FixConnectionActor(_fixServerAddress)));

            Become(Connecting); // Is it sensible to put this in PreStart?
        }

        #region Processing states

        public void Connecting()
        {
            _log.Debug("FIX Client is attempting to connect to server.");

            Receive<FixConnectionActor.LoggedOn>(message =>
            {
                Become(Ready);
            });

            // We're ready to connect.
            _fixConnectionActor.Tell(new FixConnectionActor.Connect());
        }

        /// <summary>
        /// In the Ready state the session must reciprocate heartbeats and
        /// handle any logoff or disconnection from the server.
        /// </summary>
        public void Ready()
        {
            _log.Debug("FIX Client is logged on and ready for use.");

        }

        #endregion
    }
}
