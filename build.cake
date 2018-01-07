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

// Define paths.
var name = "Dependinator";

var solutionPath = $"./{name}.sln";
var outputPath = $"{name}/bin/{configuration}/{name}.exe";
var setupPath = $"{name}Setup.exe";


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories($"./**/obj/{configuration}");
    CleanDirectories($"./**/bin/{configuration}");

    if (FileExists(setupPath))
    { 
        DeleteFile(setupPath);
    }
});


Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(solutionPath, new NuGetRestoreSettings {
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
      MSBuild(solutionPath, new MSBuildSettings {
        Configuration = configuration,
        Verbosity = Verbosity.Minimal,   
        ArgumentCustomization = args => args.Append("/nologo") 
        }  );
    }
    else
    {
      // Use XBuild
      XBuild(solutionPath, settings =>
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
    NUnit3($"./**/bin/{configuration}/*Test.dll", new NUnit3Settings {
        NoResults = true,
        NoHeader = true,
        });
});


Task("Default")
    .IsDependentOn("Run-Unit-Tests");



//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
