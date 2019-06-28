namespace NanoBatchSender
{
    using System;
    using NanoRpcSharp;

    public class SenderOptions
    {
        public string Wallet { get; set; }

        public Account Source { get; set; }
    }
}
