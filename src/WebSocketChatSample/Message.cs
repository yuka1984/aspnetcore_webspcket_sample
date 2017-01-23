using System;

namespace WebSocketChatSample
{
    public abstract class Message
    {
        public string MessageType { get; set; }
    }

    public class JoinMessage : Message
    {
        public const string MessageTypeKeyword = "JoinMessage";
        public string UserName { get; set; }
        public long? RoomId { get; set; }
    }

    public class ChatMessage
    {
        public string UserName { get; set; }
        public string Message { get; set; }
        public long RoomId { get; set; }
        public DateTimeOffset RecieveTime { get; set; }
    }
}