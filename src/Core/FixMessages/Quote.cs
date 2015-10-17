namespace Fixity.Core.FixMessages
{
    /// <summary>
    /// Quote message S.
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_S_83.html
    /// </summary>
    public class Quote : BaseMessage
    {
        public Quote(string senderCompID, string targetCompID,
            int messageSequenceNumber, string quoteReqID, string quoteID,
            string symbol, double offerPx) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
            QuoteReqID = quoteReqID;
            QuoteID = quoteID;
            Symbol = symbol;
            OfferPx = offerPx;
        }

        public string QuoteReqID { get; private set; }
        public string QuoteID { get; private set; }
        public string Symbol { get; private set; }
        public double OfferPx { get; private set; }
    }
}
