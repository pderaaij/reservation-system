#tool "nuget:?package=xunit.runner.console"
#addin "Newtonsoft.Json"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");
var verbosity = Argument<string>("verbosity");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions = GetFiles("./**/*.sln");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());
var sourceDir = Directory("./src");
var solutionFile = File(solutions.First().FullPath);

var solutionDir = Directory("./");
var globalFile = solutionDir + File("global.json");

var unitTestsProjGlob = "./**/*.Tests.Unit.csproj";

var artifactsDir = Directory("./artifacts");
///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    // Executed BEFORE the first task.
    EnsureDirectoryExists("./artifacts");
    Verbose("Running tasks...");
});

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Verbose("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans all directories that are used during the build process.")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Verbose("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
        CleanDirectory(artifactsDir);
    }
});

Task("Restore")
    .Description("Restores all the NuGet packages that are used by the specified solution.")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Verbose("Restoring {0}...", solution);
        DotNetCoreRestore(solution.FullPath);
    }
});

Task("Build")
    .Description("Builds all the different parts of the project.")
    .Does(() =>
{
    var verbosityOption = Cake.Core.Diagnostics.Verbosity.Minimal;
    verbosityOption = (Cake.Core.Diagnostics.Verbosity) Enum.Parse(typeof(Cake.Core.Diagnostics.Verbosity), verbosity, true);

    // Build all solutions.
    foreach(var solution in solutions)
    {
        Verbose("Building {0}", solution);
        var settings = new MSBuildSettings{
            Verbosity = verbosityOption
        };

        settings.SetPlatformTarget(PlatformTarget.MSIL)
                .WithProperty("TreatWarningsAsErrors","true")
                .WithTarget("Build")
                .SetConfiguration(configuration);

        MSBuild(solution, settings);
    }
});

void XunitTest(string testDllGlob)
{
    var testAssemblies = GetFiles(testDllGlob);

    foreach(var testProject in testAssemblies){
        Verbose(testProject.FullPath);
        var settings = new DotNetCoreTestSettings
        {
            Configuration = configuration
        };

        DotNetCoreTest(testProject.FullPath, settings);
    }
}

Task("Test-Unit")
    .Does(() => 
{
    XunitTest(unitTestsProjGlob);
});

Task("Test")
    .IsDependentOn("Test-Unit");

Task("Build+Test")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

Task("Rebuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .Does(() =>
{ });

Task("Package")
    .IsDependentOn("Rebuild")
    .Does(() => 
{
    var settings = new DotNetCorePackSettings
    {
        Configuration = "Release",
        OutputDirectory = "./artifacts/"
    };
    
    var project = Newtonsoft.Json.Linq.JObject.Parse(
            System.IO.File.ReadAllText(globalFile, Encoding.UTF8)
        );
    
    foreach(var folder in project["projects"])
    {
        Information(folder);
        var csproj = GetFiles(solutionDir + folder + "/*.csproj").First();
        DotNetCorePack(csproj.FullPath, settings);
    }
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .Description("This is the default task which will be ran if no specific target is passed in.")
    .IsDependentOn("Rebuild")
    .IsDependentOn("Test");
    // .IsDependentOn("Package");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);