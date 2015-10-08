namespace Fixity.Core
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Parses whole FIX messages out of a text stream, returns them as 
    /// an ordered list, and the remaining text (a partial FIX message).
    /// </summary>
    public class FixMessageInfo
    {
        /// <param name="text">
        /// Text of the form: [WholeMessage][StartOfPartialMessage]
        /// </param>
        public FixMessageInfo(IEnumerable<string> completeMessages, string remainder)//(string text)
        {
            CompleteMessages = completeMessages;
            RemainingText = remainder;

            // Identify messages by the checksum field (which is always the last field).
            ////Regex pattern = new Regex("[A-Za-z0-9_ =\\.\x01]*10=\\d+\x01");

            //Regex pattern = new Regex("([A-Za-z0-9]{1,2})=([A-Za-z0-9_ \\.]+)\x01");

            //var matches = pattern.Matches(text);
            
            //var wholeMessages = new List<string>();

            //int checksumPosition = text.IndexOf("10=");
            //if (checksumPosition == -1)
            //{
            //    // The text is a partial message
            //    PartialMessage = text;
            //    ContainsPartialMessage = true;
            //}
            //else
            //{
            //    text.IndexOf('\x01', checksumPosition);

            //}
            
            // scan for checksum fields
      
        }

        public IEnumerable<string> CompleteMessages { get; private set; }

        public string RemainingText { get; private set; }
       
    }
}
