using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace WebSocketChatSample
{
    public class ChatServer
    {
        private List<Client> _clients = new List<Client>();
        private object _lockobj = new object();

        public void Map(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(Acceptor);
        }


        private async Task Acceptor(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
            {
                await n.Invoke();
                return;
            }
            var websocket = await hc.WebSockets.AcceptWebSocketAsync();
            var client = new Client(websocket);

            await client.WaitJoinAsync();

            if (!client.IsJoin)
                return;

            // 入室メッセージを送信
            _clients.ForEach(
                x =>
                    x.OnNext(new ChatMessage
                    {
                        UserName = "管理者",
                        Message = $"{client.UserName} さんが入室しました",
                        RecieveTime = DateTimeOffset.Now
                    }));

            // ほかのクライアントと連結
            _clients.ForEach(c =>
            {
                c.Subscribe(client);
                client.Subscribe(c);
            });

            // 自分自身と連結
            client.Subscribe(client);

            _clients.Add(client);
            client.Subscribe(s => { }, Close);

            await client.ReceiveAsync();
        }

        private void Close()
        {
            var removeclients = _clients.Where(x => !x.IsOpen).ToList();
            removeclients.ForEach(x => x.Dispose());
            _clients = _clients.Where(x => x.IsOpen).ToList();

            // 退室メッセージを送信
            removeclients.ForEach(
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