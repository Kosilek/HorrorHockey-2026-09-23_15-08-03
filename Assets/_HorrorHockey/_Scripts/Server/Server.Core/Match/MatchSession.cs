using ContractsServer.Data;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Server.Core.Match
{
    public class MatchSession
    {
        public MatchController Controller { get; }
        public Thread Thread { get; private set; }
        public CancellationTokenSource Cancellation { get; }

        public MatchSession(MatchController controller)
        {
            Controller = controller;
            Cancellation = new CancellationTokenSource();

            Thread = new Thread(LifeCycle)
            {
                IsBackground = true
            };



        }

        public void Start()
        {
            ///

            Thread.Start();
        }

        //public async void Stop()
        //{
        //    ///

        //    await StopTheard();
        //}

        public void Stop()
        {
            if (Cancellation.IsCancellationRequested)
                return;
            Controller.EndMatch();
            Cancellation.Cancel();
        }
        public void Join()
        {
            if (Thread.IsAlive)
                Thread.Join();
        }

        private Task StopTheard()
        {
            Cancellation.Cancel();
            Thread.Join();
            return Task.CompletedTask;
        }

        public void SetThread(Thread thread)
        {
            Thread = thread;
        }

        public void LifeCycle()
        {
            CancellationToken token = Cancellation.Token;
            int tickMs = Mathf.RoundToInt(GameConstants.DeltaTime * 1000f);

            while (!token.IsCancellationRequested)
            {
                Controller.Tick();
                token.WaitHandle.WaitOne(tickMs);
                // Логика симуляции матча
            }
        }
    }
}
