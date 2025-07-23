using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Timers;

namespace WcfService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class Service : IService
    {
        private readonly ConcurrentDictionary<int, WorkerInfo> _workerInfo = new ConcurrentDictionary<int, WorkerInfo>();

        private readonly System.Timers.Timer _timer;
        private readonly Random _random = new Random();

        public Service()
        {
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += CheckIfAnyDead;
            _timer.AutoReset = true;

            _timer.Start();
            Console.WriteLine("[Service] Service started!");
        }

        public Message Register(int registrationWorkerId)
        {
            Console.WriteLine($"[Service] Took request from {registrationWorkerId}!");

            var newWorkerInfo = new WorkerInfo()
            {
                State = WorkerState.Standby,
                LastHeartbeat = DateTime.UtcNow,
                Callback = OperationContext.Current.GetCallbackChannel<ICallback>(),
            };

            if (_workerInfo.TryAdd(registrationWorkerId, newWorkerInfo))
            {
                if(GetActiveWorkers().Count + 1 < 6)
                {
                    ChangeWorkerState(registrationWorkerId, WorkerState.Active);
                }

                Console.WriteLine($"[Service] Added worker {registrationWorkerId} with state {newWorkerInfo.State}");
                
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

        public void SendHeartbeat(int workerId)
        {
            if (!CheckIfShouldConsiderDead(workerId))
            {
                Console.WriteLine($"[Service] Received heartbeat signal from alive worker {workerId}.");
                _workerInfo[workerId].LastHeartbeat = DateTime.UtcNow;
                return;
            }

            Console.WriteLine($"[Service] Received heartbeat signal from worker {workerId} that should be considered dead!");
            ChangeWorkerState(workerId, WorkerState.Dead);
        }

        private void CheckIfAnyDead(object sender, ElapsedEventArgs e)
        {            
            foreach(KeyValuePair<int, WorkerInfo> workerInfo in _workerInfo)
            {
                if(CheckIfShouldConsiderDead(workerInfo.Key))
                {
                    ChangeWorkerState(workerInfo.Key, WorkerState.Dead);
                    int replacerId = ReplaceDeadWorker();
                    if(replacerId != -1)
                    {
                        Console.WriteLine($"[Service] Worker {workerInfo.Key} dead! Trying to replace it with {replacerId}...");
                    }
                }
            }
        }

        private bool CheckIfShouldConsiderDead(int workerId)
        {
            if (_workerInfo.TryGetValue(workerId, out var workerInfo))
            {
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
            catch (Exception)
            {
                // Worker is shutted down and not alive.   
            }
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
