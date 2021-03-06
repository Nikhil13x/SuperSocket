using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using Xunit;

namespace Tests
{
    public class MainTest
    {
        protected virtual void RegisterServices(IServiceCollection services)
        {

        }

        protected SuperSocketServer CreateSocketServer<TPackageInfo, TPipelineFilter>(Dictionary<string, string> configDict = null, Action<IAppSession, TPackageInfo> packageHandler = null)
            where TPackageInfo : class
            where TPipelineFilter : IPipelineFilter<TPackageInfo>, new()
        {
            if (configDict == null)
            {
                configDict = new Dictionary<string, string>
                {
                    { "serverOptions:name", "TestServer" },
                    { "serverOptions:listeners:0:ip", "Any" },
                    { "serverOptions:listeners:0:port", "4040" }
                };
            }

            var server = new SuperSocketServer();

            var services = new ServiceCollection();

            var builder = new ConfigurationBuilder().AddInMemoryCollection(configDict);
            var config = builder.Build();
            
            services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

            RegisterServices(services);

            var serverOptions = new ServerOptions();
            config.GetSection("serverOptions").Bind(serverOptions);

            Assert.True(server.Configure<TPackageInfo, TPipelineFilter>(serverOptions, services, packageHandler: packageHandler));

            return server;
        }

        [Fact]
        public async Task TestSessionCount() 
        {
            var server = CreateSocketServer<LinePackageInfo, LinePipelineFilter>(packageHandler: async (s, p) =>
            {
                await s.Channel.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(p.Line + "\r\n")));
            });

            Assert.Equal("TestServer", server.Name);

            Assert.True(await server.StartAsync());
            Assert.Equal(0, server.SessionCount);

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4040));

            await Task.Delay(1);

            Assert.Equal(1, server.SessionCount);

            await server.StopAsync();
        }

        //[Fact]
        public async Task TestConsoleProtocol() 
        {
            var server = CreateSocketServer<LinePackageInfo, LinePipelineFilter>();

            
            Assert.True(await server.StartAsync());
            Assert.Equal(0, server.SessionCount);

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4040));
            
            using (var stream = new NetworkStream(client))
            using (var streamReader = new StreamReader(stream, Encoding.UTF8, true))
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1024 * 1024 * 4))
            {
                await streamWriter.WriteLineAsync("Hello World");
                await streamWriter.FlushAsync();
                var line = await streamReader.ReadLineAsync();
                Assert.Equal("Hello World", line);
            }

            await server.StopAsync();
        }
    }
}
