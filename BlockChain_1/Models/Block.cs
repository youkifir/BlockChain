using System;
using System.Collections.Generic;

namespace BlockChain_1.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }
        public List<Transaction> Transactions { get; set; }
        public string PreviousHash { get; set; }
        public int Nonce { get; set; }
        public double MiningDuration { get; set; }
        public string Hash { get; set; }
        public int Difficulty { get; set; } = 3;

        public Block(int index, DateTime timeStamp, List<Transaction> transactions, string previousHash)
        {
            Index = index;
            TimeStamp = timeStamp;
            Transactions = transactions;
            PreviousHash = previousHash;
            Hash = string.Empty;
            MiningDuration = 0;
        }
    }
}