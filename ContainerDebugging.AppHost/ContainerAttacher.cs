#if RUN_IN_CONTAINER
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class ContainerHelpers
{
    private const string AttachLabel = "attach";
    private const string TrustLabel = "trust";
    public static IResourceBuilder<ContainerResource> AddDebuggableContainer(this IDistributedApplicationBuilder builder, [ResourceName] string name,
        string projectName, string image, string tag = "latest", int? targetHttpPort = 8080, int? targetHttpsPort = 8081)
    {
        var containerResource = builder.AddContainer(name, image)
            .WithImageTag(tag)
            .WithOtlpExporter()
            .WithGeneratedCertificateTrust();

        // If an http port is specified, add it to the container resource
        if (targetHttpPort != null)
        {
            containerResource.WithHttpEndpoint(targetPort: targetHttpPort.Value)
                             .WithEnvironment("ASPNETCORE_HTTP_PORTS", targetHttpPort.Value.ToString());
        }

        // If an https port is specified, add it and a certificate to the container resource
        if (targetHttpsPort != null)
        {
            containerResource.WithHttpsEndpoint(targetPort: targetHttpsPort.Value)
                             .WithEnvironment("ASPNETCORE_HTTPS_PORTS", targetHttpsPort.Value.ToString())
                             .WithEnvironment("ASPNETCORE_HTTPS_PORT", () => containerResource.GetEndpoint("https").Port.ToString())
                             .WithGeneratedCertificate();
        }

        // If the host is being debugged, queue a thread to attach to the container
        if (Debugger.IsAttached)
        {
            // Add the debugger to the container, and a label to identify it
            string attachLabelValue = Guid.NewGuid().ToString("N");
            containerResource
              .WithBindMount(Path.Combine(builder.AppHostDirectory, "vsdbg"), "/remote_debugger/", true)
              .WithContainerRuntimeArgs("--label", $"{AttachLabel}={attachLabelValue}")
              .WithEnvironment("WAIT_FOR_DEBUGGING", "true");

            StartAttachingToContainer(projectName, attachLabelValue);
        }
        return containerResource;
    }

    public static IResourceBuilder<ContainerResource> WithGeneratedCertificate(this IResourceBuilder<ContainerResource> containerResource)
    {
        (_, string password) = EnsureGeneratedCertificate(containerResource.ApplicationBuilder);

        containerResource
            .WithBindMount(Path.Combine(containerResource.ApplicationBuilder.AppHostDirectory, "Certs"), "/Certs/", true)
            .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/Certs/localhost.pfx")
            .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", password);
        return containerResource;
    }

    public static IResourceBuilder<ContainerResource> WithGeneratedCertificateTrust(this IResourceBuilder<ContainerResource> containerResource)
    {
        // Add a label so we can find the container once it is running
        string trustLabelValue = Guid.NewGuid().ToString("N");
        containerResource
          .WithContainerRuntimeArgs("--label", $"{TrustLabel}={trustLabelValue}")
          .WithEnvironment("WAIT_FOR_CERT", "true");

        StartWatchingConainerForTrust(trustLabelValue);

        return containerResource;
    }

    public static (string path, string password) EnsureGeneratedCertificate(IDistributedApplicationBuilder builder)
    {
        string certsDir = Path.Combine(builder.AppHostDirectory, "Certs");

        if (!File.Exists(Path.Combine(builder.AppHostDirectory, "Certs", "localhost.pfx")))
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy unrestricted -File \"" + Path.Combine(certsDir, "Create-Certs.ps1") + '"',
            };
            Process certGenerator = Process.Start(processStartInfo);
            certGenerator.WaitForExit();
        }

        return (Path.Combine(certsDir, "localhost.pfx"), File.ReadAllText(Path.Combine(certsDir, "Password.txt")));
    }

    public static void DownloadVsdbg(string vsdbgPath)
    {
        // From https://github.com/Microsoft/MIEngine/wiki/Offroad-Debugging-of-.NET-Core-on-Linux---OSX-from-Visual-Studio
        string command = "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -useb 'https://aka.ms/getvsdbgps1')))  -Version vs2019 -RuntimeID linux-x64 -InstallPath \"" + vsdbgPath + '"';
        // Usign an encoded command to avoid issues with quotes in the command
        string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -ExecutionPolicy unrestricted -EncodedCommand " + encodedCommand,
        };
        Process vsdbgGetter = Process.Start(processStartInfo);
        vsdbgGetter.WaitForExit();
    }

    private static void StartWatchingConainerForTrust(string trustLabelValue)
    {
        ThreadPool.QueueUserWorkItem((_) =>
        {
            string containerId = GetContainerId(TrustLabel, trustLabelValue);
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec -u root {containerId} /bin/bash /Certs/TrustCerts.sh",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process docker = Process.Start(processStartInfo);
            docker.WaitForExit();
        });
    }

    private static Thread StartAttachingToContainer(string projectName, string attachLabelValue)
    {
        if (OperatingSystem.IsWindows())
        {
            ThreadStart attacher = () => AttachToContainer(projectName, attachLabelValue);
            Thread attacherThread = new Thread(attacher);

            attacherThread.SetApartmentState(ApartmentState.STA);
            attacherThread.Start();
            return attacherThread;
        }
        throw new NotSupportedException("Attaching to containers is only supported on Windows.");
    }

    private static void AttachToContainer(string projectName, string attachLabelValue)
    {
        string containerId = GetContainerId(AttachLabel, attachLabelValue);
        // Register the message filter
        MessageFilter.Register();
        try
        {
            var dte = GetDte(projectName);

            string config = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");

            // See https://github.com/Microsoft/MIEngine/wiki/Offroad-Debugging-of-.NET-Core-on-Linux---OSX-from-Visual-Studio#attaching
            File.WriteAllText(config, $$"""
            {
                "version": "0.2.0",
                "adapter": "docker.exe",
                "adapterArgs": "exec -i {{containerId}} /remote_debugger/vsdbg --interpreter=vscode",
                "configurations": [
                {
                    "name": ".NET Core Docker Attach",
                    "type": "coreclr",
                    "request": "attach",
                    // Just assuming the target process is the entry point and has pid 1
                    "processId": 1
                }
                ]
            }
            """);

            dte.ExecuteCommand("DebugAdapterHost.Launch", $"/LaunchJson:\"{config}\" /EngineGuid:541B8A8A-6081-4506-9F0A-1CE771DEBC04");
        }
        finally
        {
            // Revoke the message filter
            MessageFilter.Revoke();
        }

    }

    private static EnvDTE.DTE GetDte(string projectName)
    {
        var procs = GetVSProcessIds();

        foreach (var pid in procs)
        {
            var dte = GetDte(pid);
            if (dte.Solution == null || dte.Solution.Projects == null || dte.Solution.Projects.Count <= 0)
            {
                continue;
            }

            if (dte.Solution.Projects.Cast<EnvDTE.Project>().Any(p => p.Name == projectName))
            {
                return dte;
            }
        }

        return null;
    }

    private static int[] GetVSProcessIds()
    {
        List<int> processIds = new List<int>();
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("devenv"))
        {
            processIds.Add(process.Id);
        }
        return processIds.ToArray();
    }

    private static EnvDTE.DTE GetDte(int pid)
    {
        IntPtr numFetched = IntPtr.Zero;
        IRunningObjectTable runningObjectTable;
        IEnumMoniker monikerEnumerator;
        IMoniker[] monikers = new IMoniker[1];

        //get Running object table, make a pinvoke call
        NativeMethods.ThrowOnFailure(NativeMethods.GetRunningObjectTable(0, out runningObjectTable));

        //get an enumerator for entries
        runningObjectTable.EnumRunning(out monikerEnumerator);
        monikerEnumerator.Reset();

        string runningObjectNameExpected = "!VisualStudio.DTE.17.0:" + pid;

        while (monikerEnumerator.Next(1, monikers, numFetched) == 0)
        {
            IBindCtx ctx;
            NativeMethods.ThrowOnFailure(NativeMethods.CreateBindCtx(0, out ctx));

            string runningObjectName;
            monikers[0].GetDisplayName(ctx, null, out runningObjectName);

            if (runningObjectName.Equals(runningObjectNameExpected, StringComparison.OrdinalIgnoreCase))
            {
                object runningObjectVal;
                runningObjectTable.GetObject(monikers[0], out runningObjectVal);
                return (EnvDTE.DTE)runningObjectVal;
            }
        }

        return null;
    }

    private static string GetContainerId(string labelname, string labelValue)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"ps -q --filter \"label={labelname}={labelValue}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        while (true)
        {
            Process docker = Process.Start(processStartInfo);
            docker.WaitForExit();
            string[] outputLines = docker.StandardOutput.ReadToEnd()?.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            if (docker.ExitCode != 0 || outputLines.Length == 0)
            {
                System.Threading.Thread.Sleep(100);
                continue;
            }

            return outputLines.First().Trim();
        }
    }


    // Taken from: https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/ms228772(v=vs.100)?redirectedfrom=MSDN#example
    private class MessageFilter : IOleMessageFilter
    {
        //
        // Class containing the IOleMessageFilter
        // thread error-handling functions.

        // Start the filter.
        public static void Register()
        {
            IOleMessageFilter newFilter = new MessageFilter();
            IOleMessageFilter oldFilter = null;
            CoRegisterMessageFilter(newFilter, out oldFilter);
        }

        // Done with the filter, close it.
        public static void Revoke()
        {
            IOleMessageFilter oldFilter = null;
            CoRegisterMessageFilter(null, out oldFilter);
        }

        //
        // IOleMessageFilter functions.
        // Handle incoming thread requests.
        int IOleMessageFilter.HandleInComingCall(int dwCallType,
            System.IntPtr hTaskCaller, int dwTickCount, System.IntPtr
            lpInterfaceInfo)
        {
            //Return the flag SERVERCALL_ISHANDLED.
            return 0;
        }

        // Thread call was rejected, so try again.
        int IOleMessageFilter.RetryRejectedCall(System.IntPtr
            hTaskCallee, int dwTickCount, int dwRejectType)
        {
            if (dwRejectType == 2)
            // flag = SERVERCALL_RETRYLATER.
            {
                // Retry the thread call immediately if return >=0 & 
                // <100.
                return 99;
            }
            // Too busy; cancel call.
            return -1;
        }

        int IOleMessageFilter.MessagePending(System.IntPtr hTaskCallee,
            int dwTickCount, int dwPendingType)
        {
            //Return the flag PENDINGMSG_WAITDEFPROCESS.
            return 2;
        }

        // Implement the IOleMessageFilter interface.
        [DllImport("Ole32.dll")]
        private static extern int
            CoRegisterMessageFilter(IOleMessageFilter newFilter, out
            IOleMessageFilter oldFilter);
    }

    // Definition of the IMessageFilter interface which we need to implement and 
    // register with the CoRegisterMessageFilter API.
    [ComImport(), Guid("00000016-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleMessageFilter     // Renamed to avoid confusion w/ System.Windows.Forms.IMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    };

    private class NativeMethods
    {
        [DllImport("ole32.dll")]
        public static extern int CoRegisterMessageFilter(
            IOleMessageFilter newFilter,
            out IOleMessageFilter oldFilter);

        [DllImport("ole32.dll")]
        public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        public const int SERVERCALL_ISHANDLED = 0;
        public const int SERVERCALL_REJECTED = 1;
        public const int SERVERCALL_RETRYLATER = 2;
        public const int PENDINGMSG_WAITDEFPROCESS = 2;

        public static void ThrowOnFailure(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
    };
}
#endif