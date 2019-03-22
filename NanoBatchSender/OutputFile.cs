namespace NanoBatchSender
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class OutputFile : IDisposable
    {
        private readonly StreamWriter outputFile;

        private readonly DateTimeOffset startTime;

        public OutputFile(string file)
        {
            outputFile = new StreamWriter(file, true)
            {
                AutoFlush = true
            };
            startTime = DateTimeOffset.Now;
        }

        public async Task WriteHeaderAsync(string header)
        {
            await WriteLinesAsync(string.Empty, string.Empty, $"{header}, started at {startTime}", string.Empty);
        }

        public Task WriteLineAsync()
        {
            Console.WriteLine();
            return outputFile.WriteLineAsync();
        }

        public Task WriteLinesAsync(params string[] values)
        {
            var value = string.Join(Environment.NewLine, values);
            Console.WriteLine(value);
            return outputFile.WriteLineAsync(value);
        }

        public async Task WriteFooterAsync()
        {
            await WriteLinesAsync($"Elapsed: {DateTimeOffset.Now.Subtract(startTime)}", string.Empty);
            await WriteLinesAsync("By NanoBatchSender: https://github.com/justdmitry/NanoBatchSender", string.Empty);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                outputFile?.Dispose();
            }
        }
    }
}
