namespace Fixity.Core
{
    using FixMessages;

    public interface IFixParser
    {
        BaseMessage ConvertFixMessageToFixObject(string text);

        string ConvertFixObjectToFixMessage(BaseMessage message);
    }
}
