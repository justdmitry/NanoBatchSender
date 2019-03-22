namespace NanoBatchSender
{
    using System;
    using System.Globalization;
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
            var balance = "balance".Equals(args[0], StringComparison.OrdinalIgnoreCase);

            if (send == balance)
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
                await p.SendAsync(args[1], "send_done.txt");
            }

            if (balance)
            {
                await p.BalanceAsync(args[1], "balance_done.txt");
            }

            // Wait for async logging
            await Task.Delay(500);
        }

        public static void ShowHelp()
        {
            Console.WriteLine("Invalid arguments");
            Console.WriteLine("Usage:");
            Console.WriteLine("    dotnet run {send|balance} <inputfile>");
            Console.WriteLine();
            Console.WriteLine("'send' file format (lines starting # are ignored):");
            Console.WriteLine("    <ban_address> <BAN amount> <node_unique_id>");
            Console.WriteLine();
            Console.WriteLine("'balance' file format (lines starting # are ignored):");
            Console.WriteLine("    <ban_address> <any content (ignored)>");
            Console.WriteLine();
            Console.WriteLine("Fields in <inputfile> may be separated by space or tab or comma or semicolon");
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

        public async Task SendAsync(string inputFileName, string outputFileName)
        {
            logger.LogInformation($@"Preparing to send:
wallet {options.Wallet}
  from {options.Source}
   via {options.NodeEndpoint}");

            var multiplier = await GetMultiplier();

            logger.LogInformation($"1 coin == {multiplier} raw");

            var invalidCount = 0;
            var paymentsCount = 0;

            using (var outputFile = new OutputFile(outputFileName))
            {
                await outputFile.WriteHeaderAsync("Payments");

                using (var inputFile = new InputFile(inputFileName))
                {
                    while (true)
                    {
                        var data = await inputFile.ReadNextAsync();

                        if (data == null)
                        {
                            break;
                        }

                        if (data.Length != 3)
                        {
                            await outputFile.WriteLinesAsync($"Invalid line {inputFile.TotalLines}: wrong parts count ({data.Length}), skipped");
                            invalidCount++;
                            continue;
                        }

                        var account = new Account(data[0]);

                        if (await nanoRpcClient.ValidateAccountNumberAsync(account) != 1)
                        {
                            await outputFile.WriteLinesAsync($"{account} Invalid account (skipped)");
                            invalidCount++;
                            continue;
                        }

                        var amount = decimal.Parse(data[1], CultureInfo.InvariantCulture);
                        var id = data[2].ToUpperInvariant();

                        // only 3 decimal digits are supported (don't ask why, I just want it)
                        var amountRaw = multiplier * (BigInteger)Math.Truncate(amount * Fractions) / Fractions;

                        var req = new SendRequest(options.Wallet, options.Source, account, amountRaw, id);
                        var resp = await nanoRpcClient.SendAsync(req);
                        var block = resp.Block;

                        await outputFile.WriteLinesAsync($"{DateTimeOffset.Now.ToString("HH:mm:ss")} {block} {account} {amount}");
                        paymentsCount++;
                    }

                    await outputFile.WriteLineAsync();

                    var msg = $"Total: {inputFile.TotalLines} lines = {inputFile.SkippedLines} skipped + {invalidCount} invalid + {paymentsCount} payments";
                    await outputFile.WriteLinesAsync(msg);

                    await outputFile.WriteFooterAsync();
                }
            }

            logger.LogInformation("Done");
        }

        public async Task BalanceAsync(string inputFileName, string outputFileName)
        {
            logger.LogInformation($@"Preparing to check balances:
   via {options.NodeEndpoint}");

            var multiplier = await GetMultiplier();

            logger.LogInformation($"1 coin == {multiplier} raw");

            var invalidCount = 0;
            var openCount = 0;
            var notOpenCount = 0;

            using (var outputFile = new OutputFile(outputFileName))
            {
                await outputFile.WriteHeaderAsync("Accounts balance check");

                using (var inputFile = new InputFile(inputFileName))
                {
                    while (true)
                    {
                        var data = await inputFile.ReadNextAsync();

                        if (data == null)
                        {
                            break;
                        }

                        var account = new Account(data[0]);

                        if (await nanoRpcClient.ValidateAccountNumberAsync(account) != 1)
                        {
                            await outputFile.WriteLinesAsync($"{account} Invalid account (skipped)");
                            invalidCount++;
                            continue;
                        }

                        try
                        {
                            // throws exception of account not open
                            var blockCount = await nanoRpcClient.AccountBlockCountAsync(account);

                            var balanceRaw = await nanoRpcClient.AccountBalanceAsync(account);
                            var balance = ((decimal)(balanceRaw.Balance / multiplier * Fractions)) / Fractions;
                            await outputFile.WriteLinesAsync($"{account} {balance} BAN");
                            openCount++;
                        }
                        catch (ErrorResponseException ex) when (ex.Message == "Account not found")
                        {
                            await outputFile.WriteLinesAsync($"{account} Not found (not open)");
                            notOpenCount++;
                        }
                    }

                    await outputFile.WriteLineAsync();

                    var msg = $"Total: {inputFile.TotalLines} lines = {inputFile.SkippedLines} skipped + {invalidCount} invalid + {openCount} open + {notOpenCount} not open";
                    await outputFile.WriteLinesAsync(msg);
                }

                await outputFile.WriteFooterAsync();
            }

            logger.LogInformation("Done.");
        }
    }
}

