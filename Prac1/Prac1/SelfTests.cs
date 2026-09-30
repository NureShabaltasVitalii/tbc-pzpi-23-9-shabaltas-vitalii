namespace Prac1;

public static class SelfTests
{
    public static void Run()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("Address is a public-key hash", AddressIsHash), ("ECDSA signature verifies", ValidSignature),
            ("Modified transaction fails signature", ModifiedTransactionFails), ("Merkle root works for an even count", MerkleEven),
            ("Merkle root duplicates an odd leaf", MerkleOdd), ("Genesis block is deterministic", Genesis),
            ("Mining creates a valid block", Mining), ("Miner receives reward", Reward),
            ("Invalid signature is rejected by mempool", RejectBadSignature), ("Insufficient balance is rejected", RejectOverspend),
            ("Incorrect nonce is rejected", RejectNonce), ("Pending double spend is rejected", RejectDoubleSpend),
            ("Confirmed transaction replay is rejected", RejectReplay), ("Changed historic amount invalidates chain", DetectChangedAmount),
            ("Broken previous hash invalidates chain", DetectBrokenLink), ("Chain survives save and load", Persistence),
        };
        var failed = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine($"PASS: {name}"); }
            catch (Exception error) { failed++; Console.WriteLine($"FAIL: {name}: {error.Message}"); }
        }
        Console.WriteLine($"Tests: {tests.Length - failed}/{tests.Length} passed.");
        if (failed > 0) Environment.ExitCode = 1;
    }

    private static (Blockchain Chain, Wallet Miner, Wallet Recipient) Funded()
    {
        var chain = new Blockchain(); var miner = Wallet.Create(); var recipient = Wallet.Create();
        chain.Mine(miner.Address); return (chain, miner, recipient);
    }
    private static void AddressIsHash() { var w = Wallet.Create(); Equal(Crypto.Sha256(w.PublicKey), w.Address); }
    private static void ValidSignature() { var w = Wallet.Create(); True(w.Sign(Wallet.Create().Address, 1, 0, 0).HasValidSignature()); }
    private static void ModifiedTransactionFails() { var w = Wallet.Create(); var tx = w.Sign(Wallet.Create().Address, 1, 0, 0); tx.Amount = 2; False(tx.HasValidSignature()); }
    private static void MerkleEven() { var tx = new[] { Wallet.Create().Sign("a", 1, 0, 0), Wallet.Create().Sign("b", 1, 0, 0) }; Equal(Crypto.Sha256(tx[0].Id + tx[1].Id), Block.CalculateMerkleRoot(tx)); }
    private static void MerkleOdd() { var tx = new[] { Wallet.Create().Sign("a", 1, 0, 0), Wallet.Create().Sign("b", 1, 0, 0), Wallet.Create().Sign("c", 1, 0, 0) }; var left = Crypto.Sha256(tx[0].Id + tx[1].Id); var right = Crypto.Sha256(tx[2].Id + tx[2].Id); Equal(Crypto.Sha256(left + right), Block.CalculateMerkleRoot(tx)); }
    private static void Genesis() { Equal(Blockchain.CreateGenesis().Hash, new Blockchain().Chain[0].Hash); }
    private static void Mining() { var x = Funded(); True(x.Chain.Validate(out _)); Equal(2, x.Chain.Chain.Count); }
    private static void Reward() { var x = Funded(); Equal(Blockchain.BlockReward, x.Chain.BalanceOf(x.Miner.Address)); }
    private static void RejectBadSignature() { var x = Funded(); var tx = x.Miner.Sign(x.Recipient.Address, 1, 0, 0); tx.Signature = "bad"; False(x.Chain.TryAddTransaction(tx, out _)); }
    private static void RejectOverspend() { var x = Funded(); False(x.Chain.TryAddTransaction(x.Miner.Sign(x.Recipient.Address, 11, 0, 0), out _)); }
    private static void RejectNonce() { var x = Funded(); False(x.Chain.TryAddTransaction(x.Miner.Sign(x.Recipient.Address, 1, 0, 1), out _)); }
    private static void RejectDoubleSpend() { var x = Funded(); True(x.Chain.TryAddTransaction(x.Miner.Sign(x.Recipient.Address, 7, 0, 0), out _)); False(x.Chain.TryAddTransaction(x.Miner.Sign(Wallet.Create().Address, 7, 0, 1), out _)); }
    private static void RejectReplay() { var x = Funded(); var tx = x.Miner.Sign(x.Recipient.Address, 1, 0, 0); True(x.Chain.TryAddTransaction(tx, out _)); x.Chain.Mine(x.Miner.Address); False(x.Chain.TryAddTransaction(tx, out _)); }
    private static void DetectChangedAmount() { var x = Funded(); x.Chain.Chain[1].Transactions[0].Amount++; False(x.Chain.Validate(out _)); }
    private static void DetectBrokenLink() { var x = Funded(); x.Chain.Mine(x.Miner.Address); x.Chain.Chain[2].PreviousHash = "attack"; False(x.Chain.Validate(out _)); }
    private static void Persistence() { var x = Funded(); var path = Path.GetTempFileName(); x.Chain.Save(path); var loaded = Blockchain.Load(path); File.Delete(path); True(loaded.Validate(out _)); Equal(x.Chain.Chain.Count, loaded.Chain.Count); }
    private static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    private static void False(bool value) { if (value) throw new InvalidOperationException("Expected false."); }
    private static void Equal<T>(T expected, T actual) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}."); }
}
