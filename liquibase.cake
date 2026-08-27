#addin "nuget:?package=Cake.Docker&version=1.5.0"
#nullable enable
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

var networkName = $"net-{Guid.NewGuid().ToString()}";
var db_uid = Argument("db_uid", EnvironmentVariable("db_uid") ?? "postgres");
var db_pwd = Argument("db_pwd", EnvironmentVariable("db_pwd") ?? "postgres");
var db_name = Argument("db_name", EnvironmentVariable("db_name") ?? "dbname");

var liquibase_ver = "5.0.1";
int assignedPort = 0;
bool containerStarted = false;
string? liquibaseUsername = null;


Task("Read-Liquibase-Username")
    .Does(() =>
    {
        var propertiesFile = "./changelog/liquibase.properties";
	
        if (!FileExists(propertiesFile))
        {
            throw new CakeException($"Properties file not found: {propertiesFile}");
        }
        
        var lines = System.IO.File.ReadAllText(propertiesFile);
        var match = Regex.Match(lines, @"^\s*liquibase\.command\.username\s*[:|=]\s*(.*)\s*$", RegexOptions.Multiline);
        if(match.Success)
        {
            liquibaseUsername = match.Groups[1].Value;
            Information($"Found liquibase.command.username: {liquibaseUsername}");
        }
        else
        {
            throw new CakeException("Property 'liquibase.command.username' not found in liquibase.properties");
        }
    });

Task("Find-Available-Port")
    .Does(() =>
    {
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            assignedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        
        Information($"Dynamically allocated free host port: {assignedPort}");
    });
    
Task("Create-Network")
    .IsDependentOn("Find-Available-Port")
    .Does(() =>
    {
        Information($"Creating Docker network: {networkName}");
        DockerNetworkCreate(new [] { networkName });
    });
    
Task("Start-Postgres")
    .IsDependentOn("Read-Liquibase-Username")
    .IsDependentOn("Create-Network")
    .Does(() =>
    {
        var settings = new DockerContainerRunSettings
        {
            Name = "my-postgres",
            Network = networkName,
            Detach = true,
            Publish = new [] { $"{assignedPort}:5432" },
            Env = new[] {
                $"POSTGRES_USER={liquibaseUsername}",
                $"POSTGRES_PASSWORD={db_pwd}",
                $"POSTGRES_DB={db_name}"
            },
            Volume = new [] { "pgdata:/var/lib/postgresql/data" }
        };

        DockerRun(settings, "postgres:16-alpine", string.Empty, string.Empty);
        System.Threading.Thread.Sleep(5000); 
        containerStarted = true;
        Information($"Postgre started");
    });
    
    
Task("Install-Liquibase")
    .Does(() =>
    {
        Information("Installing Liquibase and Postgres driver.");
        
        int exitCode = StartProcess("bash", new ProcessSettings {
            Arguments = "-c \"" +
                "mkdir -p ./liquibase && " +
                $"curl -sL \"https://github.com/liquibase/liquibase/releases/download/v{liquibase_ver}/liquibase-{liquibase_ver}.tar.gz\" | tar xz -C ./liquibase &&" +
                "./liquibase/liquibase lpm add postgresql" +
                "\""
        });
        
        if (exitCode != 0)
        {
            Information($"Failed to install PostgreSQL driver. Exit code: {exitCode}");
        }
    });

Task("Validate-Changelog")
    .IsDependentOn("Start-Postgres")
    .IsDependentOn("Install-Liquibase")
    .Does(() =>
    {
        // first validate the change
        var liquibaseCmd = $"./liquibase/liquibase --url=jdbc:postgresql://localhost:{assignedPort}/{db_name} --search-path=./changelog --password {db_pwd} --defaults-file=./changelog/liquibase.properties";

        int exitCode = StartProcess("bash", new ProcessSettings {
            Arguments = $"-c \"{liquibaseCmd} validate\""
        });
    
        if (exitCode != 0)
        {
            throw new CakeException($"Failed to validate the changelog: {exitCode}");
        }

        // now apply the change to make sure
        exitCode = StartProcess("bash", new ProcessSettings {
            Arguments = $"-c \"{liquibaseCmd} update\""
        });
    
        if (exitCode != 0)
        {
            throw new CakeException($"Failed to apply the changelod: {exitCode}");
        }
    });
    
Teardown(context =>
{
    if (containerStarted)
    {
        Information("Cleaning up Docker containers via global script Teardown...");
        try 
        {
            DockerStop("my-postgres");
            DockerRm("my-postgres");
            DockerNetworkRemove(networkName);
        }
        catch(Exception ex)
        {
            Warning($"Failed to cleanly stop/remove Docker container: {ex.Message}");
        }
    }
});

Task("Default")
    .IsDependentOn("Validate-Changelog");

RunTarget("Default");
