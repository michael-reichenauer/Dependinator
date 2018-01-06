#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#addin nuget:?package=Cake.VersionReader
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./Dependinator/bin") + Directory(configuration);
var outputPath = File("Dependinator/bin/Release/Dependinator.exe");
var setupPath = File("DependinatorSetup.exe");


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);

    if (FileExists(setupPath))
    { 
        DeleteFile(setupPath);
    }
});


Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./Dependinator.sln", new NuGetRestoreSettings {
       Verbosity = NuGetVerbosity.Quiet 
    });
});


Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild("./Dependinator.sln", new MSBuildSettings {
        Configuration = configuration,
        Verbosity = Verbosity.Minimal,   
        ArgumentCustomization = args => args.Append("/nologo") 
        }  );
    }
    else
    {
      // Use XBuild
      XBuild("./Dependinator.sln", settings =>
        settings.SetConfiguration(configuration));
    }
});


Task("Build-Setup")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("\n");

    CopyFile(outputPath, setupPath);
    var version =  GetFullVersionNumber(setupPath);
  
    Information("Created: {0}", setupPath);
    Information("v{0}", version); 

    Information("\n\n");  
});


Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3("./**/bin/" + configuration + "/*Test.dll", new NUnit3Settings {
        NoResults = true,
        NoHeader = true,
        });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
