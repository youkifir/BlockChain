using BlockChain_1.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BlockChain_1.Services
{
    public class FileStorageService
    {
        private const string BlockchainFile = "blockchain.json";
        private const string WalletsFile = "wallets.json";

        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public void SaveBlockchain(List<Block> blockchain) =>
            File.WriteAllText(BlockchainFile, JsonSerializer.Serialize(blockchain, _jsonOptions));

        public List<Block> LoadBlockchain()
        {
            if (!File.Exists(BlockchainFile)) return new List<Block>();
            return JsonSerializer.Deserialize<List<Block>>(File.ReadAllText(BlockchainFile)) ?? new List<Block>();
        }

        public void SaveWallets(List<Wallet> wallets) =>
            File.WriteAllText(WalletsFile, JsonSerializer.Serialize(wallets, _jsonOptions));

        public List<Wallet> LoadWallets()
        {
            if (!File.Exists(WalletsFile)) return new List<Wallet>();
            return JsonSerializer.Deserialize<List<Wallet>>(File.ReadAllText(WalletsFile)) ?? new List<Wallet>();
        }
    }
}