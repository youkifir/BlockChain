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

            //Services
            var displayService = new BlockChainDisplayService();
            var blockChain1 = new BlockChainService(initialDifficulty: 4);
            var transactionService = new TransactionService(blockChain1.Chain);
            var walletService = new WalletService(blockChain1.Chain);

            //Walets
            var walletAlice = walletService.CreateWallet("Alice");
            var walletBob = walletService.CreateWallet("Bob");
            var walletCharlie = walletService.CreateWallet("Charlie");
            var walletDave = walletService.CreateWallet("Dave");

            //Start P2P service
            Console.WriteLine("Input port for this node: ");
            var port = Console.ReadLine();

            var p2pService = new TcpP2pService(blockChain1, int.Parse(port));
            p2pService.Start();

            //Display wallet addresses
            Console.WriteLine($"Alice address:   {walletAlice.Address}");
            Console.WriteLine($"Bob address:     {walletBob.Address}");
            Console.WriteLine($"Charlie address: {walletCharlie.Address}");
            Console.WriteLine($"Dave address:    {walletDave.Address}");

            //Display menu
            Console.WriteLine("\n--- BlockChain Menu ---");
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
            Console.WriteLine("12. Connect to another node");
            Console.WriteLine("13. Test: Vanity Wallets & Web3-Авторизація");
            Console.WriteLine("14. Run Exam: Епоха Смарт-Контрактів");
            Console.WriteLine("15. Test: Reliable Economy (надійна економіка)");
            Console.WriteLine("16. Test: Захист Мемпулу, RBF та Тіньові баланси");
            Console.WriteLine("17. Test: Макроекономіка блокчейну (Халвінг, Дилема, Спалювання)");

            //Main loop
            while (true)
            {
                Console.Write("\nChoose an option: ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await blockChain1.MineBlock(walletAlice.Address);
                        Console.WriteLine("Block mined successfully.");
                        break;

                    case "2":
                        try
                        {
                            var transaction1 = transactionService.CreateTransaction(walletAlice, walletBob.Address, 10m);
                            blockChain1.AddTransactionToMemPool(transaction1);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                        break;

                    case "3":
                        Console.WriteLine($"Alice's balance: {walletService.GetBalance(walletAlice.Address)}");
                        break;

                    case "4":
                        Console.WriteLine($"Bob's balance: {walletService.GetBalance(walletBob.Address)}");
                        break;

                    case "5":
                        bool isValid = blockChain1.IsValid();
                        displayService.PrintChainValidity(isValid);
                        break;

                    case "6":
                        displayService.PrintChain(blockChain1.Chain);
                        break;

                    case "7":
                        return;

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
                        await Tests.TestGetInvalidBlockIndex(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    case "10":
                        await Tests.TestVanityMining(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    case "11":
                        await Tests.TestSmartChunking(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    case "12":
                        Console.WriteLine("Input port for connect to another node:");
                        var peerPort = int.Parse(Console.ReadLine());
                        if (peerPort != 0)
                        {
                            await p2pService.ConnectToPeerAsync("127.0.0.1", peerPort);
                            Console.WriteLine($"Connected to peer at port {peerPort}");
                        }
                        break;

                    case "13":
                        Tests.TestVanityWalletAndWeb3Auth(walletService);
                        break;

                    case "14":
                        await Tests.RunExamSmartContracts(blockChain1, transactionService, walletService, walletAlice, walletBob);
                        break;
                    case "15":
                        await Tests.TestReliableEconomy(blockChain1, transactionService, walletAlice, walletBob);
                        break;
                    case "16":
                        await Tests.TestMempoolProtectionAndRbf(blockChain1, transactionService, walletAlice, walletBob);
                        break;
                    case "17":
                        await Tests.TestMacroeconomics(blockChain1, transactionService, walletAlice, walletBob);
                        break;

                    default:
                        Console.WriteLine("Choose correct option");
                        break;
                }
            }
        }
    }
}