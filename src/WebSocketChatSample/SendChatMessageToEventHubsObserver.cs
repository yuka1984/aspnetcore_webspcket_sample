using System;
using System.Text;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;

namespace WebSocketChatSample
{
    public class SendChatMessageToEventHubsObserver : IObserver<ChatMessage>
    {
        private EventHubClient client;
        public string EhConnectionString { get; set; }
        public string EhEntityPath { get; set; }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public async void OnNext(ChatMessage value)
        {
            try
            {
                if (client == null)
                {
                    var connectionStringBuilder = new EventHubsConnectionStringBuilder(EhConnectionString)
                    {
                        EntityPath = EhEntityPath
                    };
                    client = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
                }

                await client.SendAsync(new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))));                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                client = null;
            }
            
        }
    }
}