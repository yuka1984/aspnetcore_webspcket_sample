using System.Net;
using Amqp;

namespace WebSocketChatSample.ServuceBus
{
    public abstract class  TopicsClientBase
    {
        public string NameSpaceUrl { get; set; } = "";
        public string BaseUrl => $"https://{NameSpaceUrl}/";
        public string PolicyName { get; set; } = "";
        public string Key { get; set; } = "";
        public string ConnectionString => $"amqps://{WebUtility.UrlEncode(PolicyName)}:{WebUtility.UrlEncode(Key)}@{NameSpaceUrl}/";
        public string Topic { get; set; } = "";
        protected Address GetAddress() => new Address(ConnectionString);
        protected Connection GetConnection() => new Connection(new Address(ConnectionString));
        protected Session GetSession(Connection connection) => new Session(connection);        

    }
}