# NANO Batch Sender 

Command-line tool for mass sending coins in Nano and Banano networks.

## Features

* Auto-detecting of nano/banano nodes, auto rai/ban conversion to raw
* Uses `id` of RPC `Send` command, no more double-sends, guaranteed by Node itself
* Cross-platform (install [.NET Core SDK](https://dotnet.microsoft.com/download) for your OS)
* Block hashes are written to output file


## Installation

1. Download and install [.NET Core SDK](https://dotnet.microsoft.com/download) 2.2 or above (but not ".NET Framework"!)
2. Clone this repo
3. `cd NanoBatchSender`
4. `dotnet build` (should see green "Build succeeded" text)


## How to use

1. Edit `appsettings.json`: put your wallet id, send account, correct ip and port of your node RPC
2. Edit `payments.txt`: paste account, amount and id, one per line
3. Run `dotnet run` from console

### `payments.txt` sample

```
# Format:
#   Space, Tab, Comma (,) or Semicolon (;) delimited fields:
#     1) ban_ or nano_ or xrb_ address
#     2) amount (in nano/banano, not raw), no grouping, decimal paced by dot: 12345.67
#     3) id (for RPC Send command)
#
# Lines starting with # and empty lines are ignored
#
# Sample line:

ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh 1 FooBar
ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh 2 FooBar2
```

For non-integer amounts use dot as delimiter: `12345.67`

**Attention!** No more than 3 decimal places in amounts! Contact me if you need more.

**Attention 2!** Make sure you `id` are unique across node database! There is no info about "new block created" or "id already used before, skipped" in node answer (V17.1).


### `payments_done.txt` sample

File is always appended, not overwritten.

```
Payments, started at 2/8/19 1:23:01 AM +03:00
01:23:01 7D0684EDAED87E65CFDD7AD77D7BC35825995DBEA3C504B7AA4A2D048136993D ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 1
01:23:05 361EDAA885ECC9B648C046E4EFCC0295FD00A712427F996829A3E55488D86694 ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 1.5
01:23:09 E53E35169796F206548D77EBACBBDE66466DE8A0876233933081A23B068C450C ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 1.5
01:23:10 7D0684EDAED87E65CFDD7AD77D7BC35825995DBEA3C504B7AA4A2D048136993D ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 3
01:23:10 09EA5641B7EECEFEFE4FB1B43C5E978CBD051BBA71FB853FBD261D9E5A81C4C4 ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 2
01:23:15 C2E86C20194644423A0898D57FBAF1C031E619EAB8E1D6D67BD2BB40C1AC07D8 ban_1bdp693w9jy34ywgz8ih5mnxmogmfbku4yowjz3akobeb47r64c8btbwzbm6 1
01:23:18 04D312074C21E7D87C961514ADED44D0D0506CEC31FFCF1E42E72788F6CFB411 ban_1pigmjky7be47uwh9wg1y3nf74uc5dhtkonjnibcr69oisjsntkfk4xoj6fw 10

Total: 18 lines = 11 skipped + 0 invalid + 7 payments
  Elapsed: 00:00:17.4053891
```

**Attention!** There is no info about "new block created" or "id already used before, skipped" in node answer (V17.1). If you provide non-unique `id` in input file - node will return hash of old/previous block, and there is no any sign of this in node response and in output file.
