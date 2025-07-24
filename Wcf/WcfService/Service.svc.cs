using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Timers;

namespace WcfService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class Service : IService
    {
        private readonly ConcurrentDictionary<int, WorkerInfo> _workerInfo = new ConcurrentDictionary<int, WorkerInfo>();

        private readonly Timer _timer;
        private readonly Random _random = new Random();

        private const int CHECKING_DEAD_INTERVAL = 1000;
        private const int MAX_CONCURRENT_WORKERS = 5;

        private readonly object _lock = new object();

        public Service()
        {
            _timer = new Timer()
            {
                Interval = CHECKING_DEAD_INTERVAL,
                AutoReset = true,
            };
            _timer.Elapsed += CheckIfAnyDeadAndReplace;

            _timer.Start();
            Console.WriteLine("[Service] Service started!");
        }

        public Message Register(int registrationWorkerId)
        {
            Console.WriteLine($"[Service] Took request from {registrationWorkerId}!");

            lock(_lock)
            {
                var newWorkerInfo = new WorkerInfo()
                {
                    State = WorkerState.Standby,
                    LastHeartbeat = DateTime.UtcNow,
                    Callback = OperationContext.Current.GetCallbackChannel<ICallback>(),
                };

                if (_workerInfo.TryAdd(registrationWorkerId, newWorkerInfo))
                {

                    if (GetActiveWorkers().Count < MAX_CONCURRENT_WORKERS)
                    {
                        ChangeWorkerState(registrationWorkerId, WorkerState.Active);
                        Console.WriteLine($"[Service] Now working - [{string.Join(", ", GetActiveWorkers().Select(kv => kv.Key.ToString()).ToList())}]");
                    }
                    
                    Console.WriteLine($"[Service] Added new worker {registrationWorkerId} with status {newWorkerInfo.State}");
                    return new Message()
                    {
                        Status = MessageStatus.Ok
                    };
                }

                Console.WriteLine($"[Service] Worker {registrationWorkerId} was already registred!");
                if (CheckIfShouldConsiderDead(registrationWorkerId))
                {
                    ChangeWorkerState(registrationWorkerId, WorkerState.Dead);
                }

                return new Message()
                {
                    Status = MessageStatus.Error,
                    Error = MessageError.AlreadyRegistred
                };
            }
        }

        public void SendHeartbeat(int workerId)
        {
            if (_workerInfo.TryGetValue(workerId, out var workerInfo))
            {
                // Already dead!
                if(workerInfo.State == WorkerState.Dead)
                {
                    Console.WriteLine($"[Service] Received heartbeat signal from worker {workerId} that should be considered dead!");
                    workerInfo.Callback.ShutdownWorker();
                    return;
                }
            }

            if (!CheckIfShouldConsiderDead(workerId))
            {
                _workerInfo[workerId].LastHeartbeat = DateTime.UtcNow;
                return;
            }
        }

        private void CheckIfAnyDeadAndReplace(object sender, ElapsedEventArgs e)
        {            
            foreach(KeyValuePair<int, WorkerInfo> workerInfo in _workerInfo)
            {
                if(CheckIfShouldConsiderDead(workerInfo.Key))
                {
                    var wasActive = workerInfo.Value.State == WorkerState.Active;
                    ChangeWorkerState(workerInfo.Key, WorkerState.Dead);

                    if (wasActive)
                    {
                        int replacerId = ReplaceDeadWorker();
                        if(replacerId != -1)
                        {
                            Console.WriteLine($"[Service] Active worker {workerInfo.Key} dead! Trying to replace it with {replacerId}...");
                        }
                        Console.WriteLine($"[Service] Now working - [{string.Join(", ", GetActiveWorkers().Select(kv => kv.Key.ToString()).ToList())}]");
                    }
                }
            }
        }

        private bool CheckIfShouldConsiderDead(int workerId)
        {
            if (_workerInfo.TryGetValue(workerId, out var workerInfo))
            {
                if(workerInfo.State == WorkerState.Dead)
                {
                    return false; // Already dead, skip it for checking.
                }

                return DateTime.UtcNow - workerInfo.LastHeartbeat > TimeSpan.FromSeconds(15);
            }

            return false; // Not registred at all, skip it.
        }

        private List<KeyValuePair<int, WorkerInfo>> GetActiveWorkers()
        {
            return _workerInfo.Where((kv) => kv.Value.State == WorkerState.Active).ToList();
        }

        private List<KeyValuePair<int, WorkerInfo>> GetStandbyWorkers()
        {
            return _workerInfo.Where((kv) => kv.Value.State == WorkerState.Standby).ToList();
        }

        private void ChangeWorkerState(int workerId, WorkerState newState)
        {
            _workerInfo[workerId].State = newState;

            try
            {
                _workerInfo[workerId].Callback.ChangeWorkerState(newState);
            }
            catch { }
        }

        private int ReplaceDeadWorker()
        {
            List<KeyValuePair<int, WorkerInfo>> possibleReplacerWorkers = GetStandbyWorkers();
            if(possibleReplacerWorkers.Count > 0)
            {
                int randomIndex = _random.Next(possibleReplacerWorkers.Count);
                int randomWorkerId = possibleReplacerWorkers[randomIndex].Key;
                ChangeWorkerState(randomWorkerId, WorkerState.Active);

                return randomWorkerId;
            }

            return -1;
        }
    }
}
