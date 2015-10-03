using System;
using System.Net.Sockets;

using Akka.Actor;
using log4net;

namespace Fixity.FIXClient.Actors
{
    class FixConnectionActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(FixConnectionActor));

        #region Messages

        /// <summary>
        /// Asks the FixConnection to connect to the FIX server.
        /// </summary>
        public class Connect { };

        /// <summary>
        /// Asks the FixConnection to disconnect from the FIX server in an
        /// orderly manner.
        /// </summary>
        public class Disconnect { };

        /// <summary>
        /// Indicates the connection is ready for use.
        /// </summary>
        public class LoggedOn { };

        #endregion

        private readonly Uri _fixServerAddress;

        public FixConnectionActor(Uri fixServerAddress)
        {
            _fixServerAddress = fixServerAddress;

            Unconnected();

            // on restart, must try to reconnect if previously connected
        }

        #region States

        public void Unconnected()
        {
            Receive<Connect>(message =>
            {
                Become(Connecting);
            });

            // TODO: throw error if other message received?
        }

        public void Connecting()
        {
            // Connect to specified FIX server (a TCP socket).
            _log.Debug("Connecting to socket");

            Receive<Disconnect>(message =>
            {
                // TODO: Handle this
                _log.Debug("Received disconnect instruction.");
            });

            /*
            // Something like this but feeding the whole messages back into Self asyncly
            var tcpClient = new TcpClient(_fixServerAddress.Host, _fixServerAddress.Port);

            NetworkStream stream = tcpClient.GetStream();
            Byte[] buffer = new Byte[256];
            int numBytes = stream.Read(buffer, 0, buffer.Length);

            // read message header, then figure out length of message and read that

            var messageText = System.Text.Encoding.ASCII.GetString(buffer, 0, numBytes);
            
            stream.Close();
            tcpClient.Close();
            */
        }

        #endregion
    }
}
