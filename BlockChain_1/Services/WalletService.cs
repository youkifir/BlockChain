using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace BlockChain_1.Services
{
    public class WalletService
    {
        private readonly List<Block> _chain;

        public WalletService(List<Block> chain)
        {
            _chain = chain;
        }

        public Wallet CreateWallet(string name)
        {
            using var ecdsa = ECDsa.Create();
            byte[] privateKey = ecdsa.ExportECPrivateKey();
            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            string address = DeriveAddress(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }
        public static string DeriveAddress(byte[] publicKey)
        {
            if (publicKey == null || publicKey.Length == 0)
                throw new ArgumentException("Public key cannot be empty.", nameof(publicKey));

            byte[] hash = SHA256.HashData(publicKey);
            byte[] addressBytes = hash[^20..];
            return "0x" + Convert.ToHexString(addressBytes).ToLowerInvariant();
        }
        public bool VerifySignature(byte[] publicKey, byte[] data, byte[] signature)
        {
            if (publicKey == null || publicKey.Length == 0 || signature == null || signature.Length == 0)
                return false;

            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
        public decimal GetBalance(string address)
        {
            return GetTokenBalance(address, "BASE");
        }
        public Dictionary<string, decimal> GetPortfolio(string address)
        {
            var portfolio = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "BASE", 0m }
            };

            foreach (var block in _chain)
            {
                foreach (var tx in block.Transactions)
                {
                    string ticker = string.IsNullOrWhiteSpace(tx.TokenTicker) ? "BASE" : tx.TokenTicker;

                    if (tx.From == "System")
                    {
                        if (tx.To.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            portfolio["BASE"] += tx.Amount;
                        }
                        continue;
                    }

                    if (tx.Type == TransactionType.ICO)
                    {
                        if (tx.From.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!portfolio.ContainsKey(ticker)) portfolio[ticker] = 0m;
                            portfolio[ticker] += tx.TotalSupply;

                            portfolio["BASE"] -= tx.Fee;
                        }

                        var blockMiner = block.Transactions.FirstOrDefault(t => t.From == "System")?.To;
                        if (blockMiner != null && blockMiner.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            portfolio["BASE"] += tx.Fee;
                        }
                        continue;
                    }

                    if (tx.Type == TransactionType.Transfer)
                    {
                        if (tx.From.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!portfolio.ContainsKey(ticker)) portfolio[ticker] = 0m;
                            portfolio[ticker] -= tx.Amount;

                            portfolio["BASE"] -= tx.Fee;
                        }

                        if (tx.To.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!portfolio.ContainsKey(ticker)) portfolio[ticker] = 0m;
                            portfolio[ticker] += tx.Amount;
                        }

                        var blockMiner = block.Transactions.FirstOrDefault(t => t.From == "System")?.To;
                        if (blockMiner != null && blockMiner.Equals(address, StringComparison.OrdinalIgnoreCase))
                        {
                            portfolio["BASE"] += tx.Fee;
                        }
                    }
                }
            }

            return portfolio;
        }
        public decimal GetTokenBalance(string address, string ticker)
        {
            var portfolio = GetPortfolio(address);

            return portfolio.TryGetValue(ticker, out var balance)
                ? balance
                : 0;
        }
        public bool TokenExists(string ticker)
        {
            foreach (var block in _chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.Type == TransactionType.ICO &&
                        tx.TokenTicker.Equals(ticker, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return ticker.Equals("BASE", StringComparison.OrdinalIgnoreCase);
        }

    }
}