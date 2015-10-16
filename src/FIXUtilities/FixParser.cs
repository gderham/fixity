﻿namespace Fixity
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Text;
    using System.Linq;

    using Core;
    using FixMessages;

    /// <summary>
    /// Converts text FIX messages to/from a subclass of BaseMessage.
    /// </summary>
    public class FixParser
    {
        /// <summary>
        /// Splits FIX text into field=value pairs.
        /// </summary>
        private static Regex _fixFieldPattern = new Regex("(\\d{1,3})=([A-Za-z0-9_ \\.]+)\x01");

        // Message types - see http://www.onixs.biz/fix-dictionary/4.3/msgs_by_msg_type.html
        private const string HEARTBEAT_MESSAGE    = "0";
        private const string LOGON_MESSAGE        = "A";
        private const string LOGOUT_MESSAGE       = "5";
        private const string QUOTEREQUEST_MESSAGE = "R";
        private const string QUOTE_MESSAGE        = "S";

        // Field names - see http://www.onixs.biz/fix-dictionary/4.3/fields_by_name.html
        private const string BEGINSTRING_FIELD    = "8";
        private const string BODYLENGTH_FIELD     = "9";
        private const string CHECKSUM_FIELD       = "10";
        private const string MSGSEQNUM_FIELD      = "34";
        private const string MESSAGETYPE_FIELD    = "35";
        private const string SENDERCOMPID_FIELD   = "49";
        private const string SYMBOL_FIELD         = "55";
        private const string TARGETCOMPID_FIELD   = "56";
        private const string HEARTBTINT_FIELD     = "108";
        private const string QUOTEID_FIELD        = "117";
        private const string QUOTEREQID_FIELD     = "131";
        private const string OFFERPX_FIELD        = "133";
        private const string NORELATEDSYM_FIELD   = "644";

        // Constant field values
        private const string BEGINSTRING          = "FIXT1.1";

        public const char FixDelimiter = '\x01';
        
        /// <summary>
        /// Convert a FIX text message into a subclass of BaseMessage.
        /// </summary>
        public BaseMessage ConvertFixMessageToFixObject(string text)
        {
            //TODO: Throw parse exception if anything fails here
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

                    return new QuoteRequest(senderCompID, targetCompID, messageSequenceNumber,
                        quoteReqID, symbol);
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
                // No additional fields to add.
            }
            else if (message is Quote)
            {
                var msg = (Quote)message;
                fixFields[QUOTEREQID_FIELD] = msg.QuoteReqID;
                fixFields[QUOTEID_FIELD] = msg.QuoteID;
                fixFields[SYMBOL_FIELD] = msg.Symbol;
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
        private string CreateFixMessageFromDictionary(Dictionary<string, string> fixFields)
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
            foreach (Match match in _fixFieldPattern.Matches(text)) //TODO: Check the matches are guaranteed to be ordered
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

        private string FieldString(string fieldName, string fieldValue)
        {
            return string.Format("{0}={1}{2}", fieldName, fieldValue, FixDelimiter);
        }

        private string ConstructField(Dictionary<string, string> fixFields, string fieldName)
        {
            return FieldString(fieldName, fixFields[fieldName]);
        }
    }
}
