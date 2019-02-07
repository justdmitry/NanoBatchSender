namespace NanoRpcSharp.Messages
{
    using NanoRpcSharp;
    using System.Numerics;

    public class BanToRawRequest : RequestBase<BigIntegerAmount>
    {
        public BanToRawRequest(BigInteger amount)
            : base("ban_to_raw")
        {
            this.Amount = amount;
        }

        public BigInteger Amount { get; set; }
    }
}