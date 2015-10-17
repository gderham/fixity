namespace Fixity.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Text;
    using System.Linq;

    using FixMessages;

    /// <summary>
    /// Converts text FIX messages to/from a subclass of BaseMessage.
    /// </summary>
    public class FixParser : IFixParser
    {
        /// <summary>
        /// Splits FIX text into field=value pairs.
        /// </summary>
        private static Regex _fixFieldPattern = new Regex("(\\d{1,3})=([A-Za-z0-9_ \\.]+)\x01");

        // Message types - see http://www.onixs.biz/fix-dictionary/4.3/msgs_by_msg_type.html
        public const string HEARTBEAT_MESSAGE    = "0";
        public const string TESTREQUEST_MESSAGE  = "1";
        public const string LOGON_MESSAGE        = "A";
        public const string LOGOUT_MESSAGE       = "5";
        public const string QUOTEREQUEST_MESSAGE = "R";
        public const string QUOTE_MESSAGE        = "S";

        // Field names - see http://www.onixs.biz/fix-dictionary/4.3/fields_by_name.html
        public const string BEGINSTRING_FIELD    = "8";
        public const string BODYLENGTH_FIELD     = "9";
        public const string CHECKSUM_FIELD       = "10";
        public const string MSGSEQNUM_FIELD      = "34";
        public const string MESSAGETYPE_FIELD    = "35";
        public const string SENDERCOMPID_FIELD   = "49";
        public const string SYMBOL_FIELD         = "55";
        public const string TARGETCOMPID_FIELD   = "56";
        public const string HEARTBTINT_FIELD     = "108";
        public const string TESTREQID_FIELD      = "112";
        public const string QUOTEID_FIELD        = "117";
        public const string QUOTEREQID_FIELD     = "131";
        public const string OFFERPX_FIELD        = "133";
        public const string NORELATEDSYM_FIELD   = "644";

        // Constant field values
        public const string BEGINSTRING          = "FIXT1.1";

        public const char FixDelimiter = '\x01';
        
        /// <summary>
        /// Convert a FIX text message into a subclass of BaseMessage.
        /// </summary>
        public BaseMessage ConvertFixMessageToFixObject(string text)
        {
            Dictionary<string, string> fixFields = ParseFixMessageIntoDictionary(text);

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

                case LOGOUT_MESSAGE:
                {
                    return new LogoutMessage(senderCompID, targetCompID, messageSequenceNumber);
                }

                case QUOTEREQUEST_MESSAGE:
                {
                    string quoteReqID = fixFields[QUOTEREQID_FIELD];
                    string symbol = fixFields[SYMBOL_FIELD];

                    return new QuoteRequestMessage(senderCompID, targetCompID, messageSequenceNumber,
                        quoteReqID, symbol);
                }

                default:
                {
                     throw new ArgumentException("Cannot parse FIX message of type: " + messageType);
                }
            }
        }

        /// <summary>
        /// Convert a BaseMessage subclass into a FIX text message.
        /// </summary>
        public string ConvertFixObjectToFixMessage(BaseMessage message)
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
            else if (message is LogoutMessage)
            {
                fixFields[MESSAGETYPE_FIELD] = LOGOUT_MESSAGE;
            }
            else if (message is QuoteMessage)
            {
                var msg = (QuoteMessage)message;
                fixFields[MESSAGETYPE_FIELD] = QUOTE_MESSAGE;
                fixFields[QUOTEREQID_FIELD] = msg.QuoteReqID;
                fixFields[QUOTEID_FIELD] = msg.QuoteID;
                fixFields[SYMBOL_FIELD] = msg.Symbol;
                fixFields[OFFERPX_FIELD] = string.Format("{0:0.0000}", msg.OfferPx);
            }
            else if (message is TestRequestMessage)
            {
                var msg = (TestRequestMessage)message;
                fixFields[MESSAGETYPE_FIELD] = TESTREQUEST_MESSAGE;
                fixFields[TESTREQID_FIELD] = msg.TestReqID;
            }
            else
            {
                throw new ArgumentException("Unable to convert "
                    + message.GetType().ToString() + " to FIX message.");
            }
            
            return CreateFixMessageFromDictionary(fixFields);
        }

        /// <summary>
        /// Calculates and appends a checksum to a FIX message that doesn't
        /// have a checksum, and must be terminated by a [SOH] delimiter.
        /// </summary>
        /// <param name="message">A FIX message without checksum. </param>
        public static string AddChecksum(string message)
        {
            int checksum = Encoding.ASCII.GetBytes(message).Sum(b => b) % 256;

            return string.Format("{0}{1}={2}{3}", message, CHECKSUM_FIELD, checksum, FixDelimiter);
        }

        /// <summary>
        /// Parse MessageType=Text pairs from a (possibly partial) FIX message
        /// into a dictionary. No validation is performed.
        /// </summary>
        /// <returns>A dictionary of the MessageType->Text pairs.</returns>
        public static Dictionary<string, string> ParseFixMessageIntoDictionary(string message)
        {
            var matches = _fixFieldPattern.Matches(message);

            var dict = new Dictionary<string, string>();
            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                dict.Add(tag, value);
            }

            return dict;
        }

        /// <summary>
        /// Construct a FIX text message from field pairs.
        /// No validation is performed, but the BodyLength and Checksum is
        /// set correctly.
        /// </summary>
        private static string CreateFixMessageFromDictionary(Dictionary<string, string> fixFields)
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
            message = AddChecksum(message);

            return message;
        }

        /// <summary>
        /// Splits the given text into a number of whole FIX messages and the
        /// remainder. Useful for parsing messages from a text stream.
        /// </summary>
        public static MessageInfo ExtractFixMessages(string text)
        {
            var wholeMessages = new List<string>();
            string remainingText = "";

            //TODO: Replace this logic with a regex (?)
            int indexOfMessageStart = 0;
            int indexOfMessageEnd = 0;
            foreach (Match match in _fixFieldPattern.Matches(text))
            {
                if (match.Value.StartsWith(CHECKSUM_FIELD + "="))
                {
                    indexOfMessageEnd = match.Index + match.Length;
                    wholeMessages.Add(text.Substring(indexOfMessageStart, indexOfMessageEnd - indexOfMessageStart));
                    indexOfMessageStart = indexOfMessageEnd;
                }
            }

            if (indexOfMessageEnd < text.Length)
            {
                remainingText = text.Substring(indexOfMessageEnd);
            }

            return new MessageInfo(wholeMessages, remainingText);
        }

        private static string FieldString(string fieldName, string fieldValue)
        {
            return string.Format("{0}={1}{2}", fieldName, fieldValue, FixDelimiter);
        }

        private static string ConstructField(Dictionary<string, string> fixFields, string fieldName)
        {
            return FieldString(fieldName, fixFields[fieldName]);
        }
    }
}
