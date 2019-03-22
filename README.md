# NANO Batch Sender 

Command-line tool for mass sending coins in Nano-based networks.

## Features

* Auto-detecting of nano/banano nodes, auto rai/ban conversion to raw and back
* Uses `id` of RPC `Send` command, no more double-sends, guaranteed by Node itself (see [Send command](https://github.com/nanocurrency/nano-node/wiki/RPC-protocol#send) description)
* Cross-platform (install [.NET Core SDK](https://dotnet.microsoft.com/download) for your OS)
* **New**: Check for account open/balance (without sending anything)
* Results (account status, block hashes) are written to output file


## Installation

1. Download and install [.NET Core SDK](https://dotnet.microsoft.com/download) 2.2 or above (but not ".NET Framework"!)
2. Clone this repo
3. `cd NanoBatchSender`
4. `dotnet build` (should see green "Build succeeded" text)


## How to use

1. Edit `appsettings.json`: 
   * Put you node RPC endpoint
   * For `send`, also put your wallet id and account
2. Prepare input file (see syntax below)
3. Run `dotnet run send <inputfile>` or `dotnet run balance <inputfile>` from console

### Input file format

Lines starting with `#` and empty lines are ignored;

Line should contain one or several fields, separated by tab (`\t`) or space (` `) or comma (`,`) or semicolon (`;`)

For `send` operation line must contain exactly 3 fields: account, amount and uniqueId:
```
ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh 1.5 FooBar
ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh 254 FooBar2
```

For `balance` operation, line may contain any number of fields, only first field wil be used (must be account to check):
```
ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh and any you want
ban_3pa1m3g79i1h7uijugndjeytpmqbsg6hc19zm8m7foqygwos1mmcqmab91hh
```

Note: line format for `send` operation is valid for `balance` operation! You can prepare one file (for `send`) and run `balance` with it, without any change.

**Important!** For non-integer amounts use dot as delimiter: `12345.67`

**Attention!** No more than 3 decimal places in amounts! Contact me if you need more.

**Attention 2!** Make sure you `id` are unique across node database! There is no info about "new block created" or "id already used before, skipped" in node answer (V17.1).


### Output file

File is always appended, not overwritten.

File names are fixed: `send_done.txt` and `balance_done.txt` for `send` and `balance` respectively.

File will contain some header and footer lines with useful info and time elapsed, you need to manually remove them before importing output file into your database(s). 

**Once again!** There is no info about "new block created" or "id already used before, skipped" in node answer (V17.1). If you provide non-unique `id` in input file - node will return hash of old/previous block, no payment will be made, and there is no any sign of this in node response and in output file.
