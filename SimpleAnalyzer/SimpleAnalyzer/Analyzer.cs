using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SimpleAnalyzer.Walkers;

namespace SimpleAnalyzer
{
    public class Analyzer
    {
        class Options
        {
            [Option('d', "directory", Required = true, HelpText = "Directory for search with pattern *.")]
            public string Directory { get; set; }

            [Option('r', "recursive", Default = false, HelpText = "Option to search recursively in directory")]
            public bool Recursive { get; set; }
        }

        private static void Main(string[] args)
        {
            var parsedOptions = (Parser.Default.ParseArguments<Options>(args) as Parsed<Options>)?.Value;
            if (parsedOptions == null)
                return;

            var analyzedFilesCount = 0;

            foreach (var fileName in Directory.EnumerateFiles(parsedOptions.Directory, "*.cs", parsedOptions.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                var result = AnalyzeCode(File.ReadAllText(fileName)).ToList();
                ++analyzedFilesCount;

                if (result.Any())
                {
                    foreach (var symbol in result)
                    {
                        Console.WriteLine($"In file {fileName} found potential issue for symbol {symbol}");
                    }
                    Console.WriteLine();
                }
            }

            Console.WriteLine($"Analyzed {analyzedFilesCount} file(s)");
        }

        public static IEnumerable<ISymbol> AnalyzeCode(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("CoreFX", new[] {syntaxTree});

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var root = syntaxTree.GetRoot();

            var locksAndObjectCreationsWalker = new LocksAndObjectCreationsWalker(semanticModel);
            locksAndObjectCreationsWalker.Visit(root);
            var newObjectsAndLocks = locksAndObjectCreationsWalker.NewObjectsAndLocks;

            if (newObjectsAndLocks.Count > 0)
            {

                var lockWalker = new NonLockedObjectsWalker(newObjectsAndLocks, semanticModel);

                lockWalker.Visit(root);
                return lockWalker.PotentialIssuesFound;
            }

            return Enumerable.Empty<ISymbol>();
        }
    }
}