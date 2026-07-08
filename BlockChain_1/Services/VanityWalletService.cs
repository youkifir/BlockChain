using BlockChain_1.Models;
using System;
using System.Security.Cryptography;

namespace BlockChain_1.Services
{
    public class VanityWalletService
    {
        private readonly WalletService _walletService;

        public VanityWalletService(WalletService walletService)
        {
            _walletService = walletService;
        }

        public (Wallet wallet, int attempts) MineWallet(string desiredPrefix)
        {
            if (string.IsNullOrWhiteSpace(desiredPrefix))
                throw new ArgumentException("Префікс не може бути порожнім.", nameof(desiredPrefix));

            string prefix = desiredPrefix.ToLowerInvariant();
            int attempts = 0;

            Console.WriteLine($"[Майнінг адреси] Шукаємо адресу, що починається з: 0x{prefix}...");

            while (true)
            {
                attempts++;

                using var ecdsa = ECDsa.Create();
                byte[] privateKey = ecdsa.ExportECPrivateKey();
                byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();

                string address = WalletService.DeriveAddress(publicKey);

                if (address.Substring(2).StartsWith(prefix))
                {
                    var wallet = new Wallet($"Vanity-{prefix}", address, publicKey, privateKey);
                    return (wallet, attempts);
                }
            }
        }
    }
}