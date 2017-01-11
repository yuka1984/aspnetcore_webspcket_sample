using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Newtonsoft.Json;

namespace WebSocketChatSample
{
    public class ChatMessageProcessor : IEventProcessor, IObservable<ChatMessage>
    {
        private readonly Subject<ChatMessage> _subject = new Subject<ChatMessage>();

        public Task OpenAsync(PartitionContext context)
        {
            return Task.FromResult(false);
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            return Task.FromResult(false);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            foreach (var message in messages)
            {
                var body = Encoding.UTF8.GetString(message.Body.Array);
                var charmessage = JsonConvert.DeserializeObject<ChatMessage>(body);
                _subject.OnNext(charmessage);
                await context.CheckpointAsync(message);
            }
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            return Task.FromResult(false);
        }

        public IDisposable Subscribe(IObserver<ChatMessage> observer)
        {
            return _subject.Subscribe(observer);
        }
    }
}