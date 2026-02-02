using GitHub.Runner.Worker;
using GitHub.Runner.Worker.Container;
using Xunit;
using Moq;
using GitHub.Runner.Worker.Container.ContainerHooks;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GitHub.DistributedTask.WebApi;
using System;

namespace GitHub.Runner.Common.Tests.Worker
{

    public sealed class ContainerOperationProviderL0
    {

        private TestHostContext _hc;
        private Mock<IExecutionContext> _ec;
        private Mock<IDockerCommandManager> _dockerManager;
        private Mock<IContainerHookManager> _containerHookManager;
        private ContainerOperationProvider containerOperationProvider;
        private Mock<IJobServerQueue> serverQueue;
        private Mock<IPagingLogger> pagingLogger;
        private List<string> healthyDockerStatus = new() { "healthy" };
        private List<string> emptyDockerStatus = new() { string.Empty };
        private List<string> unhealthyDockerStatus = new() { "unhealthy" };
        private List<string> dockerLogs = new() { "log1", "log2", "log3" };

        List<ContainerInfo> containers = new();

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_UnhealthyServiceContainer_AssertFailedTask()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(unhealthyDockerStatus));

            //Act
            try
            {
                await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);
            }
            catch (InvalidOperationException)
            {

                //Assert
                Assert.Equal(TaskResult.Failed, _ec.Object.Result ?? TaskResult.Failed);
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_UnhealthyServiceContainer_AssertExceptionThrown()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(unhealthyDockerStatus));

            //Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers));

        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_healthyServiceContainer_AssertSucceededTask()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(healthyDockerStatus));

            //Act
            await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);

            //Assert
            Assert.Equal(TaskResult.Succeeded, _ec.Object.Result ?? TaskResult.Succeeded);

        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_healthyServiceContainerWithoutHealthcheck_AssertSucceededTask()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(emptyDockerStatus));

            //Act
            await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);

            //Assert
            Assert.Equal(TaskResult.Succeeded, _ec.Object.Result ?? TaskResult.Succeeded);

        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void InitializeWithCorrectManager()
        {
            containers.Add(new ContainerInfo() { ContainerImage = "ubuntu:16.04" });
            _hc = new TestHostContext(this, "Test");
            _ec = new Mock<IExecutionContext>();
            serverQueue = new Mock<IJobServerQueue>();
            pagingLogger = new Mock<IPagingLogger>();

            containerOperationProvider = new ContainerOperationProvider();

            _hc.SetSingleton<IJobServerQueue>(serverQueue.Object);
            _hc.SetSingleton<IPagingLogger>(pagingLogger.Object);


            _ec.Setup(x => x.Global).Returns(new GlobalContext());

            Environment.SetEnvironmentVariable(Constants.Hooks.ContainerHooksPath, "/tmp/k8s/index.js");
            _dockerManager = new Mock<IDockerCommandManager>();
            _dockerManager.Setup(x => x.Initialize(_hc)).Throws(new Exception("Docker manager's Initialize should not be called"));

            _containerHookManager = new Mock<IContainerHookManager>();
            _hc.SetSingleton<IDockerCommandManager>(_dockerManager.Object);
            _hc.SetSingleton<IContainerHookManager>(_containerHookManager.Object);

            containerOperationProvider.Initialize(_hc);

            Environment.SetEnvironmentVariable(Constants.Hooks.ContainerHooksPath, null);
            _containerHookManager = new Mock<IContainerHookManager>();
            _containerHookManager.Setup(x => x.Initialize(_hc)).Throws(new Exception("Container hook manager's Initialize should not be called"));

            _dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(_dockerManager.Object);
            _hc.SetSingleton<IContainerHookManager>(_containerHookManager.Object);

            containerOperationProvider.Initialize(_hc);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void UpdateRegistryAuth_GhcrIo_HostedServer_SetsCredentials()
        {
            // Arrange
            _hc = new TestHostContext(this, "UpdateRegistryAuth_GhcrIo_HostedServer_SetsCredentials");
            _ec = new Mock<IExecutionContext>();

            var globalContext = new GlobalContext();
            _ec.Setup(x => x.Global).Returns(globalContext);
            _ec.Setup(x => x.GetGitHubContext("actor")).Returns("test-actor");
            _ec.Setup(x => x.GetGitHubContext("token")).Returns("test-token");

            var configStore = new Mock<IConfigurationStore>();
            var settings = new RunnerSettings
            {
                GitHubUrl = "https://github.com",
                IsHostedServer = true
            };
            configStore.Setup(x => x.GetSettings()).Returns(settings);
            _hc.SetSingleton<IConfigurationStore>(configStore.Object);

            var dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(dockerManager.Object);

            var jobContainer = new GitHub.DistributedTask.Pipelines.JobContainer()
            {
                Image = "ghcr.io/owner/image:latest"
            };
            var container = new ContainerInfo(_hc, jobContainer);

            // Act
            var method = typeof(ContainerOperationProvider).GetMethod("UpdateRegistryAuthForGitHubToken",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = new ContainerOperationProvider();
            provider.Initialize(_hc);
            method.Invoke(provider, new object[] { _ec.Object, container });

            // Assert
            Assert.Equal("test-actor", container.RegistryAuthUsername);
            Assert.Equal("test-token", container.RegistryAuthPassword);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void UpdateRegistryAuth_GhcrIo_GhesServer_DoesNotSetCredentials()
        {
            // Arrange
            _hc = new TestHostContext(this, "UpdateRegistryAuth_GhcrIo_GhesServer_DoesNotSetCredentials");
            _ec = new Mock<IExecutionContext>();

            var globalContext = new GlobalContext();
            _ec.Setup(x => x.Global).Returns(globalContext);
            _ec.Setup(x => x.GetGitHubContext("actor")).Returns("test-actor");
            _ec.Setup(x => x.GetGitHubContext("token")).Returns("test-token");

            var configStore = new Mock<IConfigurationStore>();
            var settings = new RunnerSettings
            {
                GitHubUrl = "https://ghes.company.com",
                IsHostedServer = false
            };
            configStore.Setup(x => x.GetSettings()).Returns(settings);
            _hc.SetSingleton<IConfigurationStore>(configStore.Object);

            var dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(dockerManager.Object);

            var jobContainer = new GitHub.DistributedTask.Pipelines.JobContainer()
            {
                Image = "ghcr.io/owner/image:latest"
            };
            var container = new ContainerInfo(_hc, jobContainer);

            // Act
            var method = typeof(ContainerOperationProvider).GetMethod("UpdateRegistryAuthForGitHubToken",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = new ContainerOperationProvider();
            provider.Initialize(_hc);
            method.Invoke(provider, new object[] { _ec.Object, container });

            // Assert - credentials should NOT be set for GHES
            Assert.Null(container.RegistryAuthUsername);
            Assert.Null(container.RegistryAuthPassword);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void UpdateRegistryAuth_GhcrIo_NullGitHubUrl_DoesNotSetCredentials()
        {
            // Arrange
            _hc = new TestHostContext(this, "UpdateRegistryAuth_GhcrIo_NullGitHubUrl_DoesNotSetCredentials");
            _ec = new Mock<IExecutionContext>();

            var globalContext = new GlobalContext();
            _ec.Setup(x => x.Global).Returns(globalContext);
            _ec.Setup(x => x.GetGitHubContext("actor")).Returns("test-actor");
            _ec.Setup(x => x.GetGitHubContext("token")).Returns("test-token");

            var configStore = new Mock<IConfigurationStore>();
            var settings = new RunnerSettings
            {
                GitHubUrl = null, // GitHubUrl not set
                IsHostedServer = true // But IsHostedServer might be true
            };
            configStore.Setup(x => x.GetSettings()).Returns(settings);
            _hc.SetSingleton<IConfigurationStore>(configStore.Object);

            var dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(dockerManager.Object);

            var jobContainer = new GitHub.DistributedTask.Pipelines.JobContainer()
            {
                Image = "ghcr.io/owner/image:latest"
            };
            var container = new ContainerInfo(_hc, jobContainer);

            // Act
            var method = typeof(ContainerOperationProvider).GetMethod("UpdateRegistryAuthForGitHubToken",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = new ContainerOperationProvider();
            provider.Initialize(_hc);
            method.Invoke(provider, new object[] { _ec.Object, container });

            // Assert - credentials should NOT be set when GitHubUrl is null
            Assert.Null(container.RegistryAuthUsername);
            Assert.Null(container.RegistryAuthPassword);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void UpdateRegistryAuth_DockerHub_HostedServer_DoesNotSetCredentials()
        {
            // Arrange
            _hc = new TestHostContext(this, "UpdateRegistryAuth_DockerHub_HostedServer_DoesNotSetCredentials");
            _ec = new Mock<IExecutionContext>();

            var globalContext = new GlobalContext();
            _ec.Setup(x => x.Global).Returns(globalContext);
            _ec.Setup(x => x.GetGitHubContext("actor")).Returns("test-actor");
            _ec.Setup(x => x.GetGitHubContext("token")).Returns("test-token");

            var configStore = new Mock<IConfigurationStore>();
            var settings = new RunnerSettings
            {
                GitHubUrl = "https://github.com",
                IsHostedServer = true
            };
            configStore.Setup(x => x.GetSettings()).Returns(settings);
            _hc.SetSingleton<IConfigurationStore>(configStore.Object);

            var dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(dockerManager.Object);

            var jobContainer = new GitHub.DistributedTask.Pipelines.JobContainer()
            {
                Image = "ubuntu:latest"
            };
            var container = new ContainerInfo(_hc, jobContainer);

            // Act
            var method = typeof(ContainerOperationProvider).GetMethod("UpdateRegistryAuthForGitHubToken",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = new ContainerOperationProvider();
            provider.Initialize(_hc);
            method.Invoke(provider, new object[] { _ec.Object, container });

            // Assert - credentials should NOT be set for Docker Hub images
            Assert.Null(container.RegistryAuthUsername);
            Assert.Null(container.RegistryAuthPassword);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void UpdateRegistryAuth_ContainersPkgGitHubCom_HostedServer_SetsCredentials()
        {
            // Arrange
            _hc = new TestHostContext(this, "UpdateRegistryAuth_ContainersPkgGitHubCom_HostedServer_SetsCredentials");
            _ec = new Mock<IExecutionContext>();

            var globalContext = new GlobalContext();
            _ec.Setup(x => x.Global).Returns(globalContext);
            _ec.Setup(x => x.GetGitHubContext("actor")).Returns("test-actor");
            _ec.Setup(x => x.GetGitHubContext("token")).Returns("test-token");

            var configStore = new Mock<IConfigurationStore>();
            var settings = new RunnerSettings
            {
                GitHubUrl = "https://github.com",
                IsHostedServer = true
            };
            configStore.Setup(x => x.GetSettings()).Returns(settings);
            _hc.SetSingleton<IConfigurationStore>(configStore.Object);

            var dockerManager = new Mock<IDockerCommandManager>();
            _hc.SetSingleton<IDockerCommandManager>(dockerManager.Object);

            var jobContainer = new GitHub.DistributedTask.Pipelines.JobContainer()
            {
                Image = "containers.pkg.github.com/owner/image:latest"
            };
            var container = new ContainerInfo(_hc, jobContainer);

            // Act
            var method = typeof(ContainerOperationProvider).GetMethod("UpdateRegistryAuthForGitHubToken",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = new ContainerOperationProvider();
            provider.Initialize(_hc);
            method.Invoke(provider, new object[] { _ec.Object, container });

            // Assert
            Assert.Equal("test-actor", container.RegistryAuthUsername);
            Assert.Equal("test-token", container.RegistryAuthPassword);
        }

        private void Setup([CallerMemberName] string testName = "")
        {
            containers.Add(new ContainerInfo() { ContainerImage = "ubuntu:16.04" });
            _hc = new TestHostContext(this, testName);
            _ec = new Mock<IExecutionContext>();
            serverQueue = new Mock<IJobServerQueue>();
            pagingLogger = new Mock<IPagingLogger>();

            _dockerManager = new Mock<IDockerCommandManager>();
            _containerHookManager = new Mock<IContainerHookManager>();
            containerOperationProvider = new ContainerOperationProvider();

            _hc.SetSingleton<IJobServerQueue>(serverQueue.Object);
            _hc.SetSingleton<IPagingLogger>(pagingLogger.Object);

            _hc.SetSingleton<IDockerCommandManager>(_dockerManager.Object);
            _hc.SetSingleton<IContainerHookManager>(_containerHookManager.Object);

            _ec.Setup(x => x.Global).Returns(new GlobalContext());

            containerOperationProvider.Initialize(_hc);
        }
    }
}
