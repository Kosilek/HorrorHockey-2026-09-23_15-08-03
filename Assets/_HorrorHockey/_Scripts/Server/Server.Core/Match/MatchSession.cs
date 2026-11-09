using ContractsServer.Data;
using System.Diagnostics;
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
            double tickMs = GameConstants.DeltaTime * 1000.0;
            // Тик считает ровно DeltaTime, поэтому тики должны идти по часам, а не "тик + пауза".
            // Иначе сервер отстаёт от реального времени, клиент уезжает вперёд и потом откатывается к серверной позиции.
            Stopwatch clock = Stopwatch.StartNew();
            double nextTickMs = 0.0;

            while (!token.IsCancellationRequested)
            {
                Controller.Tick();
                // Логика симуляции матча

                nextTickMs += tickMs;
                int waitMs = (int)(nextTickMs - clock.Elapsed.TotalMilliseconds);
                if (waitMs > 0)
                    token.WaitHandle.WaitOne(waitMs);
            }
        }
    }
}
