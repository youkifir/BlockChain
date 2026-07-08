using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public static class Tests
    {
        public static async Task TestGetInvalidBlockIndex(BlockChainService blockChain, TransactionService transactionService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== Тест: пошук пошкодженого блоку ===");

            const int targetChainLength = 5;
            while (blockChain.Chain.Count < targetChainLength)
            {
                try
                {
                    var tx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 5m);
                    blockChain.AddTransactionToMemPool(tx);
                }
                catch
                {
                }

                await blockChain.MineBlock(walletAlice.Address);
                Console.WriteLine($"Замайнено блок №{blockChain.Chain.Count - 1}. Всього блоків: {blockChain.Chain.Count}");
            }

            int checkBeforeTamper = blockChain.GetInvalidBlockIndex();
            Console.WriteLine(checkBeforeTamper == -1
                ? "До підробки: ланцюг цілісний, порушень не знайдено."
                : $"До підробки: несподівано знайдено пошкодження в блоці {checkBeforeTamper}.");

            const int tamperedBlockIndex = 2;
            if (blockChain.Chain.Count > tamperedBlockIndex && blockChain.Chain[tamperedBlockIndex].Transactions.Count > 0)
            {
                blockChain.Chain[tamperedBlockIndex].Transactions[0].Amount = 999999;
                Console.WriteLine($"Дані блоку №{tamperedBlockIndex} навмисно підроблено.");
            }
            else
            {
                Console.WriteLine($"У блоці №{tamperedBlockIndex} немає транзакцій для підробки.");
                return;
            }

            int invalidIndex = blockChain.GetInvalidBlockIndex();

            if (invalidIndex == -1)
            {
                Console.WriteLine("Ланцюг цілісний, порушень не знайдено.");
            }
            else
            {
                Console.WriteLine($"Увага! Знайдено порушення цілісності. Підроблений блок під номером: {invalidIndex}.");
            }
        }
        public static async Task TestVanityMining(BlockChainService blockChain, TransactionService transactionService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== Тест: Vanity Mining ===");

            blockChain.VanityPrefix = "cafe";
            Console.WriteLine($"Vanity-префікс: \"{blockChain.VanityPrefix}\"");

            const int blocksToMine = 4;
            int startCount = blockChain.Chain.Count;

            while (blockChain.Chain.Count < startCount + blocksToMine)
            {
                try
                {
                    var tx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 1m);
                    blockChain.AddTransactionToMemPool(tx);
                }
                catch
                {
                }

                await blockChain.MineBlock(walletAlice.Address);

                var minedBlock = blockChain.Chain[blockChain.Chain.Count - 1];
                Console.WriteLine($"Блок №{minedBlock.Index} замайнено. Hash: {minedBlock.Hash} | Nonce: {minedBlock.Nonce} | Час: {minedBlock.MiningDuration:F3} с");
            }

            bool isValid = blockChain.IsValid();
            Console.WriteLine(isValid
                ? "Ланцюг валідний: всі блоки містять vanity-префікс і зв'язки коректні."
                : "Ланцюг НЕВАЛІДНИЙ.");
        }
        public static async Task TestSmartChunking(BlockChainService blockChain, TransactionService transactionService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== Тест: Смарт-пакування блоків ===");
            Console.WriteLine($"MaxBlockSizeBytes = {blockChain.MaxBlockSizeBytes} байт");

            // Даємо Алісі баланс для 15 транзакцій.
            const int maxFundingAttempts = 20;
            int fundingAttempts = 0;
            while (blockChain.GetBalance(walletAlice.Address) < 5m && fundingAttempts < maxFundingAttempts)
            {
                await blockChain.MineBlock(walletAlice.Address);
                fundingAttempts++;
                Console.WriteLine($"Намайнено блок для поповнення балансу Аліси. Баланс: {blockChain.GetBalance(walletAlice.Address)}");
            }

            if (blockChain.GetBalance(walletAlice.Address) < 5m)
            {
                Console.WriteLine(
                    "Не вдалось поповнити баланс Аліси - винагорода за майнінг уже занулилась " +
                    "(halving-схема виснажилась, бо ланцюг занадто довгий). " +
                    "Видаліть blockchain.json/wallets.json поруч з .exe і запустіть заново.");
                return;
            }

            // 1. Генеруємо 15 коректних транзакцій (валідні 0x-адреси, підпис, все як слід).
            var transactions = new List<Transaction>();
            for (int i = 0; i < 15; i++)
            {
                try
                {
                    var tx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 0.1m);
                    transactions.Add(tx);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не вдалось створити транзакцію #{i}: {ex.Message}");
                }
            }

            Console.WriteLine($"Згенеровано {transactions.Count} валідних транзакцій. Кожна важить ~{(transactions.Count > 0 ? transactions[0].GetSizeInBytes() : 0)} байт.");
            Console.WriteLine("Передаємо на автоматичне пакування (ProcessTransactions)...\n");

            // 2. Автоматичне пакування і майнінг кількох блоків підряд.
            blockChain.ProcessTransactions(transactions, walletAlice.Address);

            Console.WriteLine($"\nВсього блоків у ланцюгу зараз: {blockChain.Chain.Count}");
            Console.WriteLine(blockChain.IsValid() ? "Ланцюг валідний." : "Ланцюг НЕВАЛІДНИЙ.");

            // 3. Демонстрація відхилення транзакції на невалідну адресу "Bob".
            Console.WriteLine("\n--- Спроба створити транзакцію на невалідну адресу \"Bob\" ---");
            try
            {
                transactionService.CreateTransaction(walletAlice, "Bob", 1m);
                Console.WriteLine("ПОМИЛКА: транзакція НЕ мала пройти валідацію, але пройшла!");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Транзакцію відхилено, як і очікувалось: {ex.Message}");
            }
        }
        public static void TestVanityWalletAndWeb3Auth(WalletService walletService)
        {
            Console.WriteLine("\n=== ЛАБОРАТОРНА РОБОТА: Vanity Wallets & Web3-Авторизація ===");

            var vanityService = new VanityWalletService(walletService);

            string desiredPrefix = "777";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (vanityWallet, attempts) = vanityService.MineWallet(desiredPrefix);
            watch.Stop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($">> [УСПІХ] Красивий гаманець знайдено!");
            Console.WriteLine($"Адреса: {vanityWallet.Address}");
            Console.WriteLine($"Кількість спроб (Brute-force): {attempts}");
            Console.WriteLine($"Час пошуку: {watch.Elapsed.TotalSeconds:F3} сек.");
            Console.ResetColor();

            string authMessage = "Sign this message to login into Web3 portal. Nonce: 482910";
            Console.WriteLine($"\nПовідомлення сайту для авторизації:\n\"{authMessage}\"\n");

            Console.WriteLine("--- Сценарій 1: Легітимна Web3-авторизація власника ---");
            byte[] validSignature = walletService.SignMessage(vanityWallet, authMessage);
            Console.WriteLine("[Клієнт] Власник успішно підписав повідомлення своїм приватним ключем.");

            bool isAliceApproved = walletService.VerifyMessage(
                vanityWallet.Address,
                vanityWallet.PublicKey,
                authMessage,
                validSignature
            );

            if (isAliceApproved)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(">> [Сервер РЕЗУЛЬТАТ]: Авторизація успішна! Ласкаво просимо на сайт.");
                Console.ResetColor();
            }

            Console.WriteLine("\n--- Сценарій 2: Атака 'Підміна публічного ключа' від Хакера ---");
            var hackerWallet = walletService.CreateWallet("Hacker");
            Console.WriteLine($"Адреса Хакера: {hackerWallet.Address}");

            byte[] hackerSignature = walletService.SignMessage(hackerWallet, authMessage);
            Console.WriteLine("[Клієнт/Хакер] Хакер згенерував підпис за допомогою власного приватного ключа.");
            Console.WriteLine("[Атака] Хакер надсилає запит: вказує ТВОЮ красиву адресу, але передає СВІЙ ключ та підпис...");

            bool isHackerApproved = walletService.VerifyMessage(
                vanityWallet.Address,
                hackerWallet.PublicKey,
                authMessage,
                hackerSignature
            );

            if (!isHackerApproved)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(">> [Сервер РЕЗУЛЬТАТ]: Атака заблокована. Систему безпеки НЕ обдурено!");
                Console.ResetColor();
            }
        }
        public static async Task RunExamSmartContracts(BlockChainService blockChain, TransactionService transactionService, WalletService walletService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== ЕКЗАМЕНАЦІЙНА РОБОТА: Епоха Смарт-Контрактів ===");
            Console.WriteLine($"Адреса Аліси: {walletAlice.Address}");
            Console.WriteLine($"Адреса Боба: {walletBob.Address}\n");

            Console.WriteLine("--- Крок 1: Аліса майнить блоки для капіталу BASE ---");
            for (int i = 1; i <= 3; i++)
            {
                var coinbaseTx = new Transaction("System", walletAlice.Address, 50m, walletAlice.PublicKey)
                {
                    Type = TransactionType.Transfer,
                    TokenTicker = "BASE",
                    Fee = 0
                };

                var txs = new List<Transaction> { coinbaseTx };
                var lastBlock = blockChain.Chain[blockChain.Chain.Count - 1];
                var newBlock = new Block(blockChain.Chain.Count, DateTime.UtcNow, txs, lastBlock.Hash);

                Console.WriteLine($"Майнінг блоку {i} Алісою...");
                await blockChain.ProcessBlockMiningAsync(newBlock);
                blockChain.Chain.Add(newBlock);
            }

            Console.WriteLine("\n[Стан портфелів після майнінгу]:");
            blockChain.PrintPortfolio(walletAlice.Address);
            blockChain.PrintPortfolio(walletBob.Address);

            Console.WriteLine("\n--- Крок 2: Аліса випускає 1000 токенів ALICE_COIN ---");
            try
            {
                var aliceIcoTx = transactionService.CreateToken(walletAlice, "ALICE_COIN", 1000m);
                var coinbaseTx = new Transaction("System", walletAlice.Address, 50m, walletAlice.PublicKey);
                var txs = new List<Transaction> { coinbaseTx, aliceIcoTx };
                var lastBlock = blockChain.Chain[blockChain.Chain.Count - 1];
                var newBlock = new Block(blockChain.Chain.Count, DateTime.UtcNow, txs, lastBlock.Hash);

                await blockChain.ProcessBlockMiningAsync(newBlock);
                blockChain.Chain.Add(newBlock);
                Console.WriteLine(">> [УСПІХ] Токен ALICE_COIN успішно створено та додано в блокчейн!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ПОМИЛКА] Не вдалося створити токен: {ex.Message}");
            }

            Console.WriteLine("\n--- Крок 3: Боб намагається випустити BOB_COIN (будучи бідним) ---");
            try
            {
                var bobIcoTx = transactionService.CreateToken(walletBob, "BOB_COIN", 500m);
                Console.WriteLine(">> ПОМИЛКА: Мережа чомусь прийняла ICO від бідного Боба!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ОЧІКУВАНА ВІДМОВА МЕРЕЖІ]: {ex.Message}");
            }

            Console.WriteLine("\n--- Крок 4: Боб намагається вкрасти бренд ALICE_COIN ---");
            var bobsCoinbase = new Transaction("System", walletBob.Address, 150m, walletBob.PublicKey);
            var bobBlock = new Block(blockChain.Chain.Count, DateTime.UtcNow, new List<Transaction> { bobsCoinbase }, blockChain.Chain[blockChain.Chain.Count - 1].Hash);
            await blockChain.ProcessBlockMiningAsync(bobBlock);
            blockChain.Chain.Add(bobBlock);

            try
            {
                Console.WriteLine("Боб має гроші і намагається виконати плагіат тікера 'ALICE_COIN'...");
                var bobStealTx = transactionService.CreateToken(walletBob, "ALICE_COIN", 5000m);
                Console.WriteLine(">> ПОМИЛКА: Мережа дозволила дублювання тікера!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ОЧІКУВАНА ВІДМОВА МЕРЕЖІ]: {ex.Message}");
            }

            Console.WriteLine("\n--- Крок 5: Аліса переказує 300 токенів ALICE_COIN Бобу ---");
            try
            {
                var transferTx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 300m, "ALICE_COIN", fee: 1m);
                var coinbaseTx = new Transaction("System", walletAlice.Address, 50m, walletAlice.PublicKey);
                var txs = new List<Transaction> { coinbaseTx, transferTx };
                var lastBlock = blockChain.Chain[blockChain.Chain.Count - 1];
                var newBlock = new Block(blockChain.Chain.Count, DateTime.UtcNow, txs, lastBlock.Hash);

                await blockChain.ProcessBlockMiningAsync(newBlock);
                blockChain.Chain.Add(newBlock);
                Console.WriteLine(">> [УСПІХ] Переказ 300 ALICE_COIN успішно виконано!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ПОМИЛКА] Переказ відхилено: {ex.Message}");
            }

            Console.WriteLine("\n--- Крок 6: ФІНАЛЬНІ МУЛЬТИВАЛЮТНІ ПОРТФЕЛІ ---");
            Console.WriteLine("========================================");
            Console.WriteLine("ПОРТФЕЛЬ АЛІСИ:");
            blockChain.PrintPortfolio(walletAlice.Address);
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("ПОРТФЕЛЬ БОБА:");
            blockChain.PrintPortfolio(walletBob.Address);
            Console.WriteLine("========================================");
        }
    }
}
