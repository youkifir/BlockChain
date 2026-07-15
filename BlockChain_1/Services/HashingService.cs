using BlockChain_1.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChain_1.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            // Сам бере транзакції блоку і будує дерево
            string merkleRoot = GetMerkleTree(block.Transactions);

            // Перенаправляє в твій чинний метод
            return ComputeHash(block, merkleRoot);
        }
        public string ComputeHash(Block block, string merkleRoot)
        {
            var blockData = $"{block.Index}{block.TimeStamp.ToString("O")}{merkleRoot}{block.PreviousHash}{block.Nonce}";
            return HashString(blockData);
        }
        private string HashString(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
        public string GetMerkleTree(List<Transaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
            {
                return string.Empty;
            }

            var currentLayer = new List<string>();
            foreach (var tx in transactions)
            {
                currentLayer.Add(HashString(tx.ToHashString()));
            }

            int level = 0;
            Console.WriteLine($"Level {level} (Листя): {currentLayer.Count} хешів");

            while (currentLayer.Count > 1)
            {
                level++;
                var nextLayer = new List<string>();

                for (int i = 0; i < currentLayer.Count; i += 2)
                {
                    string left = currentLayer[i];
                    string right = (i + 1 < currentLayer.Count) ? currentLayer[i + 1] : left;
                    nextLayer.Add(HashString(left + right));
                }

                currentLayer = nextLayer;

                if (currentLayer.Count == 1)
                {
                    Console.WriteLine($"Level {level} (Корінь): {currentLayer.Count} хеш");
                }
                else
                {
                    Console.WriteLine($"Level {level} (Гілки): {currentLayer.Count} хеші");
                }
            }

            return currentLayer[0];
        }
        public List<(string Hash, bool IsLeft)> GetMerkleProof(List<Transaction> transactions, string targetTransactionId)
        {
            if (transactions == null || transactions.Count == 0 || string.IsNullOrEmpty(targetTransactionId))
            {
                return new List<(string Hash, bool IsLeft)>();
            }

            int targetIndex = transactions.FindIndex(t => t.Id == targetTransactionId);
            if (targetIndex == -1)
            {
                return new List<(string Hash, bool IsLeft)>();
            }

            List<List<string>> treeLevels = new List<List<string>>();

            List<string> currentLevel = transactions.Select(t => t.GetHashCode().ToString()).ToList();
            treeLevels.Add(currentLevel);

            while (currentLevel.Count > 1)
            {
                List<string> nextLevel = new List<string>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    string left = currentLevel[i];
                    string right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left;

                    nextLevel.Add(HashString(left + right));
                }
                currentLevel = nextLevel;
                treeLevels.Add(currentLevel);
            }

            List<(string Hash, bool IsLeft)> proof = new List<(string Hash, bool IsLeft)>();
            int currentIndex = targetIndex;

            for (int i = 0; i < treeLevels.Count - 1; i++)
            {
                List<string> level = treeLevels[i];
                bool isTargetLeft = (currentIndex % 2 == 0);

                int neighborIndex;
                bool isNeighborLeft;

                if (isTargetLeft)
                {
                    neighborIndex = (currentIndex + 1 < level.Count) ? currentIndex + 1 : currentIndex;
                    isNeighborLeft = false;
                }
                else
                {
                    neighborIndex = currentIndex - 1;
                    isNeighborLeft = true;
                }

                proof.Add((level[neighborIndex], isNeighborLeft));

                currentIndex /= 2;
            }

            return proof;
        }
    }
}

