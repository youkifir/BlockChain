using BlockChain_1.Models;
using BlockChain_1.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_1
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Input port number for P2P service: ");
            var portInput = Console.ReadLine();

            //Services
            var displayService = new BlockChainDisplayService();
            var blockChain1 = new BlockChainService(initialDifficulty: 4);
            var transactionService = new TransactionService(blockChain1.Chain);
            var walletService = new WalletService(blockChain1.Chain);

            var p2pService = new TcpP2pService(blockChain1, int.Parse(portInput));
            p2pService.Start();

            Console.WriteLine("Input port for connect to other node: ");
            var peerPort = int.Parse(Console.ReadLine());
            if (peerPort != 0)
            {
                await p2pService.ConnectToPeerAsync("127.0.0.1", peerPort);
                Console.WriteLine("Connected to peer.");
            }

            //Show menu
            Console.WriteLine("--- BlockChain Menu ---");
            Console.WriteLine("1. Mine Block");
            Console.WriteLine("2. Create Transaction");
            Console.WriteLine("3. Show Alice Balance");
            Console.WriteLine("4. Show Bob Balance");
            Console.WriteLine("5. Validate Blockchain");
            Console.WriteLine("6. Print Blockchain");
            Console.WriteLine("7. Exit");
            Console.WriteLine("8. Change Blockchain");
            Console.WriteLine("9. Test: знайти індекс підробленого блоку");
            Console.WriteLine("10. Test: Vanity Mining (пошук HEX-слова у хеші)");
            Console.WriteLine("11. Test: Смарт-пакування 15 транзакцій у блоки");

            //Wallets
            var walletAlice = walletService.CreateWallet("Alice");
            var walletBob = walletService.CreateWallet("Bob");
            var walletCharlie = walletService.CreateWallet("Charlie");
            var walletDave = walletService.CreateWallet("Dave");

            // Тепер це не імена, а фейкові крипто-адреси у стилі Ethereum,
            // детерміновано виведені з публічного ключа гаманця (WalletService.DeriveAddress).
            Console.WriteLine($"Alice address:   {walletAlice.Address}");
            Console.WriteLine($"Bob address:     {walletBob.Address}");
            Console.WriteLine($"Charlie address: {walletCharlie.Address}");
            Console.WriteLine($"Dave address:    {walletDave.Address}");

            while (true)
            {
                Console.Write("\nChoose an option: ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await blockChain1.MineBlock(walletAlice.Address); // Mine block with Alice's address as the miner
                        Console.WriteLine("Block mined successfully.");
                        break;

                    case "2":
                        try
                        {
                            var transaction1 = transactionService.CreateTransaction(walletAlice, walletBob.Address, 10m); // Create transaction from Alice to Bob
                            blockChain1.AddTransactionToMemPool(transaction1);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                        break;

                    case "3":
                        Console.WriteLine($"Alice's balance: {walletService.GetBalance(walletAlice.Address)}"); // ALice balance
                        break;

                    case "4":
                        Console.WriteLine($"Bob's balance: {walletService.GetBalance(walletBob.Address)}"); // Bob balance
                        break;

                    case "5":
                        bool isValid = blockChain1.IsValid();
                        displayService.PrintChainValidity(isValid);
                        break;

                    case "6":
                        displayService.PrintChain(blockChain1.Chain); // Print blockchain
                        break;

                    case "7":
                        return; // Exit

                    case "8":
                        if (blockChain1.Chain.Count > 1 && blockChain1.Chain[1].Transactions.Count > 0)
                        {
                            blockChain1.Chain[1].Transactions[0].Amount = 100;
                            Console.WriteLine("Blockchain modified. Please validate again.");
                        }
                        else
                        {
                            Console.WriteLine("Use option 2 to add a transaction first.");
                        }
                        break;

                    case "9":
                        await TestGetInvalidBlockIndex(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    case "10":
                        await TestVanityMining(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    case "11":
                        await TestSmartChunking(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    default:
                        Console.WriteLine("Choose correct option");
                        break;
                }
            }

        }

        //Test
        static async Task TestGetInvalidBlockIndex(
            BlockChainService blockChain,
            TransactionService transactionService,
            Wallet walletAlice,
            Wallet walletBob)
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

        static async Task TestVanityMining(
            BlockChainService blockChain,
            TransactionService transactionService,
            Wallet walletAlice,
            Wallet walletBob)
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

        /// <summary>
        /// Демонстрація Частини 1 і 2:
        /// 1) майнить кілька блоків, щоб Аліса мала баланс;
        /// 2) генерує 15 коректних (0x-адреси) транзакцій Аліса -> Боб;
        /// 3) віддає їх у ProcessTransactions - метод сам розбиває на блоки за вагою;
        /// 4) намагається створити транзакцію на невалідну адресу "Bob" (не 0x-формат)
        ///    і показує, що валідатор її відхиляє.
        /// </summary>
        static async Task TestSmartChunking(
            BlockChainService blockChain,
            TransactionService transactionService,
            Wallet walletAlice,
            Wallet walletBob)
        {
            Console.WriteLine("\n=== Тест: Смарт-пакування блоків ===");
            Console.WriteLine($"MaxBlockSizeBytes = {blockChain.MaxBlockSizeBytes} байт");

            // Даємо Алісі баланс для 15 транзакцій.
            while (blockChain.GetBalance(walletAlice.Address) < 5m)
            {
                await blockChain.MineBlock(walletAlice.Address);
                Console.WriteLine($"Намайнено блок для поповнення балансу Аліси. Баланс: {blockChain.GetBalance(walletAlice.Address)}");
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
    }
}