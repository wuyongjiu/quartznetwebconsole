#region

using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using Quartz;
using Quartz.Impl.Matchers;
using QuartzNet.WebConsole.VewModels.QuartzConsole;

#endregion

namespace QuartzNet.WebConsole.Modules
{
    public class QuartzConsoleModule : NancyModule
    {
        public QuartzConsoleModule()
            : base("/quartzconsole")
        {
            var schedFact = QuartzConsoleBootstrapper.Factory;


            Get[""] = (p) =>
                {
                    var m = new JobListViewModel
                        {
                            Groups = new List<JobGroupViewModel>(),
                            Machine = Environment.MachineName
                        };
                    foreach (var scheduler in schedFact.AllSchedulers)
                    {
                        var runningJobs =
                            new HashSet<JobKey>(scheduler.GetCurrentlyExecutingJobs().Select(q => q.JobDetail.Key));
                        foreach (var jobGroupName in scheduler.GetJobGroupNames())
                        {
                            var group = new JobGroupViewModel();
                            group.JobDetails = scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(jobGroupName))
                                                        .Select(scheduler.GetJobDetail)
                                                        .Select(d => CreateJobInfo(d, scheduler, runningJobs))
                                                        .ToList();
                            m.Groups.Add(group);
                        }
                    }
                    return View["Index", m];
                };
            Post["/trigger/{schedKey}/{jobKey}"] = p =>
                {
                    var scheduler = schedFact.GetScheduler((string) p.schedKey);
                    JobKey key = null;
                    var keyStr = ((string) p.jobKey);
                    if (keyStr.Contains("."))
                    {
                        var keyArr = keyStr.Split('.');
                        key = new JobKey(keyArr[1], keyArr[0]);
                    }
                    else
                    {
                        key = new JobKey(keyStr);
                    }

                    scheduler.TriggerJob(key);
                    return Response.AsRedirect("/quartzconsole");
                };
        }

        private JobViewModel CreateJobInfo(IJobDetail job, IScheduler scheduler, ISet<JobKey> runningJobs)
        {
            var triggersOfJob = scheduler.GetTriggersOfJob(job.Key);
            var nextRun = triggersOfJob.DefaultIfEmpty().Min(q => q.GetNextFireTimeUtc());
            var lastRun = triggersOfJob.DefaultIfEmpty().Max(q => q.GetPreviousFireTimeUtc());
            return new JobViewModel
                {
                    SchedulerName = scheduler.SchedulerName,
                    JobKey = job.Key,
                    NextScheduledRun = nextRun,
                    LastRun = lastRun,
                    IsRunning = runningJobs.Contains(job.Key),
                    Triggers = CreateTriggerInfo(triggersOfJob, scheduler)
                };
        }

        private List<JobTriggerViewModel> CreateTriggerInfo(IEnumerable<ITrigger> triggersOfJob, IScheduler scheduler)
        {
            return triggersOfJob.Select(q => new JobTriggerViewModel
                {
                    Status = scheduler.GetTriggerState(q.Key).ToString()
                }).ToList();
        }
    }
}