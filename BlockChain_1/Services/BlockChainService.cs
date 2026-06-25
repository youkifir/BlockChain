using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class BlockChainService
    {
        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;
        private readonly TransactionService _transactionService;

        public List<Block> Chain { get; set; }
        public int Difficulty { get; private set; }
        public double TargetBlockTime { get; set; } = 10;
        public int AdjustmentInterval { get; set; } = 3;
        private decimal _rewardAmount { get; set; } = 50;
        private int maxTransactionsAmount { get; set; } = 10;
        private List<Transaction> _pendingTransactions = new List<Transaction>();
        private WalletService wallet { get; set; }
        public BlockChainService(int initialDifficulty = 6)
        {
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService(Chain);
            wallet = new WalletService(Chain);
            Difficulty = initialDifficulty;

            CreateGenesisBlock();
        }

        private void CreateGenesisBlock()
        {
            Block genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0");
            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock);
            Chain.Add(genesisBlock);
        }

        public async Task AddBlockAsync(string minerAddress)
        {
            foreach (Transaction transaction in _pendingTransactions)
            {
                if (!_transactionService.ValidateTransaction(transaction).IsValid)
                {
                    throw new InvalidOperationException($"Invalid transaction: {transaction.Id}");
                }
            }

            var sortedTransactions = _pendingTransactions.OrderByDescending(t => t.Fee).Take(maxTransactionsAmount).ToList();
            var totalReward = sortedTransactions.Sum(t => t.Fee) + _rewardAmount;

            var rewardTransaction = new Transaction("COINBASE", minerAddress, totalReward, new byte[0]);
            sortedTransactions.Add(rewardTransaction);

            Block lastBlock = Chain.Last();
            Block newBlock = new Block(
                lastBlock.Index + 1,
                DateTime.UtcNow,
                sortedTransactions,
                lastBlock.Hash);

            _miningService.MineBlock(newBlock, Difficulty);

            Chain.Add(newBlock);

            _pendingTransactions.RemoveAll(t => sortedTransactions.Contains(t));

            if (newBlock.Index % AdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }
        }
        public void AddTransactionToMemPool(Transaction transaction)
        {
            var validation = _transactionService.ValidateTransaction(transaction);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }
            if (transaction.From != "COINBASE")
            {
                var senderBalance = wallet.GetBalance(transaction.From);
                if (senderBalance < transaction.Amount + transaction.Fee)
                {
                    throw new InvalidOperationException($"Insufficient balance for transaction: {transaction.Id}");
                }
            }
            _pendingTransactions.Add(transaction);
        }
        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in Chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.From == address)
                    {
                        balance -= transaction.Amount + transaction.Fee;
                    }
                    if (transaction.To == address)
                    {
                        balance += transaction.Amount;
                    }
                }
            }
            return balance;
        }

        public void AdjustDifficulty()
        {
            if ((Chain.Count - 1) % AdjustmentInterval != 0 || Chain.Count <= 1)
            {
                return;
            }

            var recentBlocks = Chain.Skip(Chain.Count - AdjustmentInterval).ToList();
            double avgTime = recentBlocks.Average(b => b.MiningDuration);

            if (avgTime < TargetBlockTime)
            {
                Difficulty++;
            }
            else if (avgTime > TargetBlockTime)
            {
                Difficulty = Math.Max(1, Difficulty - 1);
            }
        }

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];

                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock)) return false;
                if (currentBlock.PreviousHash != previousBlock.Hash) return false;

                if (currentBlock.MiningDuration < 0) return false;
                if (currentBlock.TimeStamp <= previousBlock.TimeStamp) return false;

                double physicalTimeDiff = (currentBlock.TimeStamp - previousBlock.TimeStamp).TotalSeconds;
                double maxAllowedDuration = physicalTimeDiff + 2.0;

                if (currentBlock.MiningDuration > maxAllowedDuration) return false;
            }
            return true;
        }

        public Block FindBlockByHash(string targetHash)
        {
            return Chain.FirstOrDefault(b => b.Hash.Equals(targetHash, StringComparison.OrdinalIgnoreCase));
        }

        public void RecalculateHashForBlock(Block block)
        {
            block.Hash = _hashingService.ComputeHash(block);
        }
    }
}