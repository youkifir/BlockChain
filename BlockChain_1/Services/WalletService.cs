using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class WalletService
    {
        private List<Block> blockChain;
        public WalletService(List<Block> blockChain)
        {
            this.blockChain = blockChain;
        }
        public Wallet CreateWallet(string name)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            byte[] privateKey = ecdsa.ExportECPrivateKey();
            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();

            string address = Convert.ToBase64String(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }
        public bool VerifySignature(string publicKey, byte[] data, byte[] signature)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            return ecdsa.VerifyData(data, signature, System.Security.Cryptography.HashAlgorithmName.SHA256);
        }
        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in blockChain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.From == address)
                    {
                        balance -= transaction.Amount + transaction.Fee;
                    }
                    if (transaction.To == address)
                    {
                        balance += transaction.Amount;
                    }
                }
            }
            return balance;
        }
}
}
