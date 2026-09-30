using System.Numerics;
using System.Text.Json;

namespace Prac1;

public sealed class Blockchain
{
    public const decimal BlockReward = 10m;
    public const int RetargetInterval = 5;
    public const int TargetBlockSeconds = 2;
    private readonly List<Block> _chain = [];
    private readonly List<Transaction> _mempool = [];
    public IReadOnlyList<Block> Chain => _chain;
    public IReadOnlyList<Transaction> Mempool => _mempool;

    public Blockchain(IEnumerable<Block>? blocks = null)
    {
        if (blocks is null) _chain.Add(CreateGenesis());
        else _chain.AddRange(blocks);
    }

    public static Block CreateGenesis()
    {
        var block = new Block { Index = 0, TimestampUnixMs = 0, PreviousHash = "0", Difficulty = 1, Nonce = 0, Transactions = [] };
        block.MerkleRoot = Block.CalculateMerkleRoot(block.Transactions);
        block.Hash = block.CalculateHash();
        return block;
    }

    public decimal BalanceOf(string address, IEnumerable<Transaction>? pending = null)
    {
        decimal balance = 0;
        foreach (var tx in _chain.SelectMany(b => b.Transactions).Concat(pending ?? []))
        {
            if (tx.Recipient == address) balance += tx.Amount;
            if (tx.Sender == address) balance -= tx.Amount + tx.Fee;
        }
        return balance;
    }

    public long NextNonce(string address, IEnumerable<Transaction>? pending = null)
    {
        var all = _chain.SelectMany(b => b.Transactions).Concat(pending ?? []).Where(t => t.Sender == address);
        return all.Any() ? all.Max(t => t.Nonce) + 1 : 0;
    }

    public bool TryAddTransaction(Transaction tx, out string reason)
    {
        if (tx.IsCoinbase) { reason = "Coinbase transactions are created only by mining."; return false; }
        if (string.IsNullOrWhiteSpace(tx.Recipient) || tx.Amount <= 0 || tx.Fee < 0) { reason = "Amount, fee, or recipient is invalid."; return false; }
        if (!tx.HasValidSignature()) { reason = "Digital signature is invalid."; return false; }
        if (AllTransactions().Any(t => t.Id == tx.Id) || _mempool.Any(t => t.Id == tx.Id)) { reason = "Transaction is already known."; return false; }
        if (tx.Nonce != NextNonce(tx.Sender, _mempool)) { reason = "Nonce is not the next value for the sender."; return false; }
        if (BalanceOf(tx.Sender, _mempool) < tx.Amount + tx.Fee) { reason = "Insufficient confirmed and pending balance."; return false; }
        _mempool.Add(tx);
        reason = "Accepted.";
        return true;
    }

    public Block Mine(string minerAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(minerAddress)) throw new ArgumentException("Miner address is required.");
        var selected = _mempool.OrderByDescending(t => t.Fee).ThenBy(t => t.Sender).ThenBy(t => t.Nonce).ToList();
        var fees = selected.Sum(t => t.Fee);
        var block = new Block
        {
            Index = _chain.Count,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PreviousHash = _chain[^1].Hash,
            Difficulty = NextDifficulty(),
            Transactions = [Transaction.Reward(minerAddress, BlockReward + fees, _chain.Count), .. selected]
        };
        block.MerkleRoot = Block.CalculateMerkleRoot(block.Transactions);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            block.Nonce++;
            block.Hash = block.CalculateHash();
        } while (!block.MeetsProofOfWork());
        if (!TryAddBlock(block, out var reason)) throw new InvalidOperationException(reason);
        return block;
    }

    public bool TryAddBlock(Block block, out string reason)
    {
        var candidate = _chain.Append(block).ToList();
        if (!Validate(candidate, out reason)) return false;
        _chain.Add(block);
        var included = block.Transactions.Select(t => t.Id).ToHashSet();
        _mempool.RemoveAll(t => included.Contains(t.Id));
        return true;
    }

    public bool ReplaceIfBetter(IEnumerable<Block> received, out string reason)
    {
        var candidate = received.ToList();
        if (!Validate(candidate, out reason)) return false;
        if (Work(candidate) <= Work(_chain)) { reason = "Local chain has equal or greater cumulative work."; return false; }
        var old = AllTransactions().Where(t => !t.IsCoinbase).ToList();
        _chain.Clear();
        _chain.AddRange(candidate);
        var newIds = AllTransactions().Select(t => t.Id).ToHashSet();
        _mempool.Clear();
        foreach (var tx in old.Where(t => !newIds.Contains(t.Id))) TryAddTransaction(tx, out _);
        reason = "Chain replaced by the chain with more cumulative work.";
        return true;
    }

    public bool Validate(out string reason) => Validate(_chain, out reason);

    public static bool Validate(IReadOnlyList<Block> blocks, out string reason)
    {
        if (blocks.Count == 0) { reason = "Chain is empty."; return false; }
        var expectedGenesis = CreateGenesis();
        if (blocks[0].Hash != expectedGenesis.Hash || blocks[0].HeaderPayload() != expectedGenesis.HeaderPayload() || blocks[0].Transactions.Count != 0) { reason = "Genesis block differs from the agreed genesis block."; return false; }
        var balances = new Dictionary<string, decimal>();
        var nonces = new Dictionary<string, long>();
        var knownIds = new HashSet<string>();
        for (var i = 1; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var previous = blocks[i - 1];
            if (block.Index != i || block.PreviousHash != previous.Hash || block.TimestampUnixMs < previous.TimestampUnixMs) { reason = $"Broken link or timestamp at block {i}."; return false; }
            if (block.Difficulty != ExpectedDifficulty(blocks, i) || block.Hash != block.CalculateHash() || !block.MeetsProofOfWork()) { reason = $"Invalid difficulty, hash, or proof of work at block {i}."; return false; }
            if (block.MerkleRoot != Block.CalculateMerkleRoot(block.Transactions)) { reason = $"Invalid Merkle root at block {i}."; return false; }
            if (block.Transactions.Count == 0 || !block.Transactions[0].IsCoinbase) { reason = $"Missing coinbase at block {i}."; return false; }
            var reward = block.Transactions[0];
            var fees = block.Transactions.Skip(1).Sum(t => t.Fee);
            if (reward.Amount != BlockReward + fees || reward.Nonce != block.Index || reward.Fee != 0 || !reward.HasValidSignature()) { reason = $"Invalid miner reward at block {i}."; return false; }
            AddBalance(balances, reward.Recipient, reward.Amount);
            foreach (var tx in block.Transactions.Skip(1))
            {
                if (tx.IsCoinbase || !tx.HasValidSignature()) { reason = $"Invalid transaction signature at block {i}."; return false; }
                if (!knownIds.Add(tx.Id)) { reason = $"Duplicate transaction at block {i}."; return false; }
                var expectedNonce = nonces.TryGetValue(tx.Sender, out var current) ? current + 1 : 0;
                if (tx.Nonce != expectedNonce) { reason = $"Invalid sender nonce at block {i}."; return false; }
                if (GetBalance(balances, tx.Sender) < tx.Amount + tx.Fee) { reason = $"Overspend at block {i}."; return false; }
                AddBalance(balances, tx.Sender, -(tx.Amount + tx.Fee));
                AddBalance(balances, tx.Recipient, tx.Amount);
                nonces[tx.Sender] = tx.Nonce;
            }
        }
        reason = "Chain is valid.";
        return true;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(_chain, JsonOptions));
    public static Blockchain Load(string path) => new(JsonSerializer.Deserialize<List<Block>>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("No blocks in file."));
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private int NextDifficulty() => ExpectedDifficulty(_chain, _chain.Count);

    private static int ExpectedDifficulty(IReadOnlyList<Block> blocks, int nextIndex)
    {
        var current = blocks[nextIndex - 1].Difficulty;
        if (nextIndex <= RetargetInterval || nextIndex % RetargetInterval != 0) return current;
        var start = blocks[nextIndex - RetargetInterval - 1].TimestampUnixMs;
        var actual = Math.Max(1, blocks[nextIndex - 1].TimestampUnixMs - start);
        var target = RetargetInterval * TargetBlockSeconds * 1000L;
        return actual < target / 2 ? current + 1 : actual > target * 2 ? Math.Max(1, current - 1) : current;
    }

    private IEnumerable<Transaction> AllTransactions() => _chain.SelectMany(b => b.Transactions);
    private static decimal GetBalance(Dictionary<string, decimal> balances, string address) => balances.GetValueOrDefault(address);
    private static void AddBalance(Dictionary<string, decimal> balances, string address, decimal amount) => balances[address] = GetBalance(balances, address) + amount;
    public static BigInteger Work(IEnumerable<Block> blocks) => blocks.Aggregate(BigInteger.Zero, (sum, block) => sum + (BigInteger.One << (4 * block.Difficulty)));
}
