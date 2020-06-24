nuget restore .
nuget pack .\Ekom.Web\ -build -Symbols -SymbolPackageFormat snupkg -Properties Configuration=Release
nuget pack .\Ekom\ -build -Symbols -SymbolPackageFormat snupkg -Properties Configuration=Release
# $pkg = gci *.nupkg 
# nuget push $pkg -Source https://www.nuget.org/api/v2/package -NonInteractive
