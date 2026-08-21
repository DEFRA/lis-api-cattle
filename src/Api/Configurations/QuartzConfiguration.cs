using Lis.Cattle.Jobs;
using Lis.Cattle.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Lis.Cattle.Configurations;

public static class QuartzConfiguration
{
    public static IServiceCollection AddQuartzServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jobOptions = configuration.GetSection(CtsPollingJobOptions.SectionName).Get<CtsPollingJobOptions>()
                         ?? new CtsPollingJobOptions();

        services.Configure<CtsPollingJobOptions>(configuration.GetSection(CtsPollingJobOptions.SectionName));

        services.AddQuartz(q =>
        {
            if (jobOptions.Enabled)
            {
                var jobKey = new JobKey(nameof(CtsBundlePollingJob));
                q.AddJob<CtsBundlePollingJob>(opts => opts.WithIdentity(jobKey));

                q.AddTrigger(opts =>
                {
                    opts.ForJob(jobKey)
                        .WithIdentity($"{nameof(CtsBundlePollingJob)}-trigger");

                    if (!string.IsNullOrWhiteSpace(jobOptions.CronSchedule))
                    {
                        opts.WithCronSchedule(jobOptions.CronSchedule);
                    }
                    else
                    {
                        var interval = jobOptions.PollingIntervalSeconds > 0 ? jobOptions.PollingIntervalSeconds : 30;
                        opts.WithSimpleSchedule(x => x.WithIntervalInSeconds(interval).RepeatForever());
                    }
                });
            }
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
