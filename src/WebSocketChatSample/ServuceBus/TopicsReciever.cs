using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Amqp;
using Amqp.Framing;
using Microsoft.AspNetCore.WebSockets.Protocol;
using Newtonsoft.Json;

namespace WebSocketChatSample.ServuceBus
{
    public class TopicsReciever : TopicsClientBase, IObservable<ChatMessage>, IDisposable
    {
        private string ReceiverSubscriptionId { get; } = "receiver-link" + Guid.NewGuid().ToString();
        private string Subscription { get; } = Guid.NewGuid().ToString();
        private ReceiverLink _consumer;

        public async Task RecieveAsync()
        {
            var put = await CreateSubscriptionAsync(BaseUrl, Topic, Subscription, GetSASToken(BaseUrl, PolicyName, Key));

            _consumer = new ReceiverLink(GetSession(GetConnection()), ReceiverSubscriptionId, $"{Topic}/Subscriptions/{Subscription}");

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

        private readonly Subject<ChatMessage> _receiveSubject = new Subject<ChatMessage>();
        public IDisposable Subscribe(IObserver<ChatMessage> observer)
        {
            return _receiveSubject.Subscribe(observer);
        }

        private static string GetSASToken(string baseAddress, string SASKeyName, string SASKeyValue)
        {
            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            string expiry = Convert.ToString((int)fromEpochStart.TotalSeconds + 3600);
            string stringToSign = WebUtility.UrlEncode(baseAddress) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SASKeyValue));

            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            string sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                WebUtility.UrlEncode(baseAddress), WebUtility.UrlEncode(signature), expiry, SASKeyName);
            return sasToken;
        }

        private static async Task<HttpResponseMessage> CreateSubscriptionAsync(string baseAddress, string topicName, string subscriptionName, string token)
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
            return await client.PutAsync(subscriptionAddress, new ByteArrayContent(Encoding.UTF8.GetBytes(putData))).ConfigureAwait(false);
            
        }
        private static async Task<HttpResponseMessage> DeleteSubscriptionAsync(string baseAddress, string topicName, string subscriptionName, string token)
        {
            var subscriptionAddress = baseAddress + topicName + "/Subscriptions/" + subscriptionName;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);

            return await client.DeleteAsync(subscriptionAddress).ConfigureAwait(false);
        }

        public void Dispose()
        {
            var result = DeleteSubscriptionAsync(BaseUrl, Topic, Subscription, GetSASToken(BaseUrl, PolicyName, Key)).GetAwaiter().GetResult();
        }
    }
}
