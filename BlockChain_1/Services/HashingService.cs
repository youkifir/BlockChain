using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            var totalHash = "";
            foreach(var item in block.Transactions)
            {
                totalHash += ComputeHash(item.ToRowString());
            }
            var blockData = $"{block.Index}{block.TimeStamp.ToString("O")}{totalHash}{block.PreviousHash}{block.Nonce}";
            return ComputeHash(blockData);
        }
        private string ComputeHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            return Convert.ToHexString(hashBytes);
        }
    }
}
