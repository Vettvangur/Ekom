# Don't use - Azure DevOps handles publishing
dotnet pack .\Ekom\ -c Release --include-symbols -o .
dotnet pack .\Ekom.Web\ -c Release --include-symbols -o .
# $pkg = gci *.nupkg 
# nuget push $pkg -Source https://www.nuget.org/api/v2/package -NonInteractive
pause
