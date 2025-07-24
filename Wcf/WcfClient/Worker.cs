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

        private readonly System.Timers.Timer _timer;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public readonly Logger Logger;

        private const int HEARTBEAT_SENDING_INTERVAL = 5000;
        private const int WORKING_INTERVAL = 5000;

        public Worker(int workerId)
        {
            _workerId = workerId;
            Logger = new Logger();

            var instanceContext = new InstanceContext(this);
            var binding = new WSDualHttpBinding()
            {
                ClientBaseAddress = new Uri($"http://localhost:8081/ClientCallback/{workerId}")
            };
            var endpoint = new EndpointAddress("http://localhost:8080/Service/");

            _serviceClient = new ServiceClient(instanceContext, binding, endpoint);

            _timer = new System.Timers.Timer()
            {
                Interval = HEARTBEAT_SENDING_INTERVAL,
                AutoReset = true,
            };
            _timer.Elapsed += (sender, e) =>
            {
                _serviceClient.SendHeartbeat(_workerId);
            };
        }

        public void DoWork()
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    if(_state == WorkerState.Active)
                    {
                        // If stopped sending heartbeat
                        if(!_timer.Enabled)
                        {
                            while(!_timer.Enabled)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                Thread.Sleep(500);
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            Thread.Sleep(500);
                        }

                        Logger.Log($"[Worker {_workerId}] Working... {DateTime.UtcNow}");
                        Thread.Sleep(WORKING_INTERVAL);

                        cancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(500);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log($"[Worker {_workerId}] Received cancellation token. Worker is being stopped gracefully.");
            }
            finally
            {
                StopSendingHeartbeat();
                _timer.Dispose();
                _cancellationTokenSource.Dispose();
                Logger.Log($"[Worker {_workerId}] Worker is dead! Shutting down...");
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

        public void ShutdownWorker()
        {
            StopWork();
        }

        public void StartSendingHeartbeat()
        {
            Logger.Log($"[Worker {_workerId}] Started sending heartbeat signal.");
            _timer.Start();
        }

        public void StopSendingHeartbeat()
        {
            Logger.Log($"[Worker {_workerId}] Stopped sending heartbeat signal.");
            _timer.Stop();
        }

    }
}
