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

        /// <summary>
        /// Виводить "реалістичну" крипто-адресу у стилі Ethereum ("0x" + 40 hex символів,
        /// рівно 42 символи разом) з публічного ключа гаманця: беремо SHA-256 хеш
        /// публічного ключа і залишаємо останні 20 байтів.
        ///
        /// Це навмисно детермінований і безстанний розрахунок (без словника-реєстру
        /// адрес десь у пам'яті), оскільки в проєкті існує кілька незалежних
        /// екземплярів WalletService (Program.cs, TransactionService, BlockChainService),
        /// і зберігати між ними спільний mutable-стан було б крихко.
        /// Будь-який вузол може самостійно перерахувати адресу з публічного ключа,
        /// який транзакція вже несе в собі (Transaction.SenderPublicKey), і звірити
        /// її з заявленим From — це і є перевірка "адреса належить цьому ключу".
        /// </summary>
        public static string DeriveAddress(byte[] publicKey)
        {
            if (publicKey == null || publicKey.Length == 0)
                throw new ArgumentException("Public key cannot be empty.", nameof(publicKey));

            byte[] hash = SHA256.HashData(publicKey);
            byte[] addressBytes = hash[^20..];
            return "0x" + Convert.ToHexString(addressBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Перевіряє підпис за наданим публічним ключем (а не за адресою -
        /// адреса тепер лише хеш ключа і не розкодовується назад у ключ).
        /// </summary>
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