using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebSocketChatSample.Models;

namespace WebSocketChatSample
{
    public class Startup
    {
        private readonly IConfigurationRoot _configuration;
        private ChatServer _chatServer;

        public Startup()
        {
            var builder = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                ;
            _configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Add(new ServiceDescriptor(typeof(IRoomService), typeof(DebugRoomService), ServiceLifetime.Singleton));
            services.Add(new ServiceDescriptor(typeof(ChatServer), typeof(ChatServer), ServiceLifetime.Singleton));

            _chatServer = services.BuildServiceProvider().GetService<ChatServer>();
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

            app.UseMvc();

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