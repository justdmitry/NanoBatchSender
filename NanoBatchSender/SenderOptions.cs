namespace NanoBatchSender
{
    using System;
    using NanoRpcSharp;

    public class SenderOptions
    {
        public string Wallet { get; set; }

        public Account Source { get; set; }

        public Account Destination { get; set; }

        public decimal Amount { get; set; }

        public int Count { get; set; }

        public string NodeEndpoint { get; set; }
    }
}
