namespace Fixity.FixMessages
{
    using Fixity.Core;

    /// <summary>
    /// QuoteRequest message R (for a single instrument).
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_R_82.html
    /// </summary>
    public class QuoteRequest : BaseMessage
    {
        public QuoteRequest(string senderCompID, string targetCompID,
            int messageSequenceNumber, string quoteReqID, string symbol) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
            QuoteReqID = quoteReqID;
            NoRelatedSym = 1; // = one instrument quote requested
            Symbol = symbol;
        }

        public string QuoteReqID { get; private set; }
        public int NoRelatedSym { get; private set; }
        public string Symbol { get; private set; }
    }
}
