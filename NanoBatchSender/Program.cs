namespace NanoBatchSender
{
    using System;
    using System.Net.Http;
    using System.Numerics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NanoRpcSharp;
    using NanoRpcSharp.Messages;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var services = new ServiceCollection()
                .AddLogging(o => o.AddConsole())
                .Configure<SenderOptions>(config)
                .BuildServiceProvider();

            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Program");

            var options = services.GetRequiredService<IOptions<SenderOptions>>().Value;

            logger.LogInformation($@"Ready to send:
wallet {options.Wallet}
  from {options.Source}
    to {options.Destination}
       {options.Amount} x {options.Count} times
   via {options.NodeEndpoint}");

            var client = new HttpClient() { BaseAddress = new Uri("http://" + options.NodeEndpoint) };

            var nanoClient = new NanoRpcClient(client, services.GetRequiredService<ILogger<NanoRpcClient>>());

            var amount = await nanoClient.RaiToRawAsync(new BigInteger(options.Amount * 1000000));

            for (var i = 0; i < options.Count; i++)
            {
                var req = new SendRequest(options.Wallet, options.Source, options.Destination, amount, Guid.NewGuid().ToString());
                var resp = await nanoClient.SendAsync(req);
                logger.LogInformation($"{i}: {resp.Block}");
            }

            logger.LogInformation("Done");
        }
    }
}
