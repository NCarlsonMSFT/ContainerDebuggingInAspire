#if RUN_IN_CONTAINER
using System.Diagnostics;

public static class ContainerHelpers
{
    private const string TrustLabel = "trust";

    public static IResourceBuilder<ProjectResource> WithDebuggableContainer(this IResourceBuilder<ProjectResource> builder, int? hostHttpPort = null, int? hostHttpsPort = null)
    {
        builder.WithGeneratedCertificateTrust()
               .WithEnvironment("ASPNETCORE_URLS", string.Empty)
               .WithEnvironment("APPDATA", string.Empty);

        // If an http port is specified, add it to the container resource
        if (hostHttpPort != null)
        {
            builder.WithHttpEndpoint(targetPort: hostHttpPort, isProxied: false);
        }

        // If an https port is specified, add it and a certificate to the container resource
        if (hostHttpsPort != null)
        {
            builder.WithHttpsEndpoint(targetPort: hostHttpsPort, isProxied: false)
                   .WithEnvironment("ASPNETCORE_HTTPS_PORT", hostHttpsPort.ToString())
                   .WithGeneratedCertificate();
        }

        return builder;
    }

    public static IResourceBuilder<ProjectResource> WithGeneratedCertificate(this IResourceBuilder<ProjectResource> containerResource)
    {
        (_, string password) = EnsureGeneratedCertificate(containerResource.ApplicationBuilder);

        containerResource
            .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/Certs/localhost.pfx")
            .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", password);
        return containerResource;
    }

    public static IResourceBuilder<ProjectResource> WithGeneratedCertificateTrust(this IResourceBuilder<ProjectResource> containerResource)
    {
        containerResource
          .WithEnvironment("WAIT_FOR_CERT", "true");

        StartWatchingContainerForTrust(containerResource.Resource.Name);

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

    private static void StartWatchingContainerForTrust(string trustLabelValue)
    {
        ThreadPool.QueueUserWorkItem((_) =>
        {
            string containerId = GetContainerId(TrustLabel, trustLabelValue);
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec -u root {containerId} /bin/bash /Certs/TrustCerts.sh"
            };
            Process docker = Process.Start(processStartInfo);
            docker.WaitForExit();
        });
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
}
#endif