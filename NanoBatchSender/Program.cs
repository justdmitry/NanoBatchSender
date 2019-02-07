namespace NanoBatchSender
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Numerics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NanoRpcSharp;
    using NanoRpcSharp.Messages;

    public class Program
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

            await new Program().SendAsync(services);
        }

        public async Task SendAsync(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();

            var options = services.GetRequiredService<IOptions<SenderOptions>>().Value;

            logger.LogInformation($@"Ready to send:
wallet {options.Wallet}
  from {options.Source}
   via {options.NodeEndpoint}");

            var client = new HttpClient() { BaseAddress = new Uri("http://" + options.NodeEndpoint) };

            var nanoClient = new NanoRpcClient(client, services.GetRequiredService<ILogger<NanoRpcClient>>());

            var version = await nanoClient.VersionAsync();
            logger.LogInformation($"Node vendor: {version.NodeVendor}");

            var multiplier = version.NodeVendor.StartsWith("Banano")
                ? await nanoClient.BanToRawAsync(new BigInteger(1))
                : await nanoClient.MraiToRawAsync(new BigInteger(1));

            logger.LogInformation($"1 coin == {multiplier} raw");

            var totalLineCount = 0;
            var skippedLineCount = 0;
            var invalidLineCount = 0;
            var paymentsCount = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            string msg;

            using (var inputFile = new StreamReader("payments.txt"))
            {
                using (var outputFile = new StreamWriter("payments_done.txt", true))
                {
                    outputFile.AutoFlush = true;

                    await outputFile.WriteLineAsync();
                    await outputFile.WriteLineAsync();
                    await outputFile.WriteLineAsync($"Payments, started at {DateTimeOffset.Now}");

                    while (!inputFile.EndOfStream)
                    {
                        var s = await inputFile.ReadLineAsync();
                        s = s.Trim();
                        totalLineCount++;

                        if (string.IsNullOrWhiteSpace(s) || s.StartsWith('#'))
                        {
                            skippedLineCount++;
                            continue;
                        }

                        var parts = s.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3)
                        {
                            msg = $"Invalid line {totalLineCount}: wrong parts count ({parts.Length}), skipped";
                            await outputFile.WriteLineAsync(msg);
                            logger.LogWarning(msg);
                            invalidLineCount++;
                            continue;
                        }

                        var account = new Account(parts[0]);
                        var amount = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                        var id = parts[2].ToUpperInvariant();

                        // only 3 decimal digits are supported (don't ask why, I just want it)
                        var amountRaw = multiplier * (BigInteger)Math.Truncate(amount * 1000) / 1000;

                        var req = new SendRequest(options.Wallet, options.Source, account, amountRaw, id);
                        var resp = await nanoClient.SendAsync(req);
                        var block = resp.Block;

                        msg = $"{DateTimeOffset.Now.ToString("HH:mm:ss")} {block} {account} {amount}";
                        await outputFile.WriteLineAsync(msg);
                        logger.LogInformation(msg);

                        paymentsCount++;
                    }

                    await outputFile.WriteLineAsync();

                    msg = $"Total: {totalLineCount} lines = {skippedLineCount} skipped + {invalidLineCount} invalid + {paymentsCount} payments";
                    await outputFile.WriteLineAsync(msg);
                    logger.LogInformation(msg);

                    msg = $"  Elapsed: {stopwatch.Elapsed}";
                    await outputFile.WriteLineAsync(msg);
                    logger.LogInformation(msg);

                    await outputFile.WriteLineAsync();
                }
            }

            logger.LogInformation("Done");
        }
    }
}
