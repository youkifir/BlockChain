using BlockChain_1.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class MiningService
    {
        private readonly HashingService _hashingService;

        private const int BatchSize = 50_000;

        public int LastThreadsUsed { get; private set; }
        public long LastTotalHashes { get; private set; }
        public double LastElapsedSeconds { get; private set; }
        public double LastHashRateHs { get; private set; }

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

            long totalAttempts = 0;

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
                    long localAttempts = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        string hash = _hashingService.ComputeHash(localBlock);
                        localAttempts++;

                        if (hash.StartsWith(target))
                        {
                            Interlocked.Add(ref totalAttempts, localAttempts);

                            if (Interlocked.CompareExchange(ref found, 1, 0) == 0)
                            {
                                foundNonce = localBlock.Nonce;
                                foundHash = hash;
                                cts.Cancel();
                            }
                            return;
                        }
                        if (localAttempts >= BatchSize)
                        {
                            Interlocked.Add(ref totalAttempts, localAttempts);
                            localAttempts = 0;
                        }

                        localBlock.Nonce += workers;
                    }

                    if (localAttempts > 0)
                    {
                        Interlocked.Add(ref totalAttempts, localAttempts);
                    }
                }, cts.Token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }

            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double hashRate = elapsedSeconds > 0 ? totalAttempts / elapsedSeconds : 0;

            LastThreadsUsed = workers;
            LastTotalHashes = totalAttempts;
            LastElapsedSeconds = elapsedSeconds;
            LastHashRateHs = hashRate;

            PrintMiningStats(workers, difficulty, elapsedSeconds, totalAttempts, hashRate, found == 1);

            if (found == 1)
            {
                block.Nonce = (int)foundNonce;
                block.Hash = foundHash;
                block.MiningDuration = elapsedSeconds;
                return true;
            }

            token.ThrowIfCancellationRequested();
            return false;
        }

        private static void PrintMiningStats(int threads, int difficulty, double elapsedSeconds, long totalHashes, double hashRateHs, bool success)
        {
            Console.WriteLine("--- Метрики майнінгу ---");
            Console.WriteLine($"Складність (Difficulty): {difficulty}");
            Console.WriteLine($"Задіяно потоків: {threads}");
            Console.WriteLine($"Витрачений час: {elapsedSeconds:F3} с");
            Console.WriteLine($"Всього перевірено хешів: {totalHashes.ToString("N0", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Hashrate: {FormatHashRate(hashRateHs)}");
            Console.WriteLine(success ? "Блок успішно знайдено." : "Блок не знайдено (скасовано).");
            Console.WriteLine("------------------------");
        }

        private static string FormatHashRate(double hashesPerSecond)
        {
            if (hashesPerSecond >= 1_000_000)
                return $"{(hashesPerSecond / 1_000_000).ToString("F2", CultureInfo.InvariantCulture)} MH/s";

            if (hashesPerSecond >= 1_000)
                return $"{(hashesPerSecond / 1_000).ToString("F2", CultureInfo.InvariantCulture)} KH/s";

            return $"{hashesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} H/s";
        }
    }
}