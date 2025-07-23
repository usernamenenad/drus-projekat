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
            List<Task> workerTasks = Enumerable.Range(0, 10).Select((i) =>
            {
                return Task.Run(() =>
                {
                    Thread.Sleep((i + 1) * 1000);
                    Worker worker = WorkerFactory.CallFactory(i);

                    // Simulate worker stop
                    Task.Run(() =>
                    {
                        Thread.Sleep((int)Math.Pow(i + 2, 3) * 1000); 
                        worker.StopWorking();
                    });

                    worker?.DoWork();
                });
            }).ToList();

            await Task.WhenAll(workerTasks);
        }
    }
}
