#region

using System;
using System.Threading;
using Nancy.Hosting.Self;
using Quartz;
using Quartz.Impl;
using QuartzNet.WebConsole;

#endregion

namespace QuartzNet.HostApplication
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var nancyHost = QuartzConsoleBootstrapper.StartDefault(GetFactory());
            
            Console.ReadLine();
            nancyHost.Stop();
        }

        private static ISchedulerFactory GetFactory()
        {
            ISchedulerFactory schedFact = new StdSchedulerFactory();

            var sched = schedFact.GetScheduler();
            sched.Start();

            for (var i = 1; i < 10; i++)
            {
                var job = JobBuilder.Create().OfType<HelloJob>().WithIdentity("myJob-" + i).Build();
                var trigger =
                    TriggerBuilder.Create()
                                  .ForJob(job)
                                  .WithDailyTimeIntervalSchedule(s => s.OnEveryDay().WithIntervalInMinutes(i*5))
                                  .Build();
                sched.ScheduleJob(job, trigger);
            }
            return schedFact;
        }
    }

    internal class HelloJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Hello!");
            Thread.Sleep(5000);
        }
    }
}