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
        private WalletService _walletService { get; set; }
        private readonly FileStorageService _fileStorageService;

        public List<Block> Chain { get; set; }
        private readonly List<Transaction> _pendingTransactions = new List<Transaction>();

        public int Difficulty { get; private set; }
        public double TargetBlockTime { get; set; } = 10;
        public int AdjustmentInterval { get; set; } = 3;
        private decimal _rewardAmount { get; set; } = 50;
        private int maxTransactionsAmount { get; set; } = 10;
        private int _halvingInterval { get; set; } = 2;

        public string VanityPrefix { get; set; } = "cafe";

        // === Смарт-пакування блоків (Smart Chunking) ===
        // Максимальна "вага" одного блоку в байтах (сума Transaction.GetSizeInBytes()
        // усіх транзакцій, що в нього увійшли). Реальні адреси в стилі Ethereum (42
        // символи кожна) + GUID + ISO-таймстемп важать разом ~150-160 байт на
        // транзакцію, тож із лімітом 256 в один блок типово влізає 1 транзакція -
        // це очікувано і показує, що алгоритм справді рахує реальну вагу, а не
        // просто ділить список на шматки по N штук. Збільшіть значення, якщо
        // хочете бачити по кілька транзакцій в одному блоці.
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
            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock);
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

            _fileStorageService.SaveBlockchain(Chain);

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
                var senderBalance = _walletService.GetBalance(transaction.From);
                if (senderBalance < transaction.Amount + transaction.Fee)
                {
                    throw new InvalidOperationException($"Insufficient balance for transaction: {transaction.Id}");
                }
            }
            _pendingTransactions.Add(transaction);
        }

        /// <summary>
        /// Автоматично розбиває вхідний список транзакцій на кілька блоків,
        /// спираючись на реальну вагу кожної транзакції в байтах
        /// (Transaction.GetSizeInBytes(), Encoding.UTF8.GetByteCount під капотом).
        ///
        /// Як тільки додавання чергової транзакції переповнило б поточний блок
        /// (currentBatchSize + txSize > MaxBlockSizeBytes) - поточний пакет одразу
        /// майниться і додається в ланцюг, а нова транзакція йде вже в наступний
        /// пакет. Жодна валідна транзакція не губиться: цикл завжди доходить до
        /// кінця списку, а залишок після циклу майниться окремим "хвостовим" блоком.
        ///
        /// Транзакція, яка сама по собі важча за MaxBlockSizeBytes, ніколи б не
        /// влізла в жоден блок - вона відхиляється з попередженням в консоль,
        /// а обробка решти списку продовжується.
        /// </summary>
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

                // Додавання цієї транзакції переповнить поточний блок -
                // спочатку майнимо накопичене, тоді починаємо новий пакет.
                if (currentBatch.Count > 0 && currentBatchSize + txSize > MaxBlockSizeBytes)
                {
                    minedBlocksCount++;
                    Console.WriteLine($"[ProcessTransactions] Блок №{minedBlocksCount}: {currentBatch.Count} транз., {currentBatchSize} байт. Майнимо...");
                    MineTransactionBatch(currentBatch, minerAddress);

                    currentBatch = new List<Transaction>();
                    currentBatchSize = 0;
                }

                currentBatch.Add(tx);
                currentBatchSize += txSize;
            }

            // "Хвостовий" блок з тим, що лишилось після циклу.
            if (currentBatch.Count > 0)
            {
                minedBlocksCount++;
                Console.WriteLine($"[ProcessTransactions] Блок №{minedBlocksCount}: {currentBatch.Count} транз., {currentBatchSize} байт. Майнимо...");
                MineTransactionBatch(currentBatch, minerAddress);
            }

            Console.WriteLine($"[ProcessTransactions] Готово. Замайнено блоків: {minedBlocksCount}. Відхилено транзакцій: {rejectedCount}.");
        }

        /// <summary>
        /// Майнить конкретний, уже сформований пакет транзакцій окремим блоком,
        /// в обхід звичайного мемпулу (_pendingTransactions) та ліміту
        /// maxTransactionsAmount, щоб гарантовано не загубити жодну транзакцію з пакета.
        ///
        /// MineBlockAsync асинхронний (паралелить пошук nonce по потоках), а
        /// ProcessTransactions навмисно синхронний за сигнатурою завдання - тому
        /// тут використано GetAwaiter().GetResult(). У консольному застосунку без
        /// SynchronizationContext це не створює дедлоку.
        /// </summary>
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
                foreach (var tx in currentBlock.Transactions)
                {
                    if (tx.From == "COINBASE")
                    {
                        continue;
                    }

                    // Адреса відправника повинна відповідати публічному ключу,
                    // який транзакція несе в собі (SenderPublicKey), і саме
                    // цим ключем (а не адресою напряму) перевіряється підпис.
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


                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock)) return false;
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
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock) ||
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
            decimal reward = _rewardAmount / (decimal)Math.Pow(2, halvings);
            return reward > 0 ? reward : 0;
        }
    }
}