using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlockChain_1.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;

        private static readonly Regex AddressPattern = new Regex(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

        public TransactionService(List<Block> chain)
        {
            _walletService = new WalletService(chain);
        }
        public Transaction CreateTransaction(Wallet sender, string to, decimal amount, string ticker = "BASE", decimal fee = 1)
        {
            var tx = new Transaction(sender.Address, to, amount, sender.PublicKey);

            tx.TokenTicker = ticker;
            tx.Fee = fee;
            tx.Type = TransactionType.Transfer;

            tx.Signature = sender.Sign(tx.GetDataToSign());

            var validation = ValidateTransaction(tx);

            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            return tx;
        }
        public (bool IsValid, string ErrorMessage) ValidateTransaction(Transaction tx)
        {
            if (!string.Equals(tx.From, "System", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsValidAddress(tx.From)) return (false, "Невалідний формат адреси відправника.");
                if (!IsValidAddress(tx.To)) return (false, "Невалідний формат адреси отримувача.");
            }

            if (tx.Amount < 0) return (false, "Сума транзакції не може бути від'ємною.");
            if (tx.Fee < 0) return (false, "Комісія не може бути від'ємною.");

            if (string.Equals(tx.From, "System", StringComparison.OrdinalIgnoreCase))
            {
                return (true, string.Empty);
            }

            var senderPortfolio = _walletService.GetPortfolio(tx.From);
            decimal senderBaseBalance = senderPortfolio.TryGetValue("BASE", out var baseBal) ? baseBal : 0m;

            if (tx.Type == TransactionType.ICO)
            {
                if (senderBaseBalance < tx.Fee)
                {
                    return (false, $"[Валідація відхилена] Недостатньо BASE для створення токена. Потрібно: {tx.Fee} BASE, є: {senderBaseBalance} BASE.");
                }

                if (_walletService.TokenExists(tx.TokenTicker))
                {
                    return (false, $"[Валідація відхилена] Токен з тікером '{tx.TokenTicker}' вже існує в історії блокчейну.");
                }

                if (tx.TotalSupply <= 0)
                {
                    return (false, "Емісія токена повинна бути більшою за 0.");
                }
            }
            else if (tx.Type == TransactionType.Transfer)
            {
                if (!_walletService.TokenExists(tx.TokenTicker))
                {
                    return (false, $"[Валідація відхилена] Токен '{tx.TokenTicker}' не існує (ICO не проводилось).");
                }

                if (string.Equals(tx.TokenTicker, "BASE", StringComparison.OrdinalIgnoreCase))
                {
                    if (senderBaseBalance < (tx.Amount + tx.Fee))
                    {
                        return (false, $"[Валідація відхилена] Недостатньо BASE для переказу та оплати комісії. Потрібно: {tx.Amount + tx.Fee}, є: {senderBaseBalance}");
                    }
                }
                else
                {
                    decimal senderTokenBalance = senderPortfolio.TryGetValue(tx.TokenTicker, out var tokBal) ? tokBal : 0m;
                    if (senderTokenBalance < tx.Amount)
                    {
                        return (false, $"[Валідація відхилена] Недостатньо токенів '{tx.TokenTicker}' для відправки. Спроба відправити: {tx.Amount}, є: {senderTokenBalance}");
                    }
                    if (senderBaseBalance < tx.Fee)
                    {
                        return (false, $"[Валідація відхилена] Недостатньо BASE для оплати комісії мережі ({tx.Fee} BASE). Поточний баланс BASE: {senderBaseBalance}");
                    }
                }
            }

            string derivedAddress;
            try
            {
                derivedAddress = WalletService.DeriveAddress(tx.SenderPublicKey);
            }
            catch (ArgumentException)
            {
                return (false, "Публічний ключ відправника відсутній або невалідний.");
            }

            if (!string.Equals(derivedAddress, tx.From, StringComparison.OrdinalIgnoreCase))
                return (false, "Адреса відправника не збігається з наданим публічним ключем.");

            bool signatureValid = _walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature);
            if (!signatureValid)
                return (false, "Невалідний цифровий підпис транзакції.");

            return (true, string.Empty);
        }
        public static bool IsValidAddress(string address)
        {
            return !string.IsNullOrEmpty(address) && AddressPattern.IsMatch(address);
        }
        public Transaction CreateToken(Wallet creator, string ticker, decimal totalSupply)
        {
            var tx = new Transaction(
                creator.Address,
                creator.Address,
                0,
                creator.PublicKey);

            tx.Type = TransactionType.ICO;
            tx.TokenTicker = ticker;
            tx.TotalSupply = totalSupply;
            tx.Fee = 100;

            tx.Signature = creator.Sign(tx.GetDataToSign());

            var validation = ValidateTransaction(tx);

            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            return tx;
        }
    }
}