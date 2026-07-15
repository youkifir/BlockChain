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
        public static async Task TestReliableEconomy(BlockChainService blockChain, TransactionService transactionService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== ЛАБОРАТОРНА РОБОТА: Надійна економіка (Double Spend, Hard Cap та Аудит) ===");

            // Зберігаємо старий префікс, щоб не зламати загальну логіку програми після тесту
            string backupPrefix = blockChain.VanityPrefix;

            // ТИМЧАСОВО вимикаємо складний Vanity-майнінг для швидкого проходження циклу Hard Cap
            // Якщо у твоєму коді метод майнінгу орієнтується на blockChain.VanityPrefix, робимо його простим (наприклад, "0")
            blockChain.VanityPrefix = "0";

            // Гарантуємо, що Аліса має баланс для тесту
            if (blockChain.GetBalance(walletAlice.Address) < 50m)
            {
                await blockChain.AddBlockWithValidation(new List<Transaction>(), walletAlice.Address);
            }

            Console.WriteLine($"Поточний баланс Аліси: {blockChain.GetBalance(walletAlice.Address)} BASE");

            // ==========================================
            // 🛑 СЦЕНАРІЙ 1: Симуляція атаки Double Spend
            // ==========================================
            Console.WriteLine("\n--- Сценарій 1: Захист від Подвійної витрати (Double Spend) ---");

            var tx1 = transactionService.CreateTransaction(walletAlice, walletBob.Address, 50m, fee: 0m);
            var tx2 = transactionService.CreateTransaction(walletAlice, walletBob.Address, 50m, fee: 0m);
            var txList = new List<Transaction> { tx1, tx2 };

            try
            {
                Console.WriteLine("[Атака] Спроба надіслати в один блок дві транзакції по 50 BASE від Аліси...");
                await blockChain.AddBlockWithValidation(txList, walletBob.Address);
                Console.WriteLine(">> [ПОМИЛКА]: Блокчейн прийняв Double Spend транзакції!");
            }
            catch (InvalidOperationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($">> [УСПІХ] Атаку Double Spend заблоковано: {ex.Message}");
                Console.ResetColor();
            }

            // ==========================================
            // 🛑 СЦЕНАРІЙ 2: Тестування Hard Cap (Жорсткий ліміт)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 2: Жорсткий ліміт емісії (Hard Cap) ---");
            Console.WriteLine($"Старт емісії (TotalMinted): {blockChain.TotalMinted} / {blockChain.MaxSupply} BASE");
            Console.WriteLine("[Майнінг] Автоматичний випуск блоків до ліміту в 1000 BASE...");

            int blocksMined = 0;
            while (blockChain.TotalMinted < blockChain.MaxSupply)
            {
                // Майнимо порожні блоки. Завдяки префіксу "0" це відбудеться миттєво без спаму "cafe"
                await blockChain.AddBlockWithValidation(new List<Transaction>(), walletBob.Address);
                blocksMined++;
            }

            Console.WriteLine($"[!] Успішно замайнено {blocksMined} блоків для досягнення ліміту.");
            Console.WriteLine($"Поточна емісія після циклу: {blockChain.TotalMinted} / {blockChain.MaxSupply} BASE");

            // Пробуємо замайнити ще один блок ПІСЛЯ досягнення ліміту
            decimal balanceBeforeNextMine = blockChain.GetBalance(walletBob.Address);
            await blockChain.AddBlockWithValidation(new List<Transaction>(), walletBob.Address);
            decimal balanceAfterNextMine = blockChain.GetBalance(walletBob.Address);

            if (balanceBeforeNextMine == balanceAfterNextMine)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($">> [УСПІХ] Hard Cap працює! Після досягнення {blockChain.MaxSupply} BASE нагорода майнеру більше НЕ нараховується.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(">> [ПОМИЛКА]: Емісія перевищила встановлений Hard Cap!");
            }

            // ==========================================
            // 🛑 СЦЕНАРІЙ 3: Інспектор аудиту (Proof of Reserves)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 3: Перевірка чесності економіки (ValidateEconomy) ---");
            bool isEconomyValid = blockChain.ValidateEconomy();

            if (isEconomyValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(">> [РЕЗУЛЬТАТ АУДИТУ]: True (Усі монети чесно розподілені, розбіжностей немає).");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(">> [РЕЗУЛЬТАТ АУДИТУ]: False (Знайдено розбіжності в балансах!)");
                Console.ResetColor();
            }

            // ВІДНОВЛЮЄМО оригінальний префікс для нормальної роботи інших функцій програми
            blockChain.VanityPrefix = backupPrefix;
        }
        public static async Task TestMempoolProtectionAndRbf(BlockChainService blockChain, TransactionService transactionService, Wallet walletAlice, Wallet walletBob)
        {
            Console.WriteLine("\n=== ЛАБОРАТОРНА РОБОТА: Захист Мемпулу, RBF та Тіньові баланси ===");

            // 1. СЕКРЕТ ЧИСТОГО ВИВОДУ: Зберігаємо старий префікс і вимикаємо складний майнінг "cafe"
            string backupPrefix = blockChain.VanityPrefix;
            blockChain.VanityPrefix = "0"; // Тепер блоки для тестів будуть майнитися миттєво без спаму в консоль

            // Очищаємо мемпул перед початком тесту
            blockChain.ClearMempool();
            blockChain.MaxMempoolSize = 5;

            // Гарантуємо баланс Аліси для тестів спаму (майнимо блоки миттєво)
            while (blockChain.GetBalance(walletAlice.Address) < 200m)
            {
                // Викликай свій стандартний метод майнінгу блоку, наприклад:
                await blockChain.AddBlockWithValidation(new List<Transaction>(), walletAlice.Address);
            }

            Console.WriteLine($"Стартовий баланс Аліси: {blockChain.GetBalance(walletAlice.Address)} BASE");

            // ==========================================
            // 🗑️ СЦЕНАРІЙ 1: Спам-атака (Mempool Size Limit)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 1: Спам-атака (Лише 5 безкоштовних транзакцій) ---");
            int acceptedSpam = 0;
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    var tx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 0.01m, fee: 0m);
                    blockChain.AddTransactionToMemPool(tx);
                    acceptedSpam++;
                }
                catch (Exception ex)
                {
                    // Не спамимо кожну помилку, покажемо фінальний результат
                }
            }
            Console.WriteLine($"[!] Спроба надіслати 10 спам-транзакцій з Fee=0.");
            Console.WriteLine($"Разом транзакцій у мемпулі: {blockChain.GetMempoolCount()} (Очікується: 5)");

            // ==========================================
            // 🗑️ СЦЕНАРІЙ 2: Витіснення (Mempool Eviction)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 2: Витіснення дешевих транзакцій (Mempool Eviction) ---");
            try
            {
                Console.WriteLine("[Надсилання] Спроба відправити транзакцію з високою комісією Fee = 10...");
                var expensiveTx = transactionService.CreateTransaction(walletAlice, walletBob.Address, 1m, fee: 10m);
                blockChain.AddTransactionToMemPool(expensiveTx);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($">> [УСПІХ] Транзакція успішно витіснила спам. Поточний розмір мемпулу: {blockChain.GetMempoolCount()}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ПОМИЛКА] Не вдалося витіснити транзакцію: {ex.Message}");
            }

            // ==========================================
            // 🚀 СЦЕНАРІЙ 3: Replace-By-Fee (RBF)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 3: Прискорення транзакцій (Replace-By-Fee) ---");
            blockChain.ClearMempool();

            var rbfTx1 = transactionService.CreateTransaction(walletAlice, walletBob.Address, 5m, fee: 1m);
            blockChain.AddTransactionToMemPool(rbfTx1);
            Console.WriteLine($"[1] Додано базову транзакцію: 5 BASE, Fee: 1 BASE. Розмір мемпулу: {blockChain.GetMempoolCount()}");

            try
            {
                Console.WriteLine("[RBF Атака] Спроба надіслати таку ж транзакцію, але з рівною комісією Fee = 1...");
                var rbfTxBad = transactionService.CreateTransaction(walletAlice, walletBob.Address, 5m, fee: 1m);
                blockChain.AddTransactionToMemPool(rbfTxBad);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Очікувано відхилено: {ex.Message}");
            }

            try
            {
                Console.WriteLine("[RBF Прискорення] Спроба надіслати таку ж транзакцію з Fee = 15...");
                var rbfTxGood = transactionService.CreateTransaction(walletAlice, walletBob.Address, 5m, fee: 15m);
                blockChain.AddTransactionToMemPool(rbfTxGood);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($">> [УСПІХ] Перевірка розміру мемпулу після RBF: {blockChain.GetMempoolCount()} (Очікується: 1, відбулася заміна)");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> [ПОМИЛКА] RBF не спрацював: {ex.Message}");
            }

            // ==========================================
            // ⭐️ СЦЕНАРІЙ 4: Тіньовий баланс (Pending Balance)
            // ==========================================
            Console.WriteLine("\n--- Сценарій 4: Захист через Тіньові баланси (Pending Balance) ---");
            blockChain.ClearMempool();

            // Створюємо гаманець
            var testWallet = new WalletService(blockChain.Chain).CreateWallet("TestWallet");

            // Переказуємо йому гроші
            var fundTx = transactionService.CreateTransaction(walletAlice, testWallet.Address, 39m, fee: 1m);

            // Замість виклику MineBlock із префіксом "cafe", пакуємо через наш швидкий метод зі зміненим префіксом
            await blockChain.AddBlockWithValidation(new List<Transaction> { fundTx }, walletAlice.Address);

            Console.WriteLine($"Реальний початковий баланс нового гаманця: {blockChain.GetBalance(testWallet.Address)} BASE (Очікується: 39)");

            try
            {
                Console.WriteLine("[Транзакція 1] Спроба відправити 28 BASE + 2 BASE Fee (Разом: 30)...");
                var tx1 = transactionService.CreateTransaction(testWallet, walletBob.Address, 28m, fee: 2m);
                blockChain.AddTransactionToMemPool(tx1);
                Console.WriteLine($"[+] Перша транзакція в мемпулі. Тіньовий залишок: {blockChain.GetPendingBalance(testWallet.Address)} BASE");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Помилка: {ex.Message}");
            }

            try
            {
                Console.WriteLine("[Транзакція 2] Спроба ОДРАЗУ відправити ще 18 BASE + 2 BASE Fee (Разом: 20)...");
                var tx2 = transactionService.CreateTransaction(testWallet, walletBob.Address, 18m, fee: 2m);
                blockChain.AddTransactionToMemPool(tx2);
                Console.WriteLine(">> [ПОМИЛКА]: Блокчейн дозволив витратити тіньовий нуль!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($">> [УСПІХ] Другу транзакцію заблоковано через брак тіньового балансу.");
                Console.ResetColor();
            }

            blockChain.ClearMempool();

            // 2. ВІДНОВЛЮЄМО оригінальний префікс для нормальної роботи блокчейну поза цим тестом
            blockChain.VanityPrefix = backupPrefix;
        }
    }
}
