namespace Prereqqer.Core.Definitions;

public static class DefinitionFactory
{
    public static ConditionDefinitionDocument CreateDefaultDocument() =>
        new()
        {
            Branding = new BrandingDefinition(),
            Groups =
            [
                CreateMacOsSystemGroup(),
                new ConditionGroupDefinition
                {
                    Name = "Local machine",
                    Description = "Starter checks for validating the current host.",
                    Conditions =
                    [
                        CreateDefaultPowerShellCondition(),
                        CreateDefaultWmiCondition(),
                        CreateWindowsServerVersionCondition(),
                        CreateSqlServerVersionCondition(),
                        CreateDnsStatusCondition(),
                        CreateIisInstallationAndFunctionCondition(),
                        CreateApiJsonPropertyCondition()
                    ]
                },
                CreateRuntimeDependenciesGroup(),
                CreateSecurityPostureGroup(),
                CreateNetworkGroup(),
                CreateDeveloperToolingGroup()
            ]
        };

    public static ConditionDefinition CreateDefaultPowerShellCondition() =>
        new()
        {
            Name = "PowerShell runtime",
            Description = "Confirms the embedded PowerShell runtime can execute scripts.",
            RecommendedAction = "Review the embedded PowerShell runtime and script output before continuing.",
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 15,
            PowerShell = new PowerShellCheckDefinition(),
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "No script output",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "0"
                },
                new OutcomeRuleDefinition
                {
                    Name = "PowerShell 7 or newer",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.FirstRow,
                    Field = "Major",
                    Operator = RuleOperator.GreaterThanOrEqual,
                    ExpectedValue = "7",
                    RecommendedAction = string.Empty
                },
                new OutcomeRuleDefinition
                {
                    Name = "Older PowerShell runtime",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "Major",
                    Operator = RuleOperator.LessThan,
                    ExpectedValue = "7",
                    RecommendedAction = "Install or bundle PowerShell 7 or newer."
                }
            ]
        };

    public static ConditionDefinition CreateDefaultWmiCondition() =>
        new()
        {
            Name = "Windows operating system inventory",
            Description = "Reads the operating system caption and version through WMI on Windows.",
            RecommendedAction = "Run this check on Windows, or disable the WMI condition for non-Windows runtimes.",
            RequiresElevation = true,
            Type = ConditionType.Wmi,
            TimeoutSeconds = 15,
            Wmi = new WmiCheckDefinition(),
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "Operating system row returned",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.GreaterThan,
                    ExpectedValue = "0"
                },
                new OutcomeRuleDefinition
                {
                    Name = "No operating system row returned",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "0"
                }
            ]
        };

    public static ConditionGroupDefinition CreateMacOsSystemGroup() =>
        new()
        {
            Id = "grp_macos_system",
            Name = "macOS system",
            Description = "Core host checks for macOS version, architecture, disk, memory and power state.",
            Conditions =
            [
                CreateMacOsVersionCondition(),
                CreateCpuArchitectureCondition(),
                CreateFreeDiskSpaceCondition(),
                CreateMemoryPressureCondition(),
                CreatePowerSourceCondition()
            ]
        };

    public static ConditionGroupDefinition CreateRuntimeDependenciesGroup() =>
        new()
        {
            Id = "grp_macos_runtime",
            Name = "Runtime dependencies",
            Description = "Checks common runtimes and macOS developer prerequisites.",
            Conditions =
            [
                CreatePowerShellInstalledCondition(),
                CreateDotNetSdkCondition(),
                CreateNodeJsCondition(),
                CreatePython3Condition(),
                CreateHomebrewCondition(),
                CreateXcodeCommandLineToolsCondition(),
                CreateRosetta2Condition()
            ]
        };

    public static ConditionGroupDefinition CreateSecurityPostureGroup() =>
        new()
        {
            Id = "grp_macos_security",
            Name = "Security posture",
            Description = "Checks common macOS security posture settings.",
            Conditions =
            [
                CreateGatekeeperCondition(),
                CreateSystemIntegrityProtectionCondition(),
                CreateFileVaultCondition(),
                CreateApplicationFirewallCondition()
            ]
        };

    public static ConditionGroupDefinition CreateNetworkGroup() =>
        new()
        {
            Id = "grp_network",
            Name = "Network",
            Description = "Checks DNS, HTTPS and package registry reachability.",
            Conditions =
            [
                CreateDnsResolutionCondition(),
                CreateInternetHttpsAccessCondition(),
                CreateVpnInterfaceCondition(),
                CreateGitHubReachableCondition(),
                CreateNuGetReachableCondition(),
                CreateNpmRegistryReachableCondition()
            ]
        };

    public static ConditionGroupDefinition CreateDeveloperToolingGroup() =>
        new()
        {
            Id = "grp_macos_developer_tools",
            Name = "Developer tooling",
            Description = "Checks Git, SSH, Docker and Kubernetes prerequisites.",
            Conditions =
            [
                CreateGitInstalledCondition(),
                CreateGitIdentityCondition(),
                CreateSshAgentCondition(),
                CreateSshKeysCondition(),
                CreateDockerInstalledCondition(),
                CreateDockerDaemonCondition(),
                CreateKubernetesContextCondition()
            ]
        };

    public static ConditionDefinition CreateMacOsVersionCondition() =>
        CreatePowerShellCondition(
            "cond_macos_version",
            "macOS version",
            "Checks that the host is running macOS 14 or newer.",
            "Upgrade macOS to a supported version.",
            """
            $version = [version](sw_vers -productVersion)
            [pscustomobject]@{
                Version = $version.ToString()
                Major = $version.Major
            }
            """,
            [
                Rule("macOS 14 or newer", CheckOutcome.Passed, "Major", RuleOperator.GreaterThanOrEqual, "14"),
                Rule("Unsupported macOS version", CheckOutcome.Error, "Major", RuleOperator.LessThan, "14", "Upgrade macOS to version 14 or newer.")
            ],
            runInParallel: false);

    public static ConditionDefinition CreateCpuArchitectureCondition() =>
        CreatePowerShellCondition(
            "cond_macos_architecture",
            "CPU architecture",
            "Verifies that the Mac reports a supported architecture.",
            "Use a supported Mac architecture or install compatible tooling.",
            """
            $arch = (uname -m).Trim()
            [pscustomobject]@{
                Architecture = $arch
            }
            """,
            [
                Rule("Apple Silicon or Intel", CheckOutcome.Passed, "Architecture", RuleOperator.RegexMatch, "^(arm64|x86_64)$"),
                Rule("Unexpected architecture", CheckOutcome.Warning, "Architecture", RuleOperator.RegexNotMatch, "^(arm64|x86_64)$", "Verify that all required tools support this CPU architecture.")
            ]);

    public static ConditionDefinition CreateFreeDiskSpaceCondition() =>
        CreatePowerShellCondition(
            "cond_macos_disk_space",
            "Free disk space",
            "Checks that the system volume has at least 20 GB free.",
            "Free up at least 20 GB on the system volume.",
            """
            $drive = Get-PSDrive -Name /
            [pscustomobject]@{
                FreeGB = [math]::Round($drive.Free / 1GB, 2)
            }
            """,
            [
                Rule("At least 20 GB free", CheckOutcome.Passed, "FreeGB", RuleOperator.GreaterThanOrEqual, "20"),
                Rule("Low disk space", CheckOutcome.Warning, "FreeGB", RuleOperator.LessThan, "20", "Free up disk space before running longer installs or builds.")
            ]);

    public static ConditionDefinition CreateMemoryPressureCondition() =>
        CreatePowerShellCondition(
            "cond_macos_memory_pressure",
            "Memory pressure",
            "Checks whether macOS reports critical memory pressure.",
            "Close memory-heavy applications or add memory capacity if pressure is high.",
            """
            $output = (memory_pressure 2>$null | Out-String)
            $state = if ($output -match 'System-wide memory free percentage') { 'Available' } else { 'Unknown' }
            [pscustomobject]@{
                State = $state
                OutputPresent = -not [string]::IsNullOrWhiteSpace($output)
            }
            """,
            [
                Rule("Memory pressure command returned output", CheckOutcome.Passed, "OutputPresent", RuleOperator.Equals, "True"),
                Rule("Memory pressure unavailable", CheckOutcome.Warning, "OutputPresent", RuleOperator.NotEquals, "True", "Check Activity Monitor if the machine feels memory constrained.")
            ]);

    public static ConditionDefinition CreatePowerSourceCondition() =>
        CreatePowerShellCondition(
            "cond_macos_power_source",
            "Power source",
            "Checks whether the Mac is connected to AC power.",
            "Connect AC power before running long operations.",
            """
            $battery = (pmset -g batt | Out-String)
            $isAc = $battery -match "AC Power"
            [pscustomobject]@{
                OnACPower = $isAc
            }
            """,
            [
                Rule("Connected to AC power", CheckOutcome.Passed, "OnACPower", RuleOperator.Equals, "True"),
                Rule("Running on battery", CheckOutcome.Warning, "OnACPower", RuleOperator.NotEquals, "True", "Connect the Mac to power before long-running operations.")
            ]);

    public static ConditionDefinition CreatePowerShellInstalledCondition() =>
        CreateInstalledCommandCondition(
            "cond_pwsh_available",
            "PowerShell installed",
            "Verifies that pwsh is available.",
            "Install PowerShell 7.",
            "pwsh",
            "pwsh found",
            "pwsh missing",
            CheckOutcome.Error,
            "Install PowerShell 7 with Homebrew or the official package.");

    public static ConditionDefinition CreateDotNetSdkCondition() =>
        CreatePowerShellCondition(
            "cond_dotnet_sdk",
            ".NET SDK",
            "Checks that dotnet is installed and reports at least one SDK.",
            "Install the required .NET SDK.",
            """
            $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
            $sdks = if ($dotnet) { dotnet --list-sdks 2>$null } else { @() }
            [pscustomobject]@{
                Installed = $null -ne $dotnet
                SdkCount = @($sdks).Count
            }
            """,
            [
                Rule(".NET SDK installed", CheckOutcome.Passed, "SdkCount", RuleOperator.GreaterThan, "0"),
                Rule(".NET SDK missing", CheckOutcome.Error, "SdkCount", RuleOperator.Equals, "0", "Install the required .NET SDK.")
            ]);

    public static ConditionDefinition CreateNodeJsCondition() =>
        CreatePowerShellCondition(
            "cond_nodejs",
            "Node.js",
            "Checks that Node.js is installed.",
            "Install Node.js if required.",
            """
            $cmd = Get-Command node -ErrorAction SilentlyContinue
            $version = if ($cmd) { (node --version).TrimStart('v') } else { '' }
            [pscustomobject]@{
                Installed = $null -ne $cmd
                Version = $version
            }
            """,
            [
                Rule("Node.js installed", CheckOutcome.Passed, "Installed", RuleOperator.Equals, "True"),
                Rule("Node.js missing", CheckOutcome.Warning, "Installed", RuleOperator.NotEquals, "True", "Install Node.js if this workflow needs JavaScript tooling.")
            ]);

    public static ConditionDefinition CreatePython3Condition() =>
        CreateInstalledCommandCondition(
            "cond_python3",
            "Python 3",
            "Checks that Python 3 is installed.",
            "Install Python 3 if required.",
            "python3",
            "Python 3 installed",
            "Python 3 missing",
            CheckOutcome.Warning,
            "Install Python 3 if this workflow needs Python tooling.");

    public static ConditionDefinition CreateHomebrewCondition() =>
        CreateInstalledCommandCondition(
            "cond_homebrew",
            "Homebrew",
            "Checks that Homebrew is installed.",
            "Install Homebrew if required.",
            "brew",
            "Homebrew installed",
            "Homebrew missing",
            CheckOutcome.Warning,
            "Install Homebrew if dependencies are managed with brew.");

    public static ConditionDefinition CreateXcodeCommandLineToolsCondition() =>
        CreatePowerShellCondition(
            "cond_xcode_clt",
            "Xcode Command Line Tools",
            "Checks that Xcode Command Line Tools are installed.",
            "Install Xcode Command Line Tools.",
            """
            $path = xcode-select -p 2>$null
            [pscustomobject]@{
                Installed = $LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($path)
            }
            """,
            [
                Rule("Command Line Tools installed", CheckOutcome.Passed, "Installed", RuleOperator.Equals, "True"),
                Rule("Command Line Tools missing", CheckOutcome.Error, "Installed", RuleOperator.NotEquals, "True", "Install Xcode Command Line Tools with xcode-select --install.")
            ]);

    public static ConditionDefinition CreateRosetta2Condition() =>
        CreatePowerShellCondition(
            "cond_rosetta2",
            "Rosetta 2",
            "Checks Rosetta 2 availability on Apple Silicon; Intel Macs pass automatically.",
            "Install Rosetta 2 if required by x64-only tools.",
            """
            $arch = (uname -m).Trim()
            $installed = $true
            if ($arch -eq 'arm64') {
                /usr/bin/pgrep oahd *> $null
                $installed = $LASTEXITCODE -eq 0
            }
            [pscustomobject]@{
                Required = $arch -eq 'arm64'
                Installed = $installed
            }
            """,
            [
                Rule("Rosetta available or not required", CheckOutcome.Passed, "Installed", RuleOperator.Equals, "True"),
                Rule("Rosetta missing", CheckOutcome.Warning, "Installed", RuleOperator.NotEquals, "True", "Install Rosetta 2 if x64-only tools are required.")
            ]);

    public static ConditionDefinition CreateGatekeeperCondition() =>
        CreateEnabledCondition(
            "cond_gatekeeper",
            "Gatekeeper",
            "Checks that Gatekeeper assessment is enabled.",
            "Enable Gatekeeper if required by policy.",
            """
            $status = (spctl --status 2>$null | Out-String).Trim()
            [pscustomobject]@{
                Enabled = $status -match 'assessments enabled'
            }
            """,
            "Gatekeeper enabled",
            "Gatekeeper disabled",
            "Enable Gatekeeper unless this is an intentional admin exception.");

    public static ConditionDefinition CreateSystemIntegrityProtectionCondition() =>
        CreateEnabledCondition(
            "cond_sip",
            "System Integrity Protection",
            "Checks that SIP is enabled.",
            "Re-enable System Integrity Protection if required by policy.",
            """
            $status = (csrutil status 2>$null | Out-String).Trim()
            [pscustomobject]@{
                Enabled = $status -match 'enabled'
            }
            """,
            "SIP enabled",
            "SIP disabled",
            "Re-enable SIP unless this Mac intentionally runs with reduced protections.");

    public static ConditionDefinition CreateFileVaultCondition() =>
        CreateEnabledCondition(
            "cond_filevault",
            "FileVault",
            "Checks that FileVault disk encryption is enabled.",
            "Enable FileVault if required by policy.",
            """
            $status = (fdesetup status 2>$null | Out-String).Trim()
            [pscustomobject]@{
                Enabled = $status -match 'FileVault is On'
            }
            """,
            "FileVault enabled",
            "FileVault disabled",
            "Enable FileVault if disk encryption is required.");

    public static ConditionDefinition CreateApplicationFirewallCondition() =>
        CreateEnabledCondition(
            "cond_application_firewall",
            "Application firewall",
            "Checks that the macOS application firewall is enabled.",
            "Enable the macOS application firewall if required.",
            """
            $state = (/usr/libexec/ApplicationFirewall/socketfilterfw --getglobalstate 2>$null | Out-String).Trim()
            [pscustomobject]@{
                Enabled = $state -match 'enabled'
            }
            """,
            "Firewall enabled",
            "Firewall disabled",
            "Enable the macOS application firewall if required by policy.");

    public static ConditionDefinition CreateDnsResolutionCondition() =>
        CreatePowerShellCondition(
            "cond_dns_resolution",
            "DNS resolution",
            "Checks that DNS can resolve github.com.",
            "Check DNS, VPN and proxy configuration.",
            """
            $resolved = $false
            try {
                [System.Net.Dns]::GetHostAddresses('github.com') | Out-Null
                $resolved = $true
            } catch {
                $resolved = $false
            }
            [pscustomobject]@{
                Resolved = $resolved
            }
            """,
            [
                Rule("DNS resolves", CheckOutcome.Passed, "Resolved", RuleOperator.Equals, "True"),
                Rule("DNS failed", CheckOutcome.Error, "Resolved", RuleOperator.NotEquals, "True", "Check DNS, VPN and proxy configuration.")
            ],
            timeoutSeconds: 10);

    public static ConditionDefinition CreateInternetHttpsAccessCondition() =>
        CreatePowerShellCondition(
            "cond_internet_https",
            "Internet HTTPS access",
            "Checks HTTPS reachability to Apple.",
            "Check network connectivity and TLS settings.",
            """
            $ok = $false
            try {
                $response = Invoke-WebRequest -Uri 'https://www.apple.com' -Method Head -TimeoutSec 8
                $ok = [int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 500
            } catch {
                $ok = $false
            }
            [pscustomobject]@{
                Reachable = $ok
            }
            """,
            [
                Rule("HTTPS reachable", CheckOutcome.Passed, "Reachable", RuleOperator.Equals, "True"),
                Rule("HTTPS unreachable", CheckOutcome.Error, "Reachable", RuleOperator.NotEquals, "True", "Check network connectivity and TLS inspection settings.")
            ],
            timeoutSeconds: 12);

    public static ConditionDefinition CreateVpnInterfaceCondition() =>
        CreatePowerShellCondition(
            "cond_vpn_interface",
            "VPN interface",
            "Checks whether a utun VPN-style interface is present.",
            "Connect VPN if internal resources are required.",
            """
            $interfaces = ifconfig 2>$null
            [pscustomobject]@{
                Present = ($interfaces | Select-String -Pattern '^utun\d+:' -Quiet)
            }
            """,
            [
                Rule("VPN interface present", CheckOutcome.Passed, "Present", RuleOperator.Equals, "True"),
                Rule("VPN interface missing", CheckOutcome.Warning, "Present", RuleOperator.NotEquals, "True", "Connect VPN if internal resources are required.")
            ]);

    public static ConditionDefinition CreateGitHubReachableCondition() =>
        CreatePortReachabilityCondition(
            "cond_github_reachable",
            "GitHub reachable",
            "Checks HTTPS reachability to github.com.",
            "Check proxy, VPN or firewall rules for GitHub.",
            "github.com",
            "GitHub reachable",
            "GitHub unreachable",
            "Check proxy, VPN or firewall rules for github.com:443.");

    public static ConditionDefinition CreateNuGetReachableCondition() =>
        CreatePortReachabilityCondition(
            "cond_nuget_reachable",
            "NuGet reachable",
            "Checks HTTPS reachability to api.nuget.org.",
            "Check proxy, VPN or firewall rules for NuGet.",
            "api.nuget.org",
            "NuGet reachable",
            "NuGet unreachable",
            "Check proxy, VPN or firewall rules for api.nuget.org:443.");

    public static ConditionDefinition CreateNpmRegistryReachableCondition() =>
        CreatePortReachabilityCondition(
            "cond_npm_reachable",
            "npm registry reachable",
            "Checks HTTPS reachability to registry.npmjs.org.",
            "Check proxy, VPN or firewall rules for npm.",
            "registry.npmjs.org",
            "npm registry reachable",
            "npm registry unreachable",
            "Check proxy, VPN or firewall rules for registry.npmjs.org:443.");

    public static ConditionDefinition CreateGitInstalledCondition() =>
        CreateInstalledCommandCondition(
            "cond_git_installed",
            "Git installed",
            "Checks that Git is available.",
            "Install Git or Xcode Command Line Tools.",
            "git",
            "Git installed",
            "Git missing",
            CheckOutcome.Error,
            "Install Git or Xcode Command Line Tools.");

    public static ConditionDefinition CreateGitIdentityCondition() =>
        CreatePowerShellCondition(
            "cond_git_identity",
            "Git identity",
            "Checks that Git user.name and user.email are configured.",
            "Configure git user.name and user.email.",
            """
            $name = git config --global user.name 2>$null
            $email = git config --global user.email 2>$null
            [pscustomobject]@{
                Configured = -not [string]::IsNullOrWhiteSpace($name) -and -not [string]::IsNullOrWhiteSpace($email)
            }
            """,
            [
                Rule("Git identity configured", CheckOutcome.Passed, "Configured", RuleOperator.Equals, "True"),
                Rule("Git identity missing", CheckOutcome.Warning, "Configured", RuleOperator.NotEquals, "True", "Configure git user.name and user.email.")
            ]);

    public static ConditionDefinition CreateSshAgentCondition() =>
        CreatePowerShellCondition(
            "cond_ssh_agent",
            "SSH agent",
            "Checks that ssh-agent is running or SSH_AUTH_SOCK is set.",
            "Start ssh-agent if SSH remotes are used.",
            """
            [pscustomobject]@{
                Available = -not [string]::IsNullOrWhiteSpace($env:SSH_AUTH_SOCK)
            }
            """,
            [
                Rule("SSH agent available", CheckOutcome.Passed, "Available", RuleOperator.Equals, "True"),
                Rule("SSH agent missing", CheckOutcome.Warning, "Available", RuleOperator.NotEquals, "True", "Start ssh-agent or configure SSH_AUTH_SOCK if SSH remotes are used.")
            ]);

    public static ConditionDefinition CreateSshKeysCondition() =>
        CreatePowerShellCondition(
            "cond_ssh_keys",
            "SSH keys",
            "Checks that at least one common SSH public key exists.",
            "Create or add an SSH key if SSH remotes are used.",
            """
            $keys = Get-ChildItem -Path "$HOME/.ssh" -Filter "*.pub" -ErrorAction SilentlyContinue
            [pscustomobject]@{
                KeyCount = @($keys).Count
            }
            """,
            [
                Rule("SSH public key exists", CheckOutcome.Passed, "KeyCount", RuleOperator.GreaterThan, "0"),
                Rule("SSH public key missing", CheckOutcome.Warning, "KeyCount", RuleOperator.Equals, "0", "Create or add an SSH key if SSH remotes are used.")
            ]);

    public static ConditionDefinition CreateDockerInstalledCondition() =>
        CreateInstalledCommandCondition(
            "cond_docker_installed",
            "Docker installed",
            "Checks that Docker CLI is available.",
            "Install Docker Desktop if containers are required.",
            "docker",
            "Docker installed",
            "Docker missing",
            CheckOutcome.Warning,
            "Install Docker Desktop if containers are required.");

    public static ConditionDefinition CreateDockerDaemonCondition() =>
        CreatePowerShellCondition(
            "cond_docker_daemon",
            "Docker daemon",
            "Checks that Docker daemon responds.",
            "Start Docker Desktop if containers are required.",
            """
            $ok = $false
            try {
                docker info *> $null
                $ok = $LASTEXITCODE -eq 0
            } catch {
                $ok = $false
            }
            [pscustomobject]@{
                Running = $ok
            }
            """,
            [
                Rule("Docker daemon running", CheckOutcome.Passed, "Running", RuleOperator.Equals, "True"),
                Rule("Docker daemon unavailable", CheckOutcome.Warning, "Running", RuleOperator.NotEquals, "True", "Start Docker Desktop if containers are required.")
            ],
            runInParallel: false);

    public static ConditionDefinition CreateKubernetesContextCondition() =>
        CreatePowerShellCondition(
            "cond_kube_context",
            "Kubernetes context",
            "Checks whether kubectl has a current context.",
            "Configure kubectl context if Kubernetes access is required.",
            """
            $kubectl = Get-Command kubectl -ErrorAction SilentlyContinue
            $context = if ($kubectl) { kubectl config current-context 2>$null } else { '' }
            [pscustomobject]@{
                HasContext = -not [string]::IsNullOrWhiteSpace($context)
            }
            """,
            [
                Rule("Kubernetes context configured", CheckOutcome.Passed, "HasContext", RuleOperator.Equals, "True"),
                Rule("Kubernetes context missing", CheckOutcome.Warning, "HasContext", RuleOperator.NotEquals, "True", "Configure kubectl context if Kubernetes access is required.")
            ]);

    public static ConditionDefinition CreateWindowsServerVersionCondition() =>
        new()
        {
            Name = "Windows Server version",
            Description = "Reads the installed Windows Server caption, version and build number.",
            RecommendedAction = "Run this check on the target Windows Server host and verify that the reported version is supported.",
            RequiresElevation = true,
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 20,
            PowerShell = new PowerShellCheckDefinition
            {
                Script =
                    """
                    $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop

                    [pscustomobject]@{
                        Caption = $os.Caption
                        Version = $os.Version
                        BuildNumber = $os.BuildNumber
                        ProductType = $os.ProductType
                        IsWindowsServer = [bool]($os.ProductType -ne 1 -or $os.Caption -like '*Windows Server*')
                    }
                    """
            },
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "No operating system information returned",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "0"
                },
                new OutcomeRuleDefinition
                {
                    Name = "Host is not Windows Server",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "IsWindowsServer",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True",
                    RecommendedAction = "Run the prerequisite checks on a Windows Server host."
                },
                new OutcomeRuleDefinition
                {
                    Name = "Windows Server version detected",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.FirstRow,
                    Field = "IsWindowsServer",
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "True"
                }
            ]
        };

    public static ConditionDefinition CreateSqlServerVersionCondition() =>
        new()
        {
            Name = "SQL Server version",
            Description = "Finds installed SQL Server Database Engine instances, service status, version and edition.",
            RecommendedAction = "Install SQL Server or start at least one SQL Server Database Engine service.",
            RequiresElevation = true,
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 30,
            PowerShell = new PowerShellCheckDefinition
            {
                Script =
                    """
                    $instanceRegistryPath = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
                    $instanceIds = @{}

                    if (Test-Path $instanceRegistryPath) {
                        $registryItem = Get-ItemProperty -Path $instanceRegistryPath
                        foreach ($property in $registryItem.PSObject.Properties) {
                            if ($property.Name -notlike 'PS*') {
                                $instanceIds[$property.Name] = [string]$property.Value
                            }
                        }
                    }

                    Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue |
                        Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' } |
                        ForEach-Object {
                            $instanceName = if ($_.Name -eq 'MSSQLSERVER') { 'MSSQLSERVER' } else { $_.Name.Substring(6) }
                            $instanceId = $instanceIds[$instanceName]
                            $setupPath = if ($instanceId) { "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\Setup" } else { $null }
                            $setup = if ($setupPath -and (Test-Path $setupPath)) { Get-ItemProperty -Path $setupPath } else { $null }

                            [pscustomobject]@{
                                InstanceName = $instanceName
                                ServiceName = $_.Name
                                Status = [string]$_.Status
                                IsRunning = [bool]($_.Status -eq 'Running')
                                Version = if ($setup) { $setup.Version } else { $null }
                                Edition = if ($setup) { $setup.Edition } else { $null }
                            }
                        }
                    """
            },
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "No SQL Server Database Engine service found",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "0"
                },
                new OutcomeRuleDefinition
                {
                    Name = "At least one SQL Server instance is running",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.AnyRow,
                    Field = "IsRunning",
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "True"
                },
                new OutcomeRuleDefinition
                {
                    Name = "SQL Server is installed but no instance is running",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.RowCount,
                    Operator = RuleOperator.GreaterThan,
                    ExpectedValue = "0",
                    RecommendedAction = "Start a SQL Server Database Engine service before continuing."
                }
            ]
        };

    public static ConditionDefinition CreateDnsStatusCondition() =>
        new()
        {
            Name = "DNS status",
            Description = "Checks whether the Windows DNS Server service is installed, running and able to resolve localhost.",
            RecommendedAction = "Install and start the DNS Server role, then verify local name resolution.",
            RequiresElevation = true,
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 20,
            PowerShell = new PowerShellCheckDefinition
            {
                Script =
                    """
                    $service = Get-Service -Name DNS -ErrorAction SilentlyContinue
                    $resolved = $false

                    try {
                        $resolved = [bool](Resolve-DnsName -Name localhost -ErrorAction Stop)
                    }
                    catch {
                        $resolved = $false
                    }

                    [pscustomobject]@{
                        ServiceInstalled = [bool]$service
                        ServiceStatus = if ($service) { [string]$service.Status } else { $null }
                        LocalhostResolves = $resolved
                    }
                    """
            },
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "DNS Server service is not installed",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.FirstRow,
                    Field = "ServiceInstalled",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True"
                },
                new OutcomeRuleDefinition
                {
                    Name = "DNS Server service is not running",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "ServiceStatus",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "Running",
                    RecommendedAction = "Start the DNS Server service."
                },
                new OutcomeRuleDefinition
                {
                    Name = "Local DNS resolution failed",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "LocalhostResolves",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True",
                    RecommendedAction = "Verify DNS resolver configuration and local DNS health."
                },
                new OutcomeRuleDefinition
                {
                    Name = "DNS service is running and resolves localhost",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.FirstRow,
                    Field = "ServiceStatus",
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "Running"
                }
            ]
        };

    public static ConditionDefinition CreateIisInstallationAndFunctionCondition() =>
        new()
        {
            Name = "IIS installation and function",
            Description = "Checks whether IIS is installed, W3SVC is running and localhost responds over HTTP.",
            RecommendedAction = "Install the Web Server role, start W3SVC and verify that the default web endpoint answers locally.",
            RequiresElevation = true,
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 30,
            PowerShell = new PowerShellCheckDefinition
            {
                Script =
                    """
                    $feature = if (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue) {
                        Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
                    } else {
                        $null
                    }
                    $service = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
                    $statusCode = $null
                    $responds = $false

                    try {
                        $response = Invoke-WebRequest -Uri 'http://localhost/' -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
                        $statusCode = [int]$response.StatusCode
                        $responds = $statusCode -ge 200 -and $statusCode -lt 500
                    }
                    catch {
                        $responds = $false
                    }

                    [pscustomobject]@{
                        Installed = [bool](($feature -and $feature.Installed) -or $service)
                        FeatureInstalled = if ($feature) { [bool]$feature.Installed } else { $null }
                        ServiceStatus = if ($service) { [string]$service.Status } else { $null }
                        LocalhostResponds = $responds
                        StatusCode = $statusCode
                    }
                    """
            },
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "IIS is not installed",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.FirstRow,
                    Field = "Installed",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True"
                },
                new OutcomeRuleDefinition
                {
                    Name = "IIS service is not running",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "ServiceStatus",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "Running",
                    RecommendedAction = "Start the World Wide Web Publishing Service."
                },
                new OutcomeRuleDefinition
                {
                    Name = "IIS localhost request failed",
                    Outcome = CheckOutcome.Warning,
                    Target = RuleTarget.FirstRow,
                    Field = "LocalhostResponds",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True",
                    RecommendedAction = "Verify site bindings, firewall rules and the default IIS site."
                },
                new OutcomeRuleDefinition
                {
                    Name = "IIS is installed and answers locally",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.FirstRow,
                    Field = "LocalhostResponds",
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "True"
                }
            ]
        };

    public static ConditionDefinition CreateApiJsonPropertyCondition() =>
        new()
        {
            Name = "API JSON property",
            Description = "Calls an HTTP API and verifies that a JSON property has the expected value.",
            RecommendedAction = "Update the API URL, property path and expected value for the target environment.",
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 30,
            PowerShell = new PowerShellCheckDefinition
            {
                Script =
                    """
                    $apiUrl = 'https://api.example.test/health'
                    $propertyPath = 'status'
                    $expectedValue = 'ok'

                    function Get-JsonPropertyValue {
                        param(
                            [Parameter(Mandatory = $true)] $InputObject,
                            [Parameter(Mandatory = $true)] [string] $Path
                        )

                        $current = $InputObject
                        foreach ($segment in $Path.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries)) {
                            if ($null -eq $current) {
                                return $null
                            }

                            $property = $current.PSObject.Properties[$segment]
                            if ($null -eq $property) {
                                return $null
                            }

                            $current = $property.Value
                        }

                        return $current
                    }

                    $json = Invoke-RestMethod -Uri $apiUrl -Method Get -TimeoutSec 20 -ErrorAction Stop
                    $actualValue = Get-JsonPropertyValue -InputObject $json -Path $propertyPath

                    [pscustomobject]@{
                        ApiUrl = $apiUrl
                        PropertyPath = $propertyPath
                        ExpectedValue = $expectedValue
                        ActualValue = $actualValue
                        Matches = [bool]([string]$actualValue -eq $expectedValue)
                    }
                    """
            },
            Rules =
            [
                new OutcomeRuleDefinition
                {
                    Name = "API property value does not match",
                    Outcome = CheckOutcome.Error,
                    Target = RuleTarget.FirstRow,
                    Field = "Matches",
                    Operator = RuleOperator.NotEquals,
                    ExpectedValue = "True",
                    RecommendedAction = "Verify API reachability, JSON schema and the expected property value."
                },
                new OutcomeRuleDefinition
                {
                    Name = "API property value matches",
                    Outcome = CheckOutcome.Passed,
                    Target = RuleTarget.FirstRow,
                    Field = "Matches",
                    Operator = RuleOperator.Equals,
                    ExpectedValue = "True"
                }
            ]
        };

    private static ConditionDefinition CreateInstalledCommandCondition(
        string id,
        string name,
        string description,
        string recommendedAction,
        string command,
        string passedRuleName,
        string missingRuleName,
        CheckOutcome missingOutcome,
        string missingRecommendedAction) =>
        CreatePowerShellCondition(
            id,
            name,
            description,
            recommendedAction,
            $$"""
            $cmd = Get-Command {{command}} -ErrorAction SilentlyContinue
            [pscustomobject]@{
                Installed = $null -ne $cmd
            }
            """,
            [
                Rule(passedRuleName, CheckOutcome.Passed, "Installed", RuleOperator.Equals, "True"),
                Rule(missingRuleName, missingOutcome, "Installed", RuleOperator.NotEquals, "True", missingRecommendedAction)
            ]);

    private static ConditionDefinition CreateEnabledCondition(
        string id,
        string name,
        string description,
        string recommendedAction,
        string script,
        string passedRuleName,
        string disabledRuleName,
        string disabledRecommendedAction) =>
        CreatePowerShellCondition(
            id,
            name,
            description,
            recommendedAction,
            script,
            [
                Rule(passedRuleName, CheckOutcome.Passed, "Enabled", RuleOperator.Equals, "True"),
                Rule(disabledRuleName, CheckOutcome.Warning, "Enabled", RuleOperator.NotEquals, "True", disabledRecommendedAction)
            ]);

    private static ConditionDefinition CreatePortReachabilityCondition(
        string id,
        string name,
        string description,
        string recommendedAction,
        string host,
        string passedRuleName,
        string unreachableRuleName,
        string unreachableRecommendedAction) =>
        CreatePowerShellCondition(
            id,
            name,
            description,
            recommendedAction,
            $$"""
            $ok = Test-NetConnection -ComputerName {{host}} -Port 443 -InformationLevel Quiet
            [pscustomobject]@{
                Reachable = $ok
            }
            """,
            [
                Rule(passedRuleName, CheckOutcome.Passed, "Reachable", RuleOperator.Equals, "True"),
                Rule(unreachableRuleName, CheckOutcome.Warning, "Reachable", RuleOperator.NotEquals, "True", unreachableRecommendedAction)
            ],
            timeoutSeconds: 10);

    private static ConditionDefinition CreatePowerShellCondition(
        string id,
        string name,
        string description,
        string recommendedAction,
        string script,
        List<OutcomeRuleDefinition> rules,
        bool runInParallel = true,
        int timeoutSeconds = 20) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            RecommendedAction = recommendedAction,
            RunInParallel = runInParallel,
            Type = ConditionType.PowerShell,
            TimeoutSeconds = timeoutSeconds,
            PowerShell = new PowerShellCheckDefinition
            {
                Script = script
            },
            Rules = rules
        };

    private static OutcomeRuleDefinition Rule(
        string name,
        CheckOutcome outcome,
        string field,
        RuleOperator ruleOperator,
        string expectedValue,
        string recommendedAction = "") =>
        new()
        {
            Name = name,
            Outcome = outcome,
            Target = RuleTarget.Scalar,
            Field = field,
            Operator = ruleOperator,
            ExpectedValue = expectedValue,
            RecommendedAction = recommendedAction
        };
}
