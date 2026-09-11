using Prereqqer.Core.Storage;
using Prereqqer.Core.Definitions;
using System.Net;

namespace Prereqqer.Core.Tests;

public sealed class ConditionDefinitionStoreTests
{
    [Fact]
    public void GetDefaultPath_uses_program_directory_for_regular_layout()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), $"prereqqer-app-{Guid.NewGuid():N}");

        var path = ConditionDefinitionStore.GetDefaultPath(baseDirectory, isMacOs: false);

        Assert.Equal(Path.Combine(Path.GetFullPath(baseDirectory), "conditions.json"), path);
    }

    [Fact]
    public void GetDefaultPath_uses_resources_directory_for_macos_app_bundle()
    {
        var baseDirectory = Path.Combine(
            Path.GetTempPath(),
            $"Prereqqer-{Guid.NewGuid():N}.app",
            "Contents",
            "MacOS");

        var path = ConditionDefinitionStore.GetDefaultPath(baseDirectory, isMacOs: true);

        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(baseDirectory)!, "Resources", "conditions.json"),
            path);
    }

    [Fact]
    public async Task LoadOrCreateAsync_creates_default_document_when_file_is_missing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prereqqer-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "conditions.json");

        try
        {
            var store = new ConditionDefinitionStore(path);

            var document = await store.LoadOrCreateAsync();
            var loaded = await store.LoadAsync();

            Assert.True(File.Exists(path));
            Assert.Equal(1, document.SchemaVersion);
            Assert.Equal("Prereqqer", document.Branding.ApplicationName);
            Assert.Equal("Prerequisite checks", document.Branding.Subtitle);
            Assert.Equal(20, document.ConfigurationSource.TimeoutSeconds);
            Assert.NotEmpty(document.Groups);
            Assert.Equal(document.Groups[0].Name, loaded.Groups[0].Name);
            Assert.NotEmpty(loaded.Groups[0].Conditions);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_round_trips_branding_configuration()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prereqqer-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "conditions.json");

        try
        {
            var store = new ConditionDefinitionStore(path);
            var document = new ConditionDefinitionDocument
            {
                Branding = new BrandingDefinition
                {
                    ApplicationName = "Contoso Readiness",
                    Subtitle = "Deployment prerequisites",
                    HeaderBackgroundColor = "#102A43",
                    HeaderForegroundColor = "#F8FAFC",
                    AccentColor = "#2F855A"
                },
                ConfigurationSource = new ConfigurationSourceDefinition
                {
                    Url = "https://config.example.test/prereqqer.json",
                    FetchOnStartup = true,
                    TimeoutSeconds = 12
                },
                Groups =
                [
                    new ConditionGroupDefinition
                    {
                        Name = "Group",
                        Conditions =
                        [
                            new ConditionDefinition
                            {
                                Name = "Condition",
                                RecommendedAction = "Restart the prerequisite service.",
                                RequiresElevation = true,
                                Rules =
                                [
                                    new OutcomeRuleDefinition
                                    {
                                        Name = "Error rule",
                                        Outcome = CheckOutcome.Error,
                                        RecommendedAction = "Install the missing component."
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            await store.SaveAsync(document);

            var json = await File.ReadAllTextAsync(path);
            var loaded = await store.LoadAsync();

            Assert.Contains("\"branding\"", json);
            Assert.Equal("Contoso Readiness", loaded.Branding.ApplicationName);
            Assert.Equal("Deployment prerequisites", loaded.Branding.Subtitle);
            Assert.Equal("#102A43", loaded.Branding.HeaderBackgroundColor);
            Assert.Equal("#F8FAFC", loaded.Branding.HeaderForegroundColor);
            Assert.Equal("#2F855A", loaded.Branding.AccentColor);
            Assert.Equal("https://config.example.test/prereqqer.json", loaded.ConfigurationSource.Url);
            Assert.True(loaded.ConfigurationSource.FetchOnStartup);
            Assert.Equal(12, loaded.ConfigurationSource.TimeoutSeconds);
            Assert.Equal("Restart the prerequisite service.", loaded.Groups[0].Conditions[0].RecommendedAction);
            Assert.True(loaded.Groups[0].Conditions[0].RequiresElevation);
            Assert.Equal("Install the missing component.", loaded.Groups[0].Conditions[0].Rules[0].RecommendedAction);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadFromUrlAsync_loads_and_normalizes_remote_document()
    {
        const string json =
            """
            {
              "schemaVersion": 1,
              "branding": {
                "applicationName": "Remote Readiness"
              },
              "configurationSource": {
                "url": "https://config.example.test/prereqqer.json",
                "fetchOnStartup": true,
                "timeoutSeconds": 8
              },
              "groups": [
                {
                  "name": "Remote group",
                  "conditions": []
                }
              ]
            }
            """;

        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));

        var document = await ConditionDefinitionStore.LoadFromUrlAsync(
            new Uri("https://config.example.test/prereqqer.json"),
            client,
            TimeSpan.FromSeconds(5));

        Assert.Equal("Remote Readiness", document.Branding.ApplicationName);
        Assert.Equal("Remote group", document.Groups[0].Name);
        Assert.Equal("https://config.example.test/prereqqer.json", document.ConfigurationSource.Url);
        Assert.True(document.ConfigurationSource.FetchOnStartup);
        Assert.Equal(8, document.ConfigurationSource.TimeoutSeconds);
    }

    [Fact]
    public async Task LoadFromUrlAsync_rejects_non_http_urls()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConditionDefinitionStore.LoadFromUrlAsync(
                new Uri("file:///tmp/prereqqer.json"),
                client,
                TimeSpan.FromSeconds(5)));

        Assert.Contains("HTTP or HTTPS", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_normalizes_condition_version_to_at_least_one()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prereqqer-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "conditions.json");

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "schemaVersion": 1,
                  "groups": [
                    {
                      "name": "Group",
                      "conditions": [
                        {
                          "name": "Condition",
                          "version": 0
                        }
                      ]
                    }
                  ]
                }
                """);

            var store = new ConditionDefinitionStore(path);
            var document = await store.LoadAsync();

            Assert.Equal(1, document.Groups[0].Conditions[0].Version);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
