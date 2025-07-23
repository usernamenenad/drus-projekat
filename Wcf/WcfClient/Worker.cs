using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

using WcfClient.ServiceReference;

namespace WcfClient
{
    internal class Worker : IServiceCallback
    {
        private WorkerState _state = WorkerState.Standby;
        private readonly ServiceClient _serviceClient;

        private readonly int _workerId;

        private readonly System.Timers.Timer _timer;

        public Worker(int workerId)
        {
            _workerId = workerId;

            var instanceContext = new InstanceContext(this);
            var binding = new WSDualHttpBinding()
            {
                ClientBaseAddress = new Uri($"http://localhost:8081/ClientCallback/{workerId}")
            };
            var endpoint = new EndpointAddress("http://localhost:8080/Service/");

            _serviceClient = new ServiceClient(instanceContext, binding, endpoint);

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += (sender, e) =>
            {
                _serviceClient.SendHeartbeat(_workerId);
            };
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void DoWork()
        {
            while(_state != WorkerState.Dead)
            {
                if(_state == WorkerState.Active)
                {
                    Console.WriteLine($"[Worker {_workerId}] Working... {DateTime.UtcNow}");
                    System.Threading.Thread.Sleep(5000);
                }
            }

            Console.WriteLine($"[Worker {_workerId}] dead! Shutting down...");
        }

        public void StopWorking()
        {
            _timer.Stop();
        }

        public Message Register()
        {
            return _serviceClient.Register(_workerId);
        }

        public void ChangeWorkerState(WorkerState newState)
        {
            _state = newState;
        }
    }
}
