namespace Fixity
{
    using System;
    using System.Collections.Generic;

    using Core;
    using FixMessages;

    /// <summary>
    /// Converts a text FIX message into a strongly typed subclass of BaseMessage.
    /// </summary>
    public class FixParser
    {
        //TODO: Combine FIXUtilities into this class

        // Message types
        private const string HEARTBEAT_MESSAGE = "0";
        private const string LOGON_MESSAGE = "A";
        // Field names
        private const string BEGINSTRING_FIELD = "8";
        private const string BODYLENGTH_FIELD = "9";
        private const string MESSAGETYPE_FIELD = "35";
        private const string SENDERCOMPID_FIELD = "49";
        private const string TARGETCOMPID_FIELD = "56";
        private const string MSGSEQNUM_FIELD = "34";
        private const string HEARTBTINT_FIELD = "108";
        // Constant field values
        private const string BEGINSTRING = "FIXT1.1";

        public const char FixDelimiter = '\x01';
        
        /// <summary>
        /// Convert a FIX text message into a subclass of BaseMessage.
        /// </summary>
        public BaseMessage ParseMessage(string text)
        {
            //TODO: Throw parse exception if anything fails here
            Dictionary<string, string> fixFields = FIXUtilities.ParseFixMessage(text);

            string messageType = fixFields[MESSAGETYPE_FIELD];
            string senderCompID = fixFields[SENDERCOMPID_FIELD];
            string targetCompID = fixFields[TARGETCOMPID_FIELD];
            int messageSequenceNumber = int.Parse(fixFields[MSGSEQNUM_FIELD]);

            switch (messageType)
            {
                case HEARTBEAT_MESSAGE:
                {
                    return new HeartbeatMessage(senderCompID, targetCompID, messageSequenceNumber);
                }

                case LOGON_MESSAGE:
                {
                    TimeSpan heartbeatInterval =
                        TimeSpan.FromSeconds(int.Parse(fixFields[HEARTBTINT_FIELD]));

                    return new LogonMessage(senderCompID, targetCompID, messageSequenceNumber,
                         heartbeatInterval);
                 }

                default:
                {
                     //TODO: Throw parse exception
                     return null;
                }
            }
        }

        /// <summary>
        /// Convert a BaseMessage subclass into a FIX text message.
        /// </summary>
        public string CreateMessage(BaseMessage message)
        {
            var fixFields = new Dictionary<string, string>() {
                    { SENDERCOMPID_FIELD, message.SenderCompID },
                    { TARGETCOMPID_FIELD, message.TargetCompID },
                    { MSGSEQNUM_FIELD,    message.MessageSequenceNumber.ToString() }
                };

            if (message is LogonMessage)
            {
                var msg = (LogonMessage)message;
                fixFields[MESSAGETYPE_FIELD] = LOGON_MESSAGE;
                fixFields[HEARTBTINT_FIELD] = Convert.ToString((int)msg.HeartBeatInterval.TotalSeconds);
            }
            else if (message is HeartbeatMessage)
            {
                var msg = (HeartbeatMessage)message;
                fixFields[MESSAGETYPE_FIELD] = HEARTBEAT_MESSAGE;
            }
            else
            {
                throw new ArgumentException("Unable to convert "
                    + message.GetType().ToString() + " to FIX message.");
            }
            
            return CreateFixMessage(fixFields);
        }

        private string FieldString(string fieldName, string fieldValue)
        {
            return string.Format("{0}={1}{2}", fieldName, fieldValue, FixDelimiter);
        }

        private string ConstructField(Dictionary<string,string> fixFields, string fieldName)
        {
            return FieldString(fieldName, fixFields[fieldName]);
        }

        /// <summary>
        /// Construct a FIX text message from field pairs.
        /// No validation is performed, but the BodyLength and Checksum is
        /// set correctly.
        /// </summary>
        public string CreateFixMessage(Dictionary<string, string> fixFields)
        {
            // 1. Construct the main body first (between BodyLength and Checksum fields)
            var initialMandatoryFieldsInOrder =
                new List<string> { MESSAGETYPE_FIELD, SENDERCOMPID_FIELD, TARGETCOMPID_FIELD };

            string body = "";

            // Add mandatory fields
            foreach (string mandatoryField in initialMandatoryFieldsInOrder)
            {
                body += ConstructField(fixFields, mandatoryField);
            }

            // Add remaining fields
            foreach (var fieldName in fixFields.Keys)
            {
                if (initialMandatoryFieldsInOrder.Contains(fieldName))
                {
                    continue;
                }

                body += ConstructField(fixFields, fieldName);
            }
            
            // 2. Get the FIX version and BodyLength
            string message = FieldString(BEGINSTRING_FIELD, BEGINSTRING);

            message += FieldString(BODYLENGTH_FIELD, Convert.ToString(body.Length));

            message += body;

            // 3. Add the Checksum
            message = FIXUtilities.AddChecksum(message);

            return message;
        }
    }
}
