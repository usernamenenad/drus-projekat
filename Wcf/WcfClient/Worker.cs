using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using WcfClient.ServiceReference;

namespace WcfClient
{
    internal class Worker : IServiceCallback
    {
        private readonly ServiceClient _serviceClient;
        private readonly int _workerId;
        private WorkerState _state = WorkerState.Standby;

        public readonly System.Timers.Timer Timer;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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

            Timer = new System.Timers.Timer(5000);
            Timer.Elapsed += (sender, e) =>
            {
                _serviceClient.SendHeartbeat(_workerId);
            };
            Timer.AutoReset = true;
            Timer.Start();
        }

        public void DoWork()
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            try
            {
                while(!cancellationToken.IsCancellationRequested && _state != WorkerState.Dead)
                {
                    if(_state == WorkerState.Active)
                    {
                        Console.WriteLine($"[Worker {_workerId}] Working... {DateTime.UtcNow}");
                        Thread.Sleep(5000);

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Worker {_workerId}] Received cancellation token. Worker is being stopped gracefully.");
            }
            finally
            {
                Timer.Stop();
                Timer.Dispose();
                _serviceClient.Close();
                _cancellationTokenSource.Dispose();
                Console.WriteLine($"[Worker {_workerId}] dead! Shutting down...");
            }
        }

        public void StopWork()
        {
            _cancellationTokenSource.Cancel();
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
