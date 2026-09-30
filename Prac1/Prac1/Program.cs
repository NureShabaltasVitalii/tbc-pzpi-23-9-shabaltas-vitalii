using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Prac1;

public sealed record MinerRequest(string Address);
public sealed record PeerRequest(string Url);

public static class Program
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(4) };

    public static async Task Main(string[] args)
    {
        if (args.Contains("--test")) { SelfTests.Run(); return; }
        if (args.Contains("--demo")) { RunDemo(); return; }
        if (args.Contains("--experiment")) { RunExperiment(); return; }
        await RunNode(args);
    }

    private static async Task RunNode(string[] args)
    {
        var port = ReadInt(args, "--port", 5101);
        var dataPath = ReadString(args, "--data", $"chain-{port}.json");
        var chain = File.Exists(dataPath) ? Blockchain.Load(dataPath) : new Blockchain();
        if (!chain.Validate(out var validationError)) throw new InvalidDataException($"Saved chain is invalid: {validationError}");
        var peers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gate = new object();
        var app = WebApplication.CreateBuilder(args).Build();

        app.MapGet("/", () => Results.Ok(new { node = port, blocks = chain.Chain.Count, peers = peers.Count }));
        app.MapGet("/chain", () => Results.Json(chain.Chain, Blockchain.JsonOptions));
        app.MapGet("/mempool", () => Results.Json(chain.Mempool, Blockchain.JsonOptions));
        app.MapGet("/balance/{address}", (string address) => Results.Ok(new { address, balance = chain.BalanceOf(address), nextNonce = chain.NextNonce(address) }));
        app.MapGet("/validate", () => { lock (gate) return Results.Ok(new { valid = chain.Validate(out var reason), reason }); });
        app.MapPost("/wallet", () => { var wallet = Wallet.Create(); return Results.Ok(new { wallet.Address, wallet.PublicKey, wallet.PrivateKey }); });
        app.MapPost("/transactions", async (Transaction transaction) =>
        {
            bool accepted; string reason;
            lock (gate) { accepted = chain.TryAddTransaction(transaction, out reason); }
            if (!accepted) return Results.BadRequest(new { accepted, reason });
            await Broadcast(peers, "/transactions", transaction);
            return Results.Ok(new { accepted, reason, transactionId = transaction.Id });
        });
        app.MapPost("/blocks", (Block block) =>
        {
            bool accepted; string reason;
            lock (gate) { accepted = chain.TryAddBlock(block, out reason); if (accepted) chain.Save(dataPath); }
            return accepted ? Results.Ok(new { accepted, reason }) : Results.BadRequest(new { accepted, reason });
        });
        app.MapPost("/mine", async (MinerRequest request) =>
        {
            Block block;
            try { lock (gate) { block = chain.Mine(request.Address); chain.Save(dataPath); } }
            catch (Exception exception) { return Results.BadRequest(new { error = exception.Message }); }
            await Broadcast(peers, "/blocks", block);
            return Results.Ok(block);
        });
        app.MapPost("/peers", (PeerRequest request) =>
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")) return Results.BadRequest(new { error = "Peer must be an HTTP URL." });
            lock (gate) peers.Add(uri.GetLeftPart(UriPartial.Authority));
            return Results.Ok(new { peers });
        });
        app.MapPost("/sync", async () =>
        {
            var results = new List<string>();
            foreach (var peer in peers.ToList())
            {
                try
                {
                    var blocks = await Client.GetFromJsonAsync<List<Block>>($"{peer}/chain", Blockchain.JsonOptions);
                    if (blocks is null) continue;
                    lock (gate) { if (chain.ReplaceIfBetter(blocks, out var reason)) chain.Save(dataPath); results.Add($"{peer}: {reason}"); }
                }
                catch (Exception exception) { results.Add($"{peer}: unavailable ({exception.GetType().Name})"); }
            }
            return Results.Ok(new { results });
        });

        app.Urls.Add($"http://127.0.0.1:{port}");
        Console.WriteLine($"Node {port} is running. Data: {Path.GetFullPath(dataPath)}");
        await app.RunAsync();
    }

    private static async Task Broadcast<T>(IEnumerable<string> peers, string endpoint, T payload)
    {
        foreach (var peer in peers.ToList())
        {
            try { await Client.PostAsJsonAsync(peer + endpoint, payload, Blockchain.JsonOptions); }
            catch (HttpRequestException) { }
        }
    }

    private static void RunDemo()
    {
        var chain = new Blockchain(); var miner = Wallet.Create(); var receiver = Wallet.Create();
        var first = chain.Mine(miner.Address);
        var payment = miner.Sign(receiver.Address, 3m, 0.1m, chain.NextNonce(miner.Address));
        Console.WriteLine(chain.TryAddTransaction(payment, out var reason) ? "Transaction accepted." : reason);
        var second = chain.Mine(miner.Address);
        Console.WriteLine($"Mined blocks: {first.Hash[..12]}, {second.Hash[..12]}");
        Console.WriteLine($"Receiver balance: {chain.BalanceOf(receiver.Address)}");
        Console.WriteLine(chain.Validate(out var validation) ? validation : $"Invalid: {validation}");
    }

    private static void RunExperiment()
    {
        const int attempts = 10;
        var rows = new List<(int Difficulty, double AverageMs, double MinMs, double MaxMs)>();
        for (var difficulty = 1; difficulty <= 4; difficulty++)
        {
            var times = new List<double>();
            for (var run = 0; run < attempts; run++)
            {
                var block = new Block { Index = 1, TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), PreviousHash = "experiment", MerkleRoot = Crypto.Sha256(run.ToString()), Difficulty = difficulty };
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                do { block.Nonce++; block.Hash = block.CalculateHash(); } while (!block.MeetsProofOfWork());
                stopwatch.Stop(); times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            rows.Add((difficulty, times.Average(), times.Min(), times.Max()));
        }
        File.WriteAllLines("mining-experiment.csv", ["difficulty,average_ms,min_ms,max_ms", .. rows.Select(r => string.Join(",", r.Difficulty, r.AverageMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), r.MinMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), r.MaxMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))]);
        File.WriteAllText("mining-experiment.svg", ExperimentSvg(rows));
        Console.WriteLine("Created mining-experiment.csv and mining-experiment.svg.");
    }

    private static string ExperimentSvg(List<(int Difficulty, double AverageMs, double MinMs, double MaxMs)> rows)
    {
        const int width = 760, height = 420, left = 80, bottom = 360;
        var logMax = Math.Max(1, rows.Max(r => Math.Log10(Math.Max(r.MaxMs, 1))));
        double Y(double ms) => bottom - 270 * Math.Log10(Math.Max(ms, 1)) / logMax;
        double X(int d) => left + (d - 1) * 180;
        var marks = string.Join("", rows.Select(r => $"<line x1='{X(r.Difficulty):F1}' y1='{Y(r.MinMs):F1}' x2='{X(r.Difficulty):F1}' y2='{Y(r.MaxMs):F1}' stroke='#64748b'/><circle cx='{X(r.Difficulty):F1}' cy='{Y(r.AverageMs):F1}' r='5' fill='#0f766e'/><text x='{X(r.Difficulty):F1}' y='385' text-anchor='middle'>{r.Difficulty}</text>"));
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'><rect width='100%' height='100%' fill='white'/><text x='80' y='32' font-family='Arial' font-size='20'>Mining time by difficulty</text><line x1='{left}' y1='70' x2='{left}' y2='{bottom}' stroke='black'/><line x1='{left}' y1='{bottom}' x2='700' y2='{bottom}' stroke='black'/><text x='330' y='410' font-family='Arial'>Difficulty</text><text x='10' y='70' font-family='Arial' font-size='12'>time, ms</text>{marks}</svg>";
    }

    private static string ReadString(string[] args, string name, string fallback) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
    private static int ReadInt(string[] args, string name, int fallback) => int.TryParse(ReadString(args, name, fallback.ToString()), out var value) ? value : fallback;
}
