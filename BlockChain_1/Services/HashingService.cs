using BlockChain_1.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChain_1.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            var transactionHashes = new StringBuilder();
            foreach (var tx in block.Transactions)
            {
                transactionHashes.Append(HashString(tx.ToHashString()));
            }

            var blockData = $"{block.Index}{block.TimeStamp:O}{transactionHashes}{block.PreviousHash}{block.Nonce}";
            return HashString(blockData);
        }

        private string HashString(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}