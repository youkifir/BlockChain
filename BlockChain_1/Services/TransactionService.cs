using BlockChain_1.Models;
using System;
using System.Collections.Generic;

namespace BlockChain_1.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;

        public TransactionService(List<Block> chain)
        {
            _walletService = new WalletService(chain);
        }

        public Transaction CreateTransaction(Wallet sender, string to, decimal amount)
        {
            var balance = _walletService.GetBalance(sender.Address);
            if (balance < amount)
                throw new ArgumentException("Insufficient balance.");

            var tx = new Transaction(sender.Address, to, amount, sender.PublicKey);
            tx.Signature = sender.Sign(tx.GetDataToSign());

            var (isValid, error) = ValidateTransaction(tx);
            if (!isValid)
                throw new ArgumentException(error);

            return tx;
        }

        public (bool IsValid, string ErrorMessage) ValidateTransaction(Transaction tx)
        {
            if (tx == null)
                return (false, "Transaction cannot be null.");
            if (string.IsNullOrEmpty(tx.From))
                return (false, "Sender cannot be empty.");
            if (string.IsNullOrEmpty(tx.To))
                return (false, "Recipient cannot be empty.");
            if (tx.Amount <= 0)
                return (false, "Amount must be greater than zero.");
            if (tx.From == "COINBASE")
                return (true, string.Empty);

            bool signatureValid = _walletService.VerifySignature(tx.From, tx.GetDataToSign(), tx.Signature);
            if (!signatureValid)
                return (false, "Invalid transaction signature.");

            return (true, string.Empty);
        }
    }
}