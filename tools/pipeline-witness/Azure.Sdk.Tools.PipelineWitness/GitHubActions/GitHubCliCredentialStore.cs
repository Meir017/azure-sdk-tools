using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class GitHubCliCredentialStore : ICredentialStore
{
    private readonly bool isChainedCredential;
    private readonly string hostname;
    private readonly string account;
    private readonly TimeSpan processTimeout;

    public GitHubCliCredentialStore(string hostname = default, string account = default, TimeSpan? processTimeout = default, bool isChainedCredential = false)
    {
        this.processTimeout = processTimeout ?? TimeSpan.FromSeconds(13);
        this.isChainedCredential = isChainedCredential;
        this.hostname = hostname;
        this.account = account;
    }

    public async Task<Credentials> GetCredentials()
    {
        Process process = new()
        {
            StartInfo = GetAzureCliProcessStartInfo(),
            EnableRaisingEvents = true
        };

        using ProcessRunner processRunner = new(process, this.processTimeout, CancellationToken.None);

        string output;
        //		try
        //		{
        output = await processRunner.RunAsync().ConfigureAwait(false);
        //		}
        //		catch (OperationCanceledException)
        //		{
        //			if (_isChainedCredential)
        //			{
        //				throw new CredentialUnavailableException(AzureCliTimeoutError);
        //			}
        //			else
        //			{
        //				throw new AuthenticationFailedException(AzureCliTimeoutError);
        //			}
        //		}
        //		catch (InvalidOperationException exception)
        //		{
        //			bool isWinError = exception.Message.StartsWith(WinAzureCLIError, StringComparison.CurrentCultureIgnoreCase);
        //
        //			bool isOtherOsError = AzNotFoundPattern.IsMatch(exception.Message);
        //
        //			if (isWinError || isOtherOsError)
        //			{
        //				throw new CredentialUnavailableException(AzureCLINotInstalled);
        //			}
        //
        //			bool isAADSTSError = exception.Message.Contains("AADSTS");
        //			bool isLoginError = exception.Message.IndexOf("az login", StringComparison.OrdinalIgnoreCase) != -1 ||
        //								exception.Message.IndexOf("az account set", StringComparison.OrdinalIgnoreCase) != -1;
        //
        //			if (isLoginError && !isAADSTSError)
        //			{
        //				throw new CredentialUnavailableException(AzNotLogIn);
        //			}
        //
        //			bool isRefreshTokenFailedError = exception.Message.IndexOf(AzureCliFailedError, StringComparison.OrdinalIgnoreCase) != -1 &&
        //											 exception.Message.IndexOf(RefreshTokeExpired, StringComparison.OrdinalIgnoreCase) != -1 ||
        //											 exception.Message.IndexOf("CLIInternalError", StringComparison.OrdinalIgnoreCase) != -1;
        //
        //			if (isRefreshTokenFailedError)
        //			{
        //				throw new CredentialUnavailableException(InteractiveLoginRequired);
        //			}
        //
        //			if (_isChainedCredential)
        //			{
        //				throw new CredentialUnavailableException($"{AzureCliFailedError} {Troubleshoot} {exception.Message}");
        //			}
        //			else
        //			{
        //				throw new AuthenticationFailedException($"{AzureCliFailedError} {Troubleshoot} {exception.Message}");
        //			}
        //		}

        return new Credentials(output, AuthenticationType.Bearer);
    }

    private ProcessStartInfo GetAzureCliProcessStartInfo()
    {
        string environmentPath = Environment.GetEnvironmentVariable("PATH");

        string command = "gh auth token";

        if (!string.IsNullOrEmpty(this.hostname))
        {
            command += $" --hostname {this.hostname}";
        }

        if (!string.IsNullOrEmpty(this.account))
        {
            command += $" --user {this.account}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            string programFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string defaultPath = $"{programFilesx86}\\GitHub CLI;{programFiles}\\GitHub CLI";

            return new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = $"/d /c \"{command}\"",
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment = { { "PATH", !string.IsNullOrEmpty(environmentPath) ? environmentPath : defaultPath } }
            };
        }
        else
        {
            string defaultPath = "/usr/bin:/usr/local/bin";

            return new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{command}\"",
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                WorkingDirectory = "/bin/",
                Environment = { { "PATH", !string.IsNullOrEmpty(environmentPath) ? environmentPath : defaultPath } }
            };
        }
    }
}
