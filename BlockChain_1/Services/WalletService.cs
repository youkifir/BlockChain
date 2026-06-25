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
            string address = Convert.ToBase64String(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }

        public bool VerifySignature(string publicKeyBase64, byte[] data, byte[] signature)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }

        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in _chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From == address) balance -= tx.Amount + tx.Fee;
                    if (tx.To == address) balance += tx.Amount;
                }
            }
            return balance;
        }
    }
}