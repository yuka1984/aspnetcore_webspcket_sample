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
        public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            _chatServer.EhConnectionString = _configuration.GetSection("EventHubs:EhConnectionString").Value;
            _chatServer.EhEntityPath = _configuration.GetSection("EventHubs:EhEntityPath").Value;
            _chatServer.StorageContainerName = _configuration.GetSection("EventHubs:StorageContainerName").Value;
            _chatServer.StorageAccountKey = _configuration.GetSection("EventHubs:StorageAccountKey").Value;
            _chatServer.StorageAccountName = _configuration.GetSection("EventHubs:StorageAccountName").Value;

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.Map("/ws", _chatServer.Map);

            await _chatServer.EventRecieveEventAsync();
        }
    }
}