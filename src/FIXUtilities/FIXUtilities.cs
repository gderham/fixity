namespace Fixity.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Functions that deal with FIX protocol specifics.
    /// </summary>
    public static class FIXUtilities
    {
        public const char FixDelimiter = '\x01';
        
        //TODO: Check which chars FIX accepts
        private static Regex _fixFieldPattern = new Regex("([A-Za-z0-9]{1,2})=([A-Za-z0-9_ \\.]+)");

        public static string SetFIXDelimiter(string message)
        {
            return message.Replace('|', FixDelimiter);
        }

        /// <summary>
        /// Converts a | delimited string message to use the FIX
        /// SOH delimiter and appends the correct checksum (tag 10).
        /// </summary>
        /// <param name="message">
        /// For example: 8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|
        /// </param>
        /// <returns>
        /// For example: 8=FIXT1.1[SOH]9=100[SOH]35=A[SOH]49=SomeClient[SOH]56=SomeFacility[SOH]34=1[SOH]10=21[SOH]
        /// </returns>
        public static string CreateFixMessage(string message)
        {
            if (!message.EndsWith("|"))
            {
                throw new ArgumentException("Message must be be terminated by a | delimiter.",
                    "message");
            }

            return AddChecksum(SetFIXDelimiter(message));
        }

        /// <summary>
        /// Calculates and appends a checksum to a FIX message that doesn't
        /// have a checksum, and must be terminated by a [SOH] delimiter.
        /// </summary>
        /// <param name="message">A FIX message without checksum. </param>
        private static string AddChecksum(string message)
        {
            int checksum = Encoding.ASCII.GetBytes(message).Sum(b => b) % 256;
            
            return string.Format("{0}10={1}{2}", message, checksum, FixDelimiter);
        }

        /// <summary>
        /// Parse MessageType=Text pairs from a (possibly partial) FIX message
        /// into a dictionary. No validation is performed.
        /// </summary>
        /// <returns>A dictionary of the MessageType->Text pairs.</returns>
        public static Dictionary<string,string> ParseFixMessage(string message)
        {
            var matches = _fixFieldPattern.Matches(message);

            var dict = new Dictionary<string,string>();
            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                dict.Add(tag, value);
            }

            return dict;
        }

    }
}
