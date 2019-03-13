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
        private const int Fractions = 1000;

        private readonly ILogger logger;

        private readonly SenderOptions options;

        private readonly NanoRpcClient nanoRpcClient;

        public Program(IOptions<SenderOptions> options, NanoRpcClient nanoRpcClient, ILogger<Program> logger)
        {
            this.options = options.Value;
            this.nanoRpcClient = nanoRpcClient;
            this.logger = logger;
        }

        public static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                ShowHelp();
                return;
            }

            var send = "send".Equals(args[0], StringComparison.OrdinalIgnoreCase);
            var check = "check".Equals(args[0], StringComparison.OrdinalIgnoreCase);

            if (send == check)
            {
                ShowHelp();
                return;
            }

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var services = new ServiceCollection()
                .AddLogging(o => o.AddConfiguration(config.GetSection("Logging")).AddConsole())
                .Configure<SenderOptions>(config)
                .AddHttpClient<NanoRpcClient>((s, c) =>
                {
                    var so = s.GetRequiredService<IOptions<SenderOptions>>();
                    c.BaseAddress = new Uri("http://" + so.Value.NodeEndpoint);
                })
                .Services
                .AddSingleton<Program>()
                .BuildServiceProvider();

            var p = services.GetRequiredService<Program>();
            if (send)
            {
                await p.SendAsync(args[1]);
            }

            if (check)
            {
                await p.CheckAsync(args[1]);
            }

            await Task.Delay(500);
        }

        public static void ShowHelp()
        {
            Console.WriteLine("Invalid arguments");
            Console.WriteLine("Usage:");
            Console.WriteLine("    dotnet run {send|check} <inputfile>");
            Console.WriteLine();
            Console.WriteLine("'Send' file format (lines starting # are ignored):");
            Console.WriteLine("    <ban_address> <BAN amount> <node_unique_id>");
            Console.WriteLine();
            Console.WriteLine("'Check' file format (lines starting # are ignored):");
            Console.WriteLine("    <ban_address> <any content (ignored)>");
            Console.WriteLine();
        }

        public async Task<BigInteger> GetMultiplier()
        {
            var version = await nanoRpcClient.VersionAsync();
            logger.LogInformation($"Node vendor: {version.NodeVendor}");

            return version.NodeVendor.StartsWith("Banano")
                ? await nanoRpcClient.BanToRawAsync(new BigInteger(1))
                : await nanoRpcClient.MraiToRawAsync(new BigInteger(1));
        }

        public async Task SendAsync(string fileName)
        {
            logger.LogInformation($@"Preparing to send:
wallet {options.Wallet}
  from {options.Source}
   via {options.NodeEndpoint}");

            var multiplier = await GetMultiplier();

            logger.LogInformation($"1 coin == {multiplier} raw");

            var totalLineCount = 0;
            var skippedLineCount = 0;
            var invalidLineCount = 0;
            var paymentsCount = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            string msg;

            using (var inputFile = new StreamReader(fileName))
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
                        var amountRaw = multiplier * (BigInteger)Math.Truncate(amount * Fractions) / Fractions;

                        var valid = await nanoRpcClient.ValidateAccountNumberAsync(account);
                        if (valid != 1)
                        {
                            msg = $"Invalid account: {account}, skipped";
                            await outputFile.WriteLineAsync(msg);
                            logger.LogWarning(msg);
                            invalidLineCount++;
                            continue;
                        }

                        var req = new SendRequest(options.Wallet, options.Source, account, amountRaw, id);
                        var resp = await nanoRpcClient.SendAsync(req);
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

                    msg = $"Elapsed: {stopwatch.Elapsed}";
                    await outputFile.WriteLineAsync(msg);
                    logger.LogInformation(msg);

                    await outputFile.WriteLineAsync();
                }
            }

            logger.LogInformation("Done");
        }

        public async Task CheckAsync(string fileName)
        {
            logger.LogInformation($@"Preparing to check:
   via {options.NodeEndpoint}");

            var multiplier = await GetMultiplier();

            logger.LogInformation($"1 coin == {multiplier} raw");

            var totalLineCount = 0;
            var skippedLineCount = 0;
            var invalidLineCount = 0;
            var openCount = 0;
            var notOpenCount = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            string msg;

            using (var inputFile = new StreamReader(fileName))
            {
                using (var outputFile = new StreamWriter("check_done.txt", true))
                {
                    outputFile.AutoFlush = true;

                    await outputFile.WriteLineAsync();
                    await outputFile.WriteLineAsync();
                    await outputFile.WriteLineAsync($"Accounts balance check, started at {DateTimeOffset.Now}");
                    await outputFile.WriteLineAsync();

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

                        var account = new Account(parts[0]);

                        var valid = await nanoRpcClient.ValidateAccountNumberAsync(account);
                        if (valid != 1)
                        {
                            msg = $"{account} Invalid account (skipped)";
                            await outputFile.WriteLineAsync(msg);
                            logger.LogWarning(msg);
                            invalidLineCount++;
                            continue;
                        }

                        try
                        {
                            // throws exception of account not open
                            var blockCount = await nanoRpcClient.AccountBlockCountAsync(account);

                            var balanceRaw = await nanoRpcClient.AccountBalanceAsync(account);
                            var balance = ((decimal)(balanceRaw.Balance / multiplier * Fractions)) / Fractions;
                            msg = $"{account} {balance} BAN";
                            await outputFile.WriteLineAsync(msg);
                            logger.LogInformation(msg);
                            openCount++;
                        }
                        catch (ErrorResponseException ex) when (ex.Message == "Account not found")
                        {
                            msg = $"{account} Not found (not open)";
                            await outputFile.WriteLineAsync(msg);
                            logger.LogWarning(msg);
                            notOpenCount++;
                        }
                    }

                    await outputFile.WriteLineAsync();

                    msg = $"Total: {totalLineCount} lines = {skippedLineCount} skipped + {invalidLineCount} invalid + {openCount} open + {notOpenCount} not open";
                    await outputFile.WriteLineAsync(msg);
                    logger.LogInformation(msg);

                    msg = $"Elapsed: {stopwatch.Elapsed}";
                    await outputFile.WriteLineAsync(msg);
                    logger.LogInformation(msg);

                    await outputFile.WriteLineAsync("By NanoBatchSender: https://github.com/justdmitry/NanoBatchSender");
                    await outputFile.WriteLineAsync();
                }
            }

            logger.LogInformation("Done");
        }
    }
}
