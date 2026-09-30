using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Prac1;

public static class Crypto
{
    public static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}

public sealed class Wallet
{
    private readonly ECDsa _key;
    public string Address { get; }
    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());
    public string PrivateKey => Convert.ToBase64String(_key.ExportPkcs8PrivateKey());

    private Wallet(ECDsa key) { _key = key; Address = Crypto.Sha256(PublicKey); }
    public static Wallet Create() => new(ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10")));
    public static Wallet Import(string privateKey)
    {
        var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        return new Wallet(key);
    }

    public Transaction Sign(string recipient, decimal amount, decimal fee, long nonce)
    {
        var tx = new Transaction { Sender = Address, Recipient = recipient, Amount = amount, Fee = fee, Nonce = nonce, PublicKey = PublicKey };
        tx.Signature = Convert.ToBase64String(_key.SignData(Crypto.Bytes(tx.SigningPayload()), HashAlgorithmName.SHA256));
        return tx;
    }
}

public sealed class Transaction
{
    public const string Coinbase = "COINBASE";
    public string Sender { get; set; } = "";
    public string Recipient { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public long Nonce { get; set; }
    public string PublicKey { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Id => Crypto.Sha256(SigningPayload() + "|" + Signature);
    public bool IsCoinbase => Sender == Coinbase;

    // Fixed field order and invariant formatting make signed bytes identical on every node.
    public string SigningPayload() => string.Join("|", Sender, Recipient, Amount.ToString("0.########", CultureInfo.InvariantCulture), Fee.ToString("0.########", CultureInfo.InvariantCulture), Nonce.ToString(CultureInfo.InvariantCulture), PublicKey);

    public bool HasValidSignature()
    {
        if (IsCoinbase) return string.IsNullOrEmpty(PublicKey) && string.IsNullOrEmpty(Signature);
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey), out _);
            return Crypto.Sha256(PublicKey) == Sender && key.VerifyData(Crypto.Bytes(SigningPayload()), Convert.FromBase64String(Signature), HashAlgorithmName.SHA256);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }

    public static Transaction Reward(string miner, decimal amount, long height) => new() { Sender = Coinbase, Recipient = miner, Amount = amount, Fee = 0, Nonce = height };
}

public sealed class Block
{
    public long Index { get; set; }
    public long TimestampUnixMs { get; set; }
    public string PreviousHash { get; set; } = "";
    public string MerkleRoot { get; set; } = "";
    public int Difficulty { get; set; }
    public long Nonce { get; set; }
    public string Hash { get; set; } = "";
    public List<Transaction> Transactions { get; set; } = [];

    public string HeaderPayload() => string.Join("|", Index.ToString(CultureInfo.InvariantCulture), TimestampUnixMs.ToString(CultureInfo.InvariantCulture), PreviousHash, MerkleRoot, Difficulty.ToString(CultureInfo.InvariantCulture), Nonce.ToString(CultureInfo.InvariantCulture));
    public string CalculateHash() => Crypto.Sha256(HeaderPayload());

    public static string CalculateMerkleRoot(IEnumerable<Transaction> transactions)
    {
        var level = transactions.Select(t => t.Id).ToList();
        if (level.Count == 0) return Crypto.Sha256("");
        while (level.Count > 1)
        {
            if (level.Count % 2 == 1) level.Add(level[^1]);
            var next = new List<string>();
            for (var i = 0; i < level.Count; i += 2) next.Add(Crypto.Sha256(level[i] + level[i + 1]));
            level = next;
        }
        return level[0];
    }

    public bool MeetsProofOfWork() => Hash.StartsWith(new string('0', Difficulty), StringComparison.Ordinal);
}
