﻿using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Newtonsoft.Json;

namespace WebSocketChatSample.ServuceBus
{
    public class TopicsReciever : TopicsClientBase, IObservable<ChatMessage>, IDisposable
    {
        private readonly Subject<ChatMessage> _receiveSubject = new Subject<ChatMessage>();
        private ReceiverLink _consumer;
        private string ReceiverSubscriptionId { get; } = "receiver-link" + Guid.NewGuid();
        private string Subscription { get; } = Guid.NewGuid().ToString();

        public void Dispose()
        {
            var result =
                DeleteSubscriptionAsync(BaseUrl, Topic, Subscription, GetSASToken(BaseUrl, PolicyName, Key))
                    .GetAwaiter()
                    .GetResult();
        }

        public IDisposable Subscribe(IObserver<ChatMessage> observer)
        {
            return _receiveSubject.Subscribe(observer);
        }

        public async Task RecieveAsync()
        {
            var put = await CreateSubscriptionAsync(BaseUrl, Topic, Subscription, GetSASToken(BaseUrl, PolicyName, Key));

            _consumer = new ReceiverLink(GetSession(GetConnection()), ReceiverSubscriptionId,
                $"{Topic}/Subscriptions/{Subscription}");

            _consumer.Closed += ConsumerOnClosed;

            _consumer.Start(10, OnMessage);
        }

        private void ConsumerOnClosed(AmqpObject sender, Error error)
        {
            _receiveSubject.OnError(new Exception(error?.Description));
        }

        private void OnMessage(ReceiverLink receiver, Amqp.Message message)
        {
            var charmessage = JsonConvert.DeserializeObject<ChatMessage>(message.Body.ToString());
            _receiveSubject.OnNext(charmessage);
            receiver.Accept(message);
        }

        private static string GetSASToken(string baseAddress, string SASKeyName, string SASKeyValue)
        {
            var fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var expiry = Convert.ToString((int) fromEpochStart.TotalSeconds + 3600);
            var stringToSign = WebUtility.UrlEncode(baseAddress) + "\n" + expiry;
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SASKeyValue));

            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = string.Format(CultureInfo.InvariantCulture,
                "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                WebUtility.UrlEncode(baseAddress), WebUtility.UrlEncode(signature), expiry, SASKeyName);
            return sasToken;
        }

        private static async Task<HttpResponseMessage> CreateSubscriptionAsync(string baseAddress, string topicName,
            string subscriptionName, string token)
        {
            var subscriptionAddress = baseAddress + topicName + "/Subscriptions/" + subscriptionName;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
            var putData = @"<entry xmlns=""http://www.w3.org/2005/Atom"">
                                  <title type=""text"">" + subscriptionName + @"</title>
                                  <content type=""application/xml"">
                                    <SubscriptionDescription xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.microsoft.com/netservices/2010/10/servicebus/connect"" />
                                  </content>
                                </entry>";
            return
                await client.PutAsync(subscriptionAddress, new ByteArrayContent(Encoding.UTF8.GetBytes(putData)))
                    .ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> DeleteSubscriptionAsync(string baseAddress, string topicName,
            string subscriptionName, string token)
        {
            var subscriptionAddress = baseAddress + topicName + "/Subscriptions/" + subscriptionName;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);

            return await client.DeleteAsync(subscriptionAddress).ConfigureAwait(false);
        }
    }
}