﻿using System;
using Amqp;
using Amqp.Framing;
using Newtonsoft.Json;

namespace WebSocketChatSample.ServuceBus
{
    public class TopicsSender : TopicsClientBase, IObserver<ChatMessage>
    {
        private SenderLink senderLink;
        private string SenderSubscriptionId { get; } = "websocketscale.amqp.sender";


        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ChatMessage value)
        {
            if (senderLink == null || senderLink.IsClosed)
                senderLink = new SenderLink(GetSession(GetConnection()), SenderSubscriptionId, Topic);

            var message = new Amqp.Message(JsonConvert.SerializeObject(value));
            message.Properties = new Properties
            {
                MessageId = Guid.NewGuid().ToString()
            };
            message.ApplicationProperties = new ApplicationProperties();
            message.ApplicationProperties["Message.Type.FullName"] = typeof(string).FullName;
            senderLink.Send(message);
        }
    }
}