namespace Fixity.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Parses whole messages out of a text stream, returns them as 
    /// an ordered list, and the remaining text (a partial message).
    /// </summary>
    public class MessageInfo
    {
        /// <param name="text">
        /// Text of the form: [WholeMessage][StartOfPartialMessage]
        /// </param>
        public MessageInfo(IEnumerable<string> completeMessages, string remainder)
        {
            CompleteMessages = completeMessages;
            RemainingText = remainder; 
        }

        public IEnumerable<string> CompleteMessages { get; private set; }

        public string RemainingText { get; private set; }
       
    }
}
