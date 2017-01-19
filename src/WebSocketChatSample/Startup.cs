using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebSocketChatSample
{
    public class Startup
    {
        private readonly ChatServer _chatServer;
        private readonly IConfigurationRoot _configuration;

        public Startup()
        {
            var builder = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                ;
            _configuration = builder.Build();

            _chatServer = new ChatServer();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetim)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            _chatServer.TopicsReciever.NameSpaceUrl =
                _chatServer.TopicsSender.NameSpaceUrl = _configuration.GetSection("ServiceBus:NameSpaceUrl").Value;

            _chatServer.TopicsReciever.PolicyName =
                _chatServer.TopicsSender.PolicyName = _configuration.GetSection("ServiceBus:PolicyName").Value;

            _chatServer.TopicsReciever.Key =
                _chatServer.TopicsSender.Key = _configuration.GetSection("ServiceBus:Key").Value;

            _chatServer.TopicsReciever.Topic =
                _chatServer.TopicsSender.Topic = _configuration.GetSection("ServiceBus:Topic").Value;

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.Map("/ws", _chatServer.Map);

            await _chatServer.EventRecieveEventAsync();

            applicationLifetim.ApplicationStopping.Register(Stopping);
        }

        private void Stopping()
        {
            _chatServer.Dispose();
        }
    }
}