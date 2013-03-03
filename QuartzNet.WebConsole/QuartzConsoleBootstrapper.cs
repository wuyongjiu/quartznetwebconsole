#region

using System;
using System.Configuration;
using System.Reflection;
using Common.Logging;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Hosting.Self;
using Nancy.Json;
using Nancy.TinyIoc;
using Nancy.ViewEngines;
using Nancy.ViewEngines.Razor;
using Quartz;
using QuartzNet.WebConsole.Modules;
using QuartzNet.WebConsole.Views;

#endregion

namespace QuartzNet.WebConsole
{
    public class QuartzConsoleBootstrapper : DefaultNancyBootstrapper
    {
        private static readonly ILog Log = LogManager.GetLogger<QuartzConsoleBootstrapper>();
        public static NancyHost StartDefault(ISchedulerFactory factory)
        {
            Factory = factory;
            var hostUrl = new Uri(ConfigurationManager.AppSettings["quartznet.webconsole.host"] ?? "http://localhost:1234");
            var defaultNancyBootstrapper = new QuartzConsoleBootstrapper();
            var nancyHost = new NancyHost(hostUrl, defaultNancyBootstrapper);
            nancyHost.Start();
            Log.InfoFormat("QuartzNet Console succesfully started on {0}", new Uri(hostUrl, "quartzconsole"));
            return nancyHost;
        }

        public static ISchedulerFactory Factory;


        private static readonly Assembly QuartzAssembly;

        static QuartzConsoleBootstrapper()
        {
            QuartzAssembly = typeof (QuartzConsoleModule).Assembly;
            ResourceViewLocationProvider
                .Ignore.Add(typeof (RazorViewEngine).Assembly);
            ResourceViewLocationProvider
                .RootNamespaces
                .Add(QuartzAssembly, typeof (ViewsPointer).Namespace);

            JsonSettings.MaxJsonLength = int.MaxValue;
        }

        protected override NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                var configuration = NancyInternalConfiguration.Default;
                configuration.ViewLocationProvider = typeof (ResourceViewLocationProvider);
                return configuration;
            }
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            container.Register<IRazorConfiguration>(new QuartzRazorConfiguration());
            container.Register<RazorViewEngine>();

            base.ConfigureApplicationContainer(container);
        }

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            base.ConfigureConventions(nancyConventions);
            nancyConventions.StaticContentsConventions.Add(
                EmbeddedStaticContentConventionBuilder.AddDirectory("/Content", QuartzAssembly));
        }
    }
}