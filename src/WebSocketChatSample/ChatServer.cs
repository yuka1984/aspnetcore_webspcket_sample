using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;

namespace WebSocketChatSample
{
    public class ChatServer : IEventProcessorFactory
    {
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly List<ChatClient> _clients = new List<ChatClient>();
        private EventProcessorHost _eventProcessorHost;
        private readonly ChatMessageProcessor _recieveProcessor = new ChatMessageProcessor();
        private readonly SendChatMessageToEventHubsObserver _sendObserver = new SendChatMessageToEventHubsObserver();

        public string EhConnectionString { get; set; } = "";
        public string EhEntityPath { get; set; } = "";
        public string StorageContainerName { get; set; } = "";
        public string StorageAccountName { get; set; } = "";
        public string StorageAccountKey { get; set; } = "";

        public string StorageConnectionString
            => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey}";

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            return _recieveProcessor;
        }

        public void Map(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(Acceptor);
        }

        public async Task EventRecieveEventAsync()
        {
            _sendObserver.EhConnectionString = EhConnectionString;
            _sendObserver.EhEntityPath = EhEntityPath;

            if (_eventProcessorHost == null)
            {
                _eventProcessorHost = new EventProcessorHost(
                    EhEntityPath,
                    PartitionReceiver.DefaultConsumerGroupName,
                    EhConnectionString,
                    StorageConnectionString,
                    StorageContainerName);

                await _eventProcessorHost.RegisterEventProcessorFactoryAsync(this).ConfigureAwait(false);
            }
        }


        private async Task Acceptor(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
            {
                await n.Invoke();
                return;
            }
            var websocket = await hc.WebSockets.AcceptWebSocketAsync();
            var client = new ChatClient(websocket);

            await client.RecieveJoinAsync();

            if (!client.IsJoin)
                return;


            using (await _asyncLock.LockAsync())
            {
                _clients.AsParallel().ForAll(
                    x =>
                    {
                        // 入室メッセージを送信
                        x.OnNext(new ChatMessage
                        {
                            UserName = "管理者",
                            Message = $"{client.UserName} さんが入室しました",
                            RecieveTime = DateTimeOffset.Now
                        });
                    });

                client.Subscribe(_sendObserver);
                _recieveProcessor.Subscribe(client);

                // 切断時動作
                client.Subscribe(s => { }, async () => await Close(client));

                // クライアント登録
                _clients.Add(client);
            }

            // 受信待機
            await client.ReceiveAsync();
        }

        private async Task Close(ChatClient client)
        {
            using (await _asyncLock.LockAsync())
            {
                _clients.Remove(client);

                // 退室メッセージを送信
                _clients.ForEach(
                    x =>
                    {
                        _clients.ForEach(
                            y =>
                                y.OnNext(new ChatMessage
                                {
                                    UserName = "管理者",
                                    Message = $"{x.UserName} さんが退室しました",
                                    RecieveTime = DateTimeOffset.Now
                                }));
                    });
            }
        }
    }
}