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
        //Services
        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;
        private readonly TransactionService _transactionService;
        private WalletService _walletService { get; set; }
        private readonly FileStorageService _fileStorageService;

        //Blockchain properties
        public List<Block> Chain { get; set; }
        private readonly List<Transaction> _pendingTransactions = new List<Transaction>();

        //Configuration properties
        public int Difficulty { get; private set; }
        public double TargetBlockTime { get; set; } = 10;
        public int AdjustmentInterval { get; set; } = 3;
        private decimal _rewardAmount { get; set; } = 50;
        private int maxTransactionsAmount { get; set; } = 10;
        private int _halvingInterval { get; set; } = 2;
        private const decimal ICO_FEE = 100m;

        //Vanity prefix for block hashes
        public string VanityPrefix { get; set; } = "cafe";
        public int MaxBlockSizeBytes { get; } = 256;


        public BlockChainService(int initialDifficulty = 6)
        {
            Chain = new List<Block>();

            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService(Chain);
            _walletService = new WalletService(Chain);
            Difficulty = initialDifficulty;
            _fileStorageService = new FileStorageService();

            var loadedChain = _fileStorageService.LoadBlockchain();

            if (loadedChain != null && loadedChain.Count > 0)
            {
                Chain = loadedChain;
                _transactionService = new TransactionService(Chain);
                _walletService = new WalletService(Chain);
            }
            else
            {
                CreateGenesisBlock();
                _fileStorageService.SaveBlockchain(Chain);
            }


        }
        private void CreateGenesisBlock()
        {
            Block genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0");
            string merkleRoot = _hashingService.GetMerkleTree(genesisBlock.Transactions);
            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock, merkleRoot);
            Chain.Add(genesisBlock);
        }
        public async Task MineBlock(string minerAddress)
        {
            foreach (Transaction transaction in _pendingTransactions)
            {
                if (!_transactionService.ValidateTransaction(transaction).IsValid)
                {
                    throw new InvalidOperationException($"Invalid transaction: {transaction.Id}");
                }
            }

            var sortedTransactions = _pendingTransactions.OrderByDescending(t => t.Fee).Take(maxTransactionsAmount).ToList();
            var totalReward = sortedTransactions.Sum(t => t.Fee) + GetMinerReward();

            var rewardTransaction = new Transaction("COINBASE", minerAddress, totalReward, new byte[0]);
            sortedTransactions.Add(rewardTransaction);

            Block lastBlock = Chain.Last();
            Block newBlock = new Block(
                lastBlock.Index + 1,
                DateTime.UtcNow,
                sortedTransactions,
                lastBlock.Hash);

            await _miningService.MineBlockAsync(newBlock, VanityPrefix);

            Chain.Add(newBlock);

            foreach (var tx in newBlock.Transactions)
            {
                if (tx.Type == TransactionType.ICO)
                {
                    Console.WriteLine(
                        $"ICO created: {tx.TokenTicker} ({tx.TotalSupply})");
                }
            }

            _fileStorageService.SaveBlockchain(Chain);

            _pendingTransactions.RemoveAll(t => sortedTransactions.Contains(t));

            foreach (var tx in sortedTransactions)
            {
                if (tx.Type == TransactionType.ICO)
                {
                    Console.WriteLine(
                        $"{tx.TokenTicker} successfully issued.");
                }
            }

            if (newBlock.Index % AdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }
        }
        public void AddTransactionToMemPool(Transaction transaction)
        {
            var validation = _transactionService.ValidateTransaction(transaction);

            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            if (transaction.From != "COINBASE")
            {
                if (transaction.Type == TransactionType.Transfer)
                {
                    decimal tokenBalance =
                        _walletService.GetTokenBalance(transaction.From, transaction.TokenTicker);

                    decimal baseBalance =
                        _walletService.GetBalance(transaction.From);

                    if (tokenBalance < transaction.Amount)
                        throw new InvalidOperationException("Not enough token balance.");

                    if (baseBalance < transaction.Fee)
                        throw new InvalidOperationException("Not enough BASE for fee.");
                }

                if (transaction.Type == TransactionType.ICO)
                {
                    if (_walletService.GetBalance(transaction.From) < ICO_FEE)
                        throw new InvalidOperationException("Not enough BASE for ICO.");
                }
            }

            _pendingTransactions.Add(transaction);
        }
        public void ProcessTransactions(List<Transaction> incomingTransactions, string minerAddress = null)
        {
            if (incomingTransactions == null || incomingTransactions.Count == 0)
            {
                Console.WriteLine("[ProcessTransactions] Немає транзакцій для обробки.");
                return;
            }

            var currentBatch = new List<Transaction>();
            int currentBatchSize = 0;
            int minedBlocksCount = 0;
            int rejectedCount = 0;

            foreach (var tx in incomingTransactions)
            {
                var validation = _transactionService.ValidateTransaction(tx);
                if (!validation.IsValid)
                {
                    rejectedCount++;
                    Console.WriteLine($"[ProcessTransactions] Транзакцію {tx?.Id} відхилено: {validation.ErrorMessage}");
                    continue;
                }

                int txSize = tx.GetSizeInBytes();

                if (txSize > MaxBlockSizeBytes)
                {
                    rejectedCount++;
                    Console.WriteLine($"[ProcessTransactions] Транзакція {tx.Id} важить {txSize} байт - більше за ліміт блоку ({MaxBlockSizeBytes} байт). Відхилено.");
                    continue;
                }

                if (currentBatch.Count > 0 && currentBatchSize + txSize > MaxBlockSizeBytes)
                {
                    minedBlocksCount++;
                    Console.WriteLine($"[ProcessTransactions] Блок №{minedBlocksCount}: {currentBatch.Count} транз., {currentBatchSize} байт. Майнимо...");
                    MineTransactionBatch(currentBatch, minerAddress);

                    currentBatch = new List<Transaction>();
                    currentBatchSize = 0;
                }

                if (tx.Type == TransactionType.ICO)
                {
                    Console.WriteLine(
                        $"ICO transaction: {tx.TokenTicker}");
                }

                currentBatch.Add(tx);
                currentBatchSize += txSize;
            }

            if (currentBatch.Count > 0)
            {
                minedBlocksCount++;
                Console.WriteLine($"[ProcessTransactions] Блок №{minedBlocksCount}: {currentBatch.Count} транз., {currentBatchSize} байт. Майнимо...");
                MineTransactionBatch(currentBatch, minerAddress);
            }

            Console.WriteLine($"[ProcessTransactions] Готово. Замайнено блоків: {minedBlocksCount}. Відхилено транзакцій: {rejectedCount}.");
        }
        private void MineTransactionBatch(List<Transaction> batch, string minerAddress)
        {
            var transactionsToMine = new List<Transaction>(batch);

            if (!string.IsNullOrEmpty(minerAddress))
            {
                var rewardTransaction = new Transaction("COINBASE", minerAddress, GetMinerReward(), new byte[0]);
                transactionsToMine.Add(rewardTransaction);
            }

            Block lastBlock = Chain.Last();
            Block newBlock = new Block(
                lastBlock.Index + 1,
                DateTime.UtcNow,
                transactionsToMine,
                lastBlock.Hash);

            _miningService.MineBlockAsync(newBlock, VanityPrefix).GetAwaiter().GetResult();

            Chain.Add(newBlock);
            _fileStorageService.SaveBlockchain(Chain);

            if (newBlock.Index % AdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }
        }
        public decimal GetBalance(string address)
        {
            return _walletService.GetBalance(address);
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
                foreach (var tx in currentBlock.Transactions)
                {
                    if (tx.From == "COINBASE")
                    {
                        continue;
                    }

                    string derivedAddress;
                    try
                    {
                        derivedAddress = WalletService.DeriveAddress(tx.SenderPublicKey);
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine($"[CRITICAL ERROR] Missing/invalid sender public key in block {currentBlock.Index} | transaction: {tx.Id}");
                        return false;
                    }

                    if (!string.Equals(derivedAddress, tx.From, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[CRITICAL ERROR] Sender address does not match public key in block {currentBlock.Index} | transaction: {tx.Id}");
                        return false;
                    }

                    bool isSignatureValid = _walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature);

                    if (!isSignatureValid)
                    {
                        Console.WriteLine($"[CRITICAL ERROR] Invalid signature in block {currentBlock.Index} | transaction: {tx.Id}");
                        return false;
                    }
                }


                string merkleRoot = _hashingService.GetMerkleTree(currentBlock.Transactions);
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock, merkleRoot)) return false;
                if (currentBlock.PreviousHash != previousBlock.Hash) return false;

                if (!currentBlock.Hash.StartsWith(VanityPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[CRITICAL ERROR] Block {currentBlock.Index} hash \"{currentBlock.Hash}\" does not start with vanity prefix \"{VanityPrefix}\"");
                    return false;
                }

                if (currentBlock.MiningDuration < 0) return false;
                if (currentBlock.TimeStamp <= previousBlock.TimeStamp) return false;

                double physicalTimeDiff = (currentBlock.TimeStamp - previousBlock.TimeStamp).TotalSeconds;
                double maxAllowedDuration = physicalTimeDiff + 2.0;

                if (currentBlock.MiningDuration > maxAllowedDuration) return false;
            }
            return true;
        }
        public int GetInvalidBlockIndex()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];
                string merkleRoot = _hashingService.GetMerkleTree(currentBlock.Transactions);
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock, merkleRoot) ||
                    currentBlock.PreviousHash != previousBlock.Hash ||
                    !currentBlock.Hash.StartsWith(VanityPrefix, StringComparison.OrdinalIgnoreCase) ||
                    currentBlock.MiningDuration < 0 ||
                    currentBlock.TimeStamp <= previousBlock.TimeStamp)
                {
                    return i;
                }
            }
            return -1;
        }
        public Block FindBlockByHash(string targetHash)
        {
            return Chain.FirstOrDefault(b => b.Hash.Equals(targetHash, StringComparison.OrdinalIgnoreCase));
        }
        private decimal GetMinerReward()
        {
            int halvings = Chain.Count / _halvingInterval;

            double rewardDouble = (double)_rewardAmount / Math.Pow(2, halvings);

            if (double.IsNaN(rewardDouble) || double.IsInfinity(rewardDouble) || rewardDouble <= 0)
            {
                return 0;
            }

            decimal reward = (decimal)rewardDouble;
            return reward > 0 ? reward : 0;
        }
        public double getChainWeight(List<Block> Chain)
        {
            double weight = 0;
            foreach (var block in Chain)
            {
                weight += Math.Pow(2, block.Difficulty);
            }
            return weight;
        }
        public bool IsChainValid(List<Block> externalChain)
        {
            for (int i = 1; i < externalChain.Count; i++)
            {
                var currentBlock = externalChain[i];
                var previousBlock = externalChain[i - 1];
                string merkleRoot = _hashingService.GetMerkleTree(currentBlock.Transactions);
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock, merkleRoot) ||
                    currentBlock.PreviousHash != previousBlock.Hash ||
                    !currentBlock.Hash.StartsWith(VanityPrefix, StringComparison.OrdinalIgnoreCase) ||
                    currentBlock.MiningDuration < 0 ||
                    currentBlock.TimeStamp <= previousBlock.TimeStamp)
                {
                    return false;
                }
            }
            double currentChainWeight = getChainWeight(Chain);
            double externalChainWeight = getChainWeight(externalChain);
            return externalChainWeight > currentChainWeight;
        }
        public bool ResolveConflicts(List<Block> externalChain)
        {
            if (IsChainValid(externalChain))
            {
                var currentTotalWork = getChainWeight(Chain);
                var externalTotalWork = getChainWeight(externalChain);
                if(externalTotalWork > currentTotalWork)
                {
                    return false;
                }
                Chain = externalChain;
                _fileStorageService.SaveBlockchain(Chain);
                return true;
            }
            return false;
        }
        public Dictionary<string, decimal> GetPortfolio(string address)
        {
            return _walletService.GetPortfolio(address);
        }
        public void PrintPortfolio(string address)
        {
            var portfolio = GetPortfolio(address);

            Console.WriteLine(address);

            foreach (var token in portfolio)
            {
                Console.WriteLine($"{token.Key} : {token.Value}");
            }
        }
        public async Task ProcessBlockMiningAsync(Block block)
        {
            string merkleRoot = _hashingService.GetMerkleTree(block.Transactions);

            block.Difficulty = this.Difficulty;

            Console.WriteLine($"[Майнінг] Початок підбору Nonce для блоку #{block.Index} з префіксом '{this.VanityPrefix}'...");

            bool success = await _miningService.MineBlockAsync(block, this.VanityPrefix);

            if (!success)
            {
                throw new InvalidOperationException($"Не вдалося замайнити блок #{block.Index}");
            }
        }
    }
}