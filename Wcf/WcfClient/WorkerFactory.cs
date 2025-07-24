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
                newWorker.Logger.Log($"[Worker {workerId}] Worker registred!");
                newWorker.StartSendingHeartbeat();
                return newWorker;
            }

            return null;
        }
    }
}
