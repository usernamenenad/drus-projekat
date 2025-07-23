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
            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += CheckIfAnyDead;
            _timer.AutoReset = true;

            _timer.Start();
        }

        public Message Register(int registrationWorkerId)
        {
            int currentActiveWorkers = GetActiveWorkers().Count();
            var newWorkerInfo = new WorkerInfo()
            {
                State = currentActiveWorkers + 1 < 6 ? WorkerState.Active : WorkerState.Standby,
                LastHeartbeat = DateTime.UtcNow,
                Callback = OperationContext.Current.GetCallbackChannel<ICallback>(),
            };

            if (_workerInfo.TryAdd(registrationWorkerId, newWorkerInfo))
            {
                return new Message()
                {
                    Status = MessageStatus.Ok
                };
            }

            return new Message()
            {
                Status = MessageStatus.Error,
                Error = MessageError.AlreadyRegistred
            };
        }

        public void SendHeartbeat(int workerId)
        {
            if(_workerInfo.TryGetValue(workerId, out var workerInfo))
            {
                workerInfo.LastHeartbeat = DateTime.UtcNow;
            }
        }

        private void CheckIfAnyDead(object sender, ElapsedEventArgs e)
        {            
            foreach(KeyValuePair<int, WorkerInfo> workerInfo in _workerInfo)
            {
                if(CheckIfShouldConsiderDead(workerInfo.Key))
                {
                    ChangeWorkerState(workerInfo.Key, WorkerState.Dead);
                    ReplaceDeadWorker();
                }
            }
        }

        private bool CheckIfShouldConsiderDead(int workerId)
        {
            if (_workerInfo.TryGetValue(workerId, out var workerInfo))
            {
                return DateTime.UtcNow - workerInfo.LastHeartbeat > TimeSpan.FromSeconds(15);
            }

            return true;
        }

        private List<KeyValuePair<int, WorkerInfo>> GetActiveWorkers()
        {
            return _workerInfo.Where((kv) => kv.Value.State == WorkerState.Active).ToList();
        }

        private List<KeyValuePair<int, WorkerInfo>> GetStandbyWorkers()
        {
            return _workerInfo.Where((kv) => kv.Value.State == WorkerState.Active).ToList();
        }

        private void ChangeWorkerState(int workerId, WorkerState newState)
        {
            _workerInfo[workerId].State = newState;
            _workerInfo[workerId].Callback.ChangeWorkerState(newState);
        }

        private void ReplaceDeadWorker()
        {
            List<KeyValuePair<int, WorkerInfo>> possibleReplacerWorkers = GetStandbyWorkers();
            if(possibleReplacerWorkers.Count > 0)
            {
                int randomIndex = _random.Next(possibleReplacerWorkers.Count);
                int randomWorkerId = possibleReplacerWorkers[randomIndex].Key;
                ChangeWorkerState(randomWorkerId, WorkerState.Active);
            }
        }
    }
}
