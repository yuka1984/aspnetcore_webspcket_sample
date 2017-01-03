using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSocketChatSample
{
    public class Client : IObservable<ChatMessage>, IObserver<ChatMessage>, IDisposable
    {
        private readonly Subject<ChatMessage> _receiveSubject;
        private readonly WebSocket _socket;

        public Client(WebSocket socket)
        {
            _socket = socket;
            _receiveSubject = new Subject<ChatMessage>();
        }

        public bool IsOpen => _socket.State == WebSocketState.Open;
        public bool IsJoin { get; private set; }
        public string UserName { get; private set; } = "";

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
            if (_socket.State == WebSocketState.Open)
                await _socket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))),
                    WebSocketMessageType.Text,
                    true, CancellationToken.None);
        }

        public async Task WaitJoinAsync()
        {
            var buffer = new byte[4096];
            var tokensource = new CancellationTokenSource();
            tokensource.CancelAfter(5000);
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), tokensource.Token);

            if (result.MessageType == WebSocketMessageType.Text && result.EndOfMessage)
            {
                var joinmessage =
                    JsonConvert.DeserializeObject<JoinMessage>(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (
                    JoinMessage.MessageTypeKeyword.Equals(joinmessage.MessageType,
                        StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrWhiteSpace(joinmessage.UserName))
                {
                    IsJoin = true;
                    UserName = joinmessage.UserName;
                }
            }
        }

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
                    if (resultCount == 0)
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
                            RecieveTime = DateTimeOffset.Now
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