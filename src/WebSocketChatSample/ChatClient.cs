using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketChatSample.Models;

namespace WebSocketChatSample
{
    public class ChatClient : IObservable<ChatMessage>, IObserver<ChatMessage>, IDisposable
    {
        private readonly Subject<ChatMessage> _receiveSubject;
        private readonly WebSocket _socket;
        private readonly IRoomService _roomService;

        public ChatClient(WebSocket socket, IRoomService roomService)
        {
            _socket = socket;
            _roomService = roomService;
            _receiveSubject = new Subject<ChatMessage>();
        }

        /// <summary>
        /// WebSocketがオープンしているかを示します。
        /// </summary>
        public bool IsOpen => _socket.State == WebSocketState.Open;

        /// <summary>
        /// クライアントからチャットへの参加が行われたかを示します。
        /// </summary>
        public bool IsJoin { get; private set; }

        /// <summary>
        /// クライアントのユーザ名
        /// </summary>
        public string UserName { get; private set; } = "";

        /// <summary>
        /// 参加した部屋番号
        /// </summary>
        public long RoomId { get; private set; }

        /// <summary>
        /// クライアントの識別
        /// </summary>
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        public void Dispose()
        {
            _receiveSubject?.Dispose();
            _socket?.Dispose();
        }

        public IDisposable Subscribe(IObserver<ChatMessage> observer)
        {
            return _receiveSubject.Subscribe(observer);
        }

        public void OnCompleted()
        {
        }

        public async void OnError(Exception error)
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, error.Message,
                    CancellationToken.None);
        }

        public async void OnNext(ChatMessage value)
        {
            if (value.RoomId == this.RoomId)
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))),
                        WebSocketMessageType.Text,
                        true, CancellationToken.None);
            }            
        }

        /// <summary>
        /// クライアントのチャットへの参加の受信を行います。
        /// </summary>
        /// <param name="timeout">タイムアウト時間 msec</param>
        /// <returns></returns>
        public async Task RecieveJoinAsync(int timeout = 5000)
        {
            var buffer = new byte[4096];
            var tokensource = new CancellationTokenSource();
            tokensource.CancelAfter(timeout);
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), tokensource.Token);

            if (result.MessageType == WebSocketMessageType.Text && result.EndOfMessage)
            {
                var joinmessage =
                    JsonConvert.DeserializeObject<JoinMessage>(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (
                    JoinMessage.MessageTypeKeyword.Equals(joinmessage.MessageType,
                        StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrWhiteSpace(joinmessage.UserName) &&  _roomService.GetRooms().Any(x=> x.Id == joinmessage.RoomId))
                {
                    IsJoin = true;
                    RoomId = joinmessage.RoomId ?? 0;
                    UserName = joinmessage.UserName;
                }
            }
        }

        /// <summary>
        /// メッセージの受信を行います。
        /// </summary>
        /// <returns></returns>
        public async Task ReceiveAsync()
        {
            if (!IsJoin) return;

            var resultCount = 0;
            var buffer = new byte[4096];
            while (true)
            {
                var segmentbuffer = new ArraySegment<byte>(buffer, resultCount, buffer.Length - resultCount);
                var result = await _socket.ReceiveAsync(segmentbuffer, CancellationToken.None);
                resultCount += result.Count;
                if (resultCount >= buffer.Length)
                {
                    Debug.WriteLine("Long Message!!!");
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Long Message",
                        CancellationToken.None);
                    _socket.Dispose();
                    _receiveSubject.OnCompleted();
                }
                else if (result.EndOfMessage)
                {
                    if (result.MessageType == WebSocketMessageType.Close ||　resultCount == 0)
                    {
                        _receiveSubject.OnCompleted();
                        break;
                    }
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        _receiveSubject.OnNext(new ChatMessage
                        {
                            UserName = UserName,
                            Message = Encoding.UTF8.GetString(buffer, 0, resultCount),
                            RecieveTime = DateTimeOffset.Now,
                            RoomId = this.RoomId,
                        });
                        resultCount = 0;
                    }
                    else
                    {
                        _receiveSubject.OnCompleted();
                        break;
                    }
                }
            }
        }
    }
}