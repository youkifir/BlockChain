using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlockChain_1.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;

        // Крипто-адреса: рівно "0x" + 40 HEX-символів (0-9, a-f/A-F) = 42 символи.
        // Жодних спецсимволів, пробілів чи переносів рядків.
        private static readonly Regex AddressPattern = new Regex(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

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

            // "COINBASE" - системний відправник нагороди за майнінг,
            // формат крипто-адреси на нього не поширюється.
            if (tx.From != "COINBASE" && !IsValidAddress(tx.From))
                return (false, $"Invalid sender address \"{tx.From}\": must start with \"0x\" and be exactly 42 characters (0x + 40 hex chars).");

            if (!IsValidAddress(tx.To))
                return (false, $"Invalid recipient address \"{tx.To}\": must start with \"0x\" and be exactly 42 characters (0x + 40 hex chars).");

            if (tx.Amount <= 0)
                return (false, "Amount must be greater than zero.");

            if (tx.From == "COINBASE")
                return (true, string.Empty);

            // Адреса відправника повинна відповідати наданому публічному ключу -
            // інакше хтось міг би підписати транзакцію своїм ключем, але видати
            // себе за власника чужої адреси в полі From.
            string derivedAddress;
            try
            {
                derivedAddress = WalletService.DeriveAddress(tx.SenderPublicKey);
            }
            catch (ArgumentException)
            {
                return (false, "Sender public key is missing or invalid.");
            }

            if (!string.Equals(derivedAddress, tx.From, StringComparison.OrdinalIgnoreCase))
                return (false, "Sender address does not match the provided public key.");

            bool signatureValid = _walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature);
            if (!signatureValid)
                return (false, "Invalid transaction signature.");

            return (true, string.Empty);
        }

        /// <summary>
        /// Перевіряє формат крипто-адреси: "0x" + рівно 40 hex-символів (42 символи разом).
        /// </summary>
        public static bool IsValidAddress(string address)
        {
            return !string.IsNullOrEmpty(address) && AddressPattern.IsMatch(address);
        }
    }
}