using System;
using System.Threading.Tasks;
using Build.Buildary;
using static Bullseye.Targets;
using static Build.Buildary.Runner;
using static Build.Buildary.Directory;
using static Build.Buildary.Path;
using static Build.Buildary.Shell;
using static Build.Buildary.Log;
using static Build.Buildary.File;

namespace Build
{
    static class Program
    {
        static Task Main(string[] args)
        {
            var options = ParseOptions(args);
            var tmpRepo = ExpandPath("./tmp-repo");
            var output = ExpandPath("./output");
            var commitAuthorEmail = Environment.GetEnvironmentVariable("COMMIT_AUTHOR_EMAIL");
            var sha = ReadShell("git rev-parse --short HEAD");
            
            Target("build-clean", () =>
            {
                if (DirectoryExists(output))
                {
                    DeleteDirectory(output);
                }
            });
            
            Target("build", DependsOn("build-clean"), () =>
            {
                RunShell($"dotnet run --project statik-project-doc/StatikProject/StatikProject.csproj build -o {output}");
            });
            
            Target("serve", () =>
            {
                RunShell("dotnet run --project statik-project-doc/StatikProject/StatikProject.csproj serve");
            });
            
            Target("deploy-clean", () =>
            {
                if (DirectoryExists(tmpRepo))
                {
                    DeleteDirectory(tmpRepo);
                }
            });
            
            Target("deploy", DependsOn("deploy-clean"), () =>
            {
                if (Travis.IsTravis)
                {
                    if (Travis.IsPullRequest)
                    {
                        Info("Pull request, skipping deploy...");
                        return;
                    }
                
                    var branch = Travis.Branch;
                    if (branch != "staging")
                    {
                        Info("Not on staging branch, skipping deploy...");
                        return;
                    }
                }

                if (string.IsNullOrEmpty(commitAuthorEmail))
                {
                    Failure("No COMMIT_AUTHOR_EMAIL, skipping deploy...");
                    Environment.Exit(1);
                }
                
                RunShell($"git clone git@github.com:qmlnet/qmlnet.github.io.git {tmpRepo}");
                
                using (ChangeDirectory(tmpRepo))
                {
                    RunShell("git checkout master || git checkout --orphan master");

                    RunShell("git rm -r .");
                    RunShell($"cp -r {output}/. {tmpRepo}");
                    RunShell("git add .");

                    if (string.IsNullOrEmpty(ReadShell("git status --porcelain")))
                    {
                        Info("No changes, skipping deploy...");
                        return;
                    }
                    
                    RunShell("git config user.name \"Travis CI\"");
                    RunShell($"git config user.email \"{commitAuthorEmail}\"");
                    RunShell($"git commit -m \"Deploy to GitHub Pages: {sha}\"");
                    RunShell($"git push origin master");
                }
            });
            
            Target("default", DependsOn("serve"));
            Target("ci", DependsOn("build", "deploy"));
            
            return Run(options);
        }
    }
}