using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;

        public TransactionService(List<Block> blockChain)
        {
            _walletService = new WalletService(blockChain);
        }
        public Transaction CreateTransaction(Wallet walletFrom, string to, decimal amount, byte[] sendePublicKey)
        {
            var balance = _walletService.GetBalance(walletFrom.Address);
            if (balance < amount)
            {
                throw new ArgumentException("Insufficient balance.");
            }
            var tx = new Transaction(walletFrom.Address, to, amount, sendePublicKey);
            tx.Signature = walletFrom.Sign(tx.GetDataToSign());

            var validation = ValidateTransaction(tx);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }
            return tx;
        }

        public (bool IsValid, string ErrorMessage) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null)
            {
                return (false, "Transaction can not be null.");
            }
            if (string.IsNullOrEmpty(transaction.From))
            {
                return (false, "Sender cannot be empty");
            }
            if (string.IsNullOrEmpty(transaction.To))
            {
                return (false, "Recipient cannot be empty");
            }
            if (transaction.Amount <= 0)
            {
                return (false, "Amount must be greater than zero");
            }
            if(transaction.From == "COINBASE")
            {
                return(true, "");
            }

            bool isSignatureValid = _walletService.VerifySignature(transaction.From, transaction.GetDataToSign(), transaction.Signature);
            if (!isSignatureValid)
            {
                return (false, "Invalid transaction signature.");
            }
            return (true, string.Empty);
        }
    }
}
