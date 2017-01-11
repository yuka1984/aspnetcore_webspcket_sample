using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebSocketChatSample
{
    public class Startup
    {
        private readonly ChatServer chatServer = new ChatServer();

        private readonly IConfigurationRoot Configuration;

        public Startup()
        {
            var builder = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                ;
            Configuration = builder.Build();
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

            chatServer.EhConnectionString = Configuration.GetSection("EventHubs:EhConnectionString").Value;
            chatServer.EhEntityPath = Configuration.GetSection("EventHubs:EhEntityPath").Value;
            chatServer.StorageContainerName = Configuration.GetSection("EventHubs:StorageContainerName").Value;
            chatServer.StorageAccountKey = Configuration.GetSection("EventHubs:StorageAccountKey").Value;
            chatServer.StorageAccountName = Configuration.GetSection("EventHubs:StorageAccountName").Value;

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.Map("/ws", chatServer.Map);

            await chatServer.EventRecieveEventAsync();
        }
    }
}