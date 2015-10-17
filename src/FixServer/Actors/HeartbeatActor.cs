namespace Fixity.FIXServer.Actors
{
    using System;

    using Akka.Actor;
    using FixMessages;

    public class HeartbeatActor : ReceiveActor
    {
        /// <summary>
        /// The interval between heartbeat messages sent to the client.
        /// This is negotiated in the received Logon message.
        /// </summary>
        private TimeSpan _heartbeatInterval;

        private ICancelable _heartbeatCanceller;

        private DateTime _lastHeartbeatArrivalTime;

        public HeartbeatActor()
        {
            Inactive();
        }

        public void Inactive()
        {
            Receive<LogonMessage>(message =>
            {
                _heartbeatInterval = message.HeartBeatInterval;
            });

            Receive<HeartbeatMessage>(message =>
            {
                _lastHeartbeatArrivalTime = DateTime.UtcNow; //TODO: The server could timestamp messages as they arrive to avoid checking the time here?
            });

        }






    }
}
