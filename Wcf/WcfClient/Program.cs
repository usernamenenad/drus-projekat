using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WcfClient
{
    internal class Program
    {
        static async Task Main()
        {
            Random random = new Random();

            var fromZeroToNine = Enumerable.Range(0, 10).ToList();
            var workerTasks = new List<Task>();

            while(fromZeroToNine.Count > 0)
            {
                int randomIndex = random.Next(0, fromZeroToNine.Count);
                int workerId = fromZeroToNine[randomIndex];
                fromZeroToNine.RemoveAt(randomIndex);

                workerTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(random.Next(1, 10) * 1000);
                    Worker worker = WorkerFactory.CallFactory(workerId);

                    var doWorkTask = Task.Run(() =>
                    {
                        worker?.DoWork();
                    });

                    var stopWorkTask = Task.Run(async () =>
                    {
                        await Task.Delay((int)Math.Pow(random.Next(3, 10), 2) * 1000);
                        if (random.Next(0, 5) == 3)
                        {
                            // Simulate worker's absolute death
                            worker.Logger.Log($"[Worker {workerId}] Worker will simulate death by killing thread.");
                            worker.StopWork();
                            return;
                        }

                        // Simulate worker revival
                        worker.Logger.Log($"[Worker {workerId}] Worker will simulate death by not sending heartbeat signal for 20 seconds.");
                        worker.StopSendingHeartbeat();
                        await Task.Delay(20 * 1000);
                        worker.StartSendingHeartbeat();
                    });

                    await Task.WhenAll(doWorkTask, stopWorkTask);
                }));
            }

            await Task.WhenAll(workerTasks);
        }
    }
}
