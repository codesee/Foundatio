﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Tests.Utility;
using Xunit;

namespace Foundatio.Tests.Jobs {
    public class JobTests {
        [Fact]
        public void CanRunJobs() {
            var job = new HelloWorldJob();
            job.Run();
            Assert.Equal(1, job.RunCount);

            job.RunContinuous(iterationLimit: 2);
            Assert.Equal(3, job.RunCount);

            job.RunContinuous(token: new CancellationTokenSource(TimeSpan.FromMilliseconds(10)).Token);
            Assert.True(job.RunCount > 10);
        }

        [Fact]
        public void CanBootstrapJobs() {
            var serviceProvider = JobRunner.GetServiceProvider(typeof(JobTests));
            Assert.NotNull(serviceProvider);
            Assert.Equal(serviceProvider.GetType(), typeof(MyBootstrappedServiceProvider));

            serviceProvider = JobRunner.GetServiceProvider(typeof(MyBootstrappedServiceProvider));
            Assert.NotNull(serviceProvider);
            Assert.Equal(serviceProvider.GetType(), typeof(MyBootstrappedServiceProvider));

            var job = serviceProvider.GetService<WithDependencyJob>();
            Assert.NotNull(job);
            Assert.NotNull(job.Dependency);
            Assert.Equal(5, job.Dependency.MyProperty);

            var jobInstance = JobRunner.CreateJobInstance("Foundatio.Tests.Jobs.HelloWorldJob,Foundatio.Tests");
            Assert.NotNull(job);
            Assert.NotNull(job.Dependency);
            Assert.Equal(5, job.Dependency.MyProperty);

            jobInstance = JobRunner.CreateJobInstance("Foundatio.Tests.Jobs.HelloWorldJob,Foundatio.Tests", "Foundatio.Tests.Jobs.MyBootstrappedServiceProvider,Foundatio.Tests");
            Assert.NotNull(job);
            Assert.NotNull(job.Dependency);
            Assert.Equal(5, job.Dependency.MyProperty);

            int result = JobRunner.RunJob(jobInstance);
            Assert.Equal(0, result);
            Assert.True(jobInstance is HelloWorldJob);
        }

        [Fact]
        public void CanRunQueueJob() {
            const int workItemCount = 1000;
            var metrics = new InMemoryMetricsClient();
            var countdown = new CountDownLatch(workItemCount);
            var queue = new InMemoryQueue<SampleQueueWorkItem>(0, TimeSpan.Zero, metrics: metrics);

            for (int i = 0; i < workItemCount; i++)
                queue.Enqueue(new SampleQueueWorkItem { Created = DateTime.Now, Path = "somepath" + i });

            var job = new SampleQueueJob(queue, metrics, countdown);
            var tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => job.RunContinuousAsync(token: tokenSource.Token), tokenSource.Token);
            bool success = countdown.Wait(3 * 60 * 1000);
            metrics.DisplayStats();

            Assert.Equal(0, queue.GetQueueCount());
        }
    }
}