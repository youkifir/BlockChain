using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class MiningService
    {
        private readonly HashingService _hashingService;

        public MiningService(HashingService hashingService)
        {
            _hashingService = hashingService;
        }

        public async Task<bool> MineBlockAsync(Block block, string vanityTarget, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(vanityTarget))
                throw new ArgumentException("Vanity target не може бути порожнім.", nameof(vanityTarget));

            string target = vanityTarget.ToUpperInvariant();

            foreach (char c in target)
            {
                bool isValidHexChar = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
                if (!isValidHexChar)
                {
                    throw new ArgumentException(
                        $"Символ '{c}' не є HEX-символом (дозволені лише 0-9, a-f). " +
                        $"Приклади коректних слів: cafe, beef, dead, face, c0de, b0b.",
                        nameof(vanityTarget));
                }
            }

            int workers = Environment.ProcessorCount;
            long foundNonce = -1;
            string foundHash = null;
            int found = 0;

            string merkleRoot = _hashingService.GetMerkleTree(block.Transactions);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var tasks = new List<Task>();
            var stopwatch = Stopwatch.StartNew();

            for (int workerId = 0; workerId < workers; workerId++)
            {
                int localId = workerId;
                var localBlock = new Block(block.Index, block.TimeStamp, block.Transactions, block.PreviousHash)
                {
                    Nonce = localId
                };

                tasks.Add(Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        string hash = _hashingService.ComputeHash(localBlock, merkleRoot);

                        if (hash.StartsWith(target))
                        {
                            if (Interlocked.CompareExchange(ref found, 1, 0) == 0)
                            {
                                foundNonce = localBlock.Nonce;
                                foundHash = hash;
                                cts.Cancel();
                            }
                            return;
                        }

                        localBlock.Nonce += workers;
                    }
                }, cts.Token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }

            stopwatch.Stop();

            if (found == 1)
            {
                block.Nonce = (int)foundNonce;
                block.Hash = foundHash;
                block.MiningDuration = stopwatch.Elapsed.TotalSeconds;

                Console.WriteLine($"[VanityMiner] Знайдено блок з префіксом \"{vanityTarget}\": {foundHash}");
                Console.WriteLine($"[VanityMiner] Nonce: {foundNonce} | Час пошуку: {stopwatch.Elapsed.TotalSeconds:F3} с | Потоків: {workers}");

                return true;
            }

            token.ThrowIfCancellationRequested();
            return false;
        }
    }
}