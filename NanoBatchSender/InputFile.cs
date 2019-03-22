namespace NanoBatchSender
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class InputFile : IDisposable
    {
        private static readonly char[] FieldSeparators = new[] { ' ', '\t', ',', ';' };

        private readonly StreamReader inputFile;

        public InputFile(string file)
        {
            inputFile = new StreamReader(file);
        }

        public int TotalLines { get; private set; }

        public int SkippedLines { get; private set; }

        public async Task<string[]> ReadNextAsync()
        {
            while (true)
            {
                if (inputFile.EndOfStream)
                {
                    return null;
                }

                var s = await inputFile.ReadLineAsync();
                s = s.Trim();
                TotalLines++;

                if (string.IsNullOrWhiteSpace(s) || s.StartsWith('#'))
                {
                    SkippedLines++;
                    continue;
                }

                return s.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
            }
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
                inputFile?.Dispose();
            }
        }
    }
}
