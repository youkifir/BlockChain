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

        public async Task<bool> MineBlockAsync(Block block, int difficulty, CancellationToken token = default)
        {
            string target = new string('0', difficulty);
            int workers = Environment.ProcessorCount;

            long foundNonce = -1;
            string foundHash = null;
            int found = 0;

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
            catch (OperationCanceledException) { }

            stopwatch.Stop();

            if (found == 1)
            {
                block.Nonce = (int)foundNonce;
                block.Hash = foundHash;
                block.MiningDuration = stopwatch.Elapsed.TotalSeconds;
                return true;
            }

            token.ThrowIfCancellationRequested();
            return false;
        }
    }
}