using System;

using WcfClient.ServiceReference;

namespace WcfClient
{
    internal class WorkerFactory
    {
        public static Worker CallFactory(int workerId)
        {
            Worker newWorker = new Worker(workerId);
            if(newWorker.Register().Status == MessageStatus.Ok)
            {
                Console.WriteLine($"[Worker {workerId}] Worker registred!");
                return newWorker;
            }

            return null;
        }
    }
}
