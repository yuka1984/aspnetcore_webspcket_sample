﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using WebSocketChatSample.ServuceBus;

namespace WebSocketChatSample
{
    public class ChatServer : IEventProcessorFactory, IDisposable
    {
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly List<ChatClient> _clients = new List<ChatClient>();
        private EventProcessorHost _eventProcessorHost;
        private readonly ChatMessageProcessor _recieveProcessor = new ChatMessageProcessor();
        public readonly TopicsSender TopicsSender = new TopicsSender();
        public readonly TopicsReciever TopicsReciever = new TopicsReciever();

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
            await TopicsReciever.RecieveAsync();
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
                TopicsSender.OnNext(new ChatMessage
                {
                    UserName = "管理者",
                    Message = $"{client.UserName} さんが入室しました",
                    RecieveTime = DateTimeOffset.Now
                });

                // 送信と受信を設定
                client.Subscribe(TopicsSender);
                TopicsReciever.Subscribe(client);

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
                TopicsSender.OnNext(new ChatMessage
                {
                    UserName = "管理者",
                    Message = $"{client.UserName} さんが退室しました",
                    RecieveTime = DateTimeOffset.Now
                });                
            }
        }

        public void Dispose()
        {
            TopicsReciever?.Dispose();
        }
    }
}