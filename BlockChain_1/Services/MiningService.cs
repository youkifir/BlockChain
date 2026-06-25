using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        public long MineBlock(Block block, int difficulty)
        {
            string target = new string('0', difficulty);

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                block.Hash = _hashingService.ComputeHash(block);
                if (block.Hash.StartsWith(target))
                {
                    stopwatch.Stop();
                    block.MiningDuration = stopwatch.Elapsed.TotalSeconds;

                    return block.Nonce;
                }
                block.Nonce++;

                if (block.Nonce % 10000 == 0)
                {
                    Console.Write($".");
                }
            }
        }

        public async Task<long?> MineBlockAsync(Block block, int difficulty, CancellationToken token)
        {
            string target = new string('0', difficulty);
            int workers = Environment.ProcessorCount;

            long foundNonce = -1;
            string foundHash = null;
            int found = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            List<Task> tasks = new();

            var stopwatch = Stopwatch.StartNew();

            for (int workerId = 0; workerId < workers; workerId++)
            {
                int localWorker = workerId;

                // Создаем глубокую или поверхностную копию блока для каждого потока,
                // чтобы избежать Race Condition при изменении block.Nonce и чтении в ComputeHash
                var localBlock = new Block(block.Index, block.TimeStamp, block.Transactions, block.PreviousHash)
                {
                    Nonce = localWorker
                };

                tasks.Add(Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        string hash = _hashingService.ComputeHash(localBlock);

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
            catch (OperationCanceledException)
            {
                // Игнорируем исключение отмены, так как это ожидаемое поведение
            }

            stopwatch.Stop();

            if (found == 1)
            {
                block.Nonce = (int)foundNonce;
                block.Hash = foundHash;
                block.MiningDuration = stopwatch.Elapsed.TotalSeconds;

                return foundNonce;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }
    }
}