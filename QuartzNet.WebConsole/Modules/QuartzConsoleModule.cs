﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Helpers;
using Quartz;
using Quartz.Impl.Matchers;
using QuartzNet.WebConsole.VewModels.QuartzConsole;

#endregion

namespace QuartzNet.WebConsole.Modules
{
    public class UserZoneService
    {
        private readonly NancyModule _module;

        public UserZoneService(NancyModule module)
        {
            _module = module;
        }

        public TimeZoneInfo GetUserTimeZone()
        {
            if (!_module.Request.Cookies.ContainsKey("time-zone"))
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZone.CurrentTimeZone.StandardName);
            var timeZoneId = HttpUtility.UrlDecode(_module.Request.Cookies["time-zone"]);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZone.CurrentTimeZone.StandardName);
            }
        }

        public DateTimeOffset? ToUser(DateTimeOffset? dateTimeOffset)
        {
            if (!dateTimeOffset.HasValue)
                return null;
            return ToUser(dateTimeOffset.Value);
        }
        public DateTimeOffset ToUser(DateTimeOffset dateTimeOffset)
        {
            return TimeZoneInfo.ConvertTime(dateTimeOffset, GetUserTimeZone());
        }

        public DateTimeOffset FromUser(DateTime fromUnixTimestamp)
        {
            return TimeZoneInfo.ConvertTimeToUtc(fromUnixTimestamp, GetUserTimeZone());
        }
    }

    public sealed class QuartzConsoleModule : NancyModule
    {
        private readonly UserZoneService _timeZoneService;

        public QuartzConsoleModule()
            : base("/quartzconsole")
        {
            _timeZoneService = new UserZoneService(this);
            var schedFact = QuartzConsoleBootstrapper.Factory;

            After.AddItemToEndOfPipeline(ctx =>
                {
                    ViewBag.machineName = Environment.MachineName;
                    ViewBag.timezones = System.TimeZoneInfo.GetSystemTimeZones().Select(q => Tuple.Create(q.Id, q.DisplayName)).ToArray();
                    ViewBag.selectedZone = _timeZoneService.GetUserTimeZone().Id;
                });
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
                    var scheduler = schedFact.GetScheduler((string)p.schedKey);
                    JobKey key = null;
                    var keyStr = ((string)p.jobKey);
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
            Get["/schedule"] = p =>
                {
                    var jobs = (
                        from sched in schedFact.AllSchedulers
                        from jobGroup in sched.GetJobGroupNames()
                        from jobKey in sched.GetJobKeys(GroupMatcher<JobKey>.GroupContains(jobGroup))
                        select jobKey.ToString()
                        ).ToList();

                    return View["Schedule", new { jobs }];
                };
            Post["/ScheduleJson"] = p =>
                {
                    var start = _timeZoneService.FromUser(FromUnixTimestamp((long)Request.Form.start));
                    var end = _timeZoneService.FromUser(FromUnixTimestamp((long)Request.Form.end));
                    var activeJobKeys = ((string)Request.Form["activeJobs[]"] ?? "").Split(',');
                    var triggers = (
                                       from sched in schedFact.AllSchedulers
                                       from triggerGroup in sched.GetTriggerGroupNames()
                                       from triggerKey in sched.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupContains(triggerGroup))
                                       let trig = sched.GetTrigger(triggerKey)
                                       join ajk in activeJobKeys on trig.JobKey.ToString() equals ajk
                                       select trig
                                       ).ToList();
                    var lst = new List<object>();
                    lst.AddRange(triggers.SelectMany(trigger => GetAllTimes(trigger, start, end).Select(t => new
                    {
                        start = _timeZoneService.ToUser(t).ToString("yyyy-MM-dd hh:mm:ss"),
                        title = trigger.JobKey.ToString(),
                        allDay = false
                    })));

                    return Response.AsJson(lst);
                };

        }

        private IEnumerable<DateTimeOffset> GetAllTimes(ITrigger trigger, DateTimeOffset startTime, DateTimeOffset end)
        {
            var start = startTime;
            DateTimeOffset? time = start;
            while (true)
            {
                time = trigger.GetFireTimeAfter(time);
                if (!time.HasValue || time > end)
                    break;
                yield return time.Value;
            }

        }

        DateTime FromUnixTimestamp(long timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(timestamp);
        }



        private JobViewModel CreateJobInfo(IJobDetail job, IScheduler scheduler, ISet<JobKey> runningJobs)
        {
            var triggersOfJob = scheduler.GetTriggersOfJob(job.Key);
            var nextRun = _timeZoneService.ToUser(triggersOfJob.DefaultIfEmpty().Min(q => q.GetNextFireTimeUtc()));
            var lastRun = _timeZoneService.ToUser(triggersOfJob.DefaultIfEmpty().Max(q => q.GetPreviousFireTimeUtc()));
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