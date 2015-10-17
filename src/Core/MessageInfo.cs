namespace Fixity.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// An ordered list of messages received from a stream, plus the remaining
    /// text.
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
