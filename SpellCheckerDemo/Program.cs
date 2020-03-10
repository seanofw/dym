using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Clockwerk.Dym.Trigrams;

namespace Clockwerk.Dym.SpellCheckerDemo
{
	public static class Program
	{
		static void Main(string[] args)
		{
			// Load 'dictionary.txt'.
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			string[] words = LoadDictionary("dictionary.txt");
			stopwatch.Stop();
			TimeSpan loadTime = stopwatch.Elapsed;

			// Make a WordDictionary from the words in 'dictionary.txt'.
			stopwatch = new Stopwatch();
			stopwatch.Start();
			TrigramWordDictionary dictionary = new TrigramWordDictionary();
			dictionary.AddRange(words, ignoreDuplicates: true);
			stopwatch.Stop();
			TimeSpan buildTime = stopwatch.Elapsed;

			Console.WriteLine("Loaded 'dictionary.txt' in {0:0.0} msec, and built database in {1:0.0} msec.",
				loadTime.TotalMilliseconds, buildTime.TotalMilliseconds);

			// Now spell-check whatever command-line arguments we were given.
			stopwatch = new Stopwatch();
			foreach (string arg in args)
			{
				stopwatch.Start();
				List<TrigramMatch> matches = dictionary.Match(arg, maximumWords: 10, includeTags: false);
				stopwatch.Stop();
				if (matches.Count >= 1 && matches[0].Similarity >= 1.0)
					continue;	// This word has a perfect match.

				// This word is misspelled, so here are some suggested alternatives.
				Console.WriteLine("Misspelled word '{0}'.  Suggestions:", arg);
				foreach (TrigramMatch match in matches)
					Console.WriteLine(" - {0} (score {1:0.00})", match.Word, match.Similarity * 10);
			}

			TimeSpan matchTime = stopwatch.Elapsed;
			TimeSpan averageMatchTime = matchTime / Math.Max(args.Length, 1);
			Console.WriteLine("Average time to match each word: {0:0.00} msec.", averageMatchTime.TotalMilliseconds);
		}

		private static string[] LoadDictionary(string dictionaryFilename)
		{
			// Start at the '.exe' and search up the directory tree from there for the dictionary file.
			string dictionaryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string fullDictionaryPath;
			while (!File.Exists(fullDictionaryPath = Path.Combine(dictionaryPath, dictionaryFilename)))
			{
				if (string.IsNullOrEmpty(dictionaryPath))
				{
					Console.Error.WriteLine("Cannot find '{0}'.", dictionaryFilename);
					Environment.Exit(-1);
				}
				dictionaryPath = Path.GetDirectoryName(dictionaryPath);
			}

			// Found it, so load it.  We assume each word is on its own line, and we discard blank lines.
			string[] words = File.ReadAllLines(fullDictionaryPath)
				.Where(w => !string.IsNullOrWhiteSpace(w))
				.Select(w => w.Trim())
				.ToArray();

			return words;
		}
	}
}
