namespace NanoRpcSharp
{
    using NanoRpcSharp.Messages;
    using System.Numerics;
    using System.Threading.Tasks;

    public static class Extensions
    {
        public static async Task<BigInteger> BanToRawAsync(this INanoRpcClient client, BigInteger amount)
        {
            var r = await client.SendAsync(new BanToRawRequest(amount));
            return r.Amount;
        }
    }
}
