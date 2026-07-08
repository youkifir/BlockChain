using BlockChain_1.Models;
using System;
using System.Collections.Generic;

namespace BlockChain_1.Services
{
    public class BlockChainDisplayService
    {
        public void PrintChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine($"Index:          {block.Index}");
                Console.WriteLine($"TimeStamp:      {block.TimeStamp}");
                Console.WriteLine($"Previous Hash:  {block.PreviousHash}");
                Console.WriteLine($"Hash:           {block.Hash}");
                Console.WriteLine($"Nonce:          {block.Nonce}");
                Console.WriteLine($"Mining time:    {block.MiningDuration:F2}s");

                foreach (var tx in block.Transactions)
                    PrintTransaction(tx);

                Console.WriteLine(new string('-', 50));
            }
        }
        public void PrintTransaction(Transaction tx)
        {
            Console.WriteLine($"Id:           {tx.Id}");
            Console.WriteLine($"Type:         {tx.Type}");
            Console.WriteLine($"From:         {tx.From}");
            Console.WriteLine($"To:           {tx.To}");
            Console.WriteLine($"Currency:     {tx.TokenTicker}");
            Console.WriteLine($"Amount:       {tx.Amount}");

            if (tx.Type == TransactionType.ICO)
            {
                Console.WriteLine($"Emission:     {tx.TotalSupply}");
            }

            Console.WriteLine($"Fee:          {tx.Fee}");
            Console.WriteLine($"Time:         {tx.TimeStamp}");
            Console.WriteLine("---------------------------------------");
        }
        public void PrintChainValidity(bool isValid)
        {
            Console.WriteLine(isValid ? "Blockchain is valid." : "Blockchain is INVALID.");
        }
        public void PrintPortfolio(string name, Dictionary<string, decimal> portfolio)
        {
            Console.WriteLine();
            Console.WriteLine($"========== {name} ==========");

            foreach (var token in portfolio)
            {
                Console.WriteLine($"{token.Key,-20}{token.Value}");
            }

            Console.WriteLine();
        }
    }
}