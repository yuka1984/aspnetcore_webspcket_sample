using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;

namespace WebSocketChatSample
{
    public class DirectEventReceiveManager : IObservable<ChatMessage>
    {
        private readonly Subject<ChatMessage> _chatmessageSubject = new Subject<ChatMessage>();

        public DirectEventReceiveManager(string eventHubPath, string consumerGroupName,
            string eventHubConnectionString)
        {
            EventHubPath = eventHubPath;
            ConsumerGroupName = consumerGroupName;
            EventHubConnectionString = eventHubConnectionString;
        }

        public string EventHubPath { get; }
        public string ConsumerGroupName { get; }
        public string EventHubConnectionString { get; }

        public IDisposable Subscribe(IObserver<ChatMessage> observer)
        {
            return _chatmessageSubject.Subscribe(observer);
        }

        public async Task EventRecieveAsync()
        {
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(EventHubConnectionString)
            {
                EntityPath = EventHubPath
            };
            var client = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
            var runtimeinformation = await client.GetRuntimeInformationAsync();
            var tasks = runtimeinformation.PartitionIds.Select(RecieveLoop);
            Task.WaitAll(tasks.ToArray());
        }

        private async Task RecieveLoop(string partitionId)
        {
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(EventHubConnectionString)
            {
                EntityPath = EventHubPath
            };
            var client = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
            var ff = await client.GetPartitionRuntimeInformationAsync(partitionId);
            var offset = ff.LastEnqueuedOffset;
            while (true)
                try
                {
                    var receiver = client.CreateReceiver(ConsumerGroupName, partitionId, offset);
                    var messages = await receiver.ReceiveAsync(100);
                    if (messages != null)
                        foreach (var eventData in messages)
                        {
                            var body = Encoding.UTF8.GetString(eventData.Body.Array);
                            var charmessage = JsonConvert.DeserializeObject<ChatMessage>(body);
                            _chatmessageSubject.OnNext(charmessage);
                            offset = eventData.Properties["x-opt-offset"].ToString();
                        }
                    await receiver.CloseAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
        }
    }
}