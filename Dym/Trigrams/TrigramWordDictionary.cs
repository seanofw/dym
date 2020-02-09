using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dym.Collections;

namespace Dym.Trigrams
{
	/// <summary>
	/// A "dictionary" of known-good words, based on trigram shingles.  Each known-good
	/// word may be tagged with an object (definition, ID, or anything else you want to
	/// be able to find based on that word).  This dictionary includes many nested lookup
	/// tables to make searching for similar words to those in the dictionary as
	/// efficient as possible.
	/// 
	/// This class is not thread-safe across mutation; however, if you never
	/// mutate it, it will never self-mutate, so you can safely share a read-only
	/// instance of it across threads.
	/// 
	/// Note that this doesn't implement IDictionary{K, V} because it's not really a
	/// mapping in that sense:  It's a mapping, but a mapping only in the functional
	/// sense of y = F(x), since an infinite number of possible inputs may produce
	/// each output value.
	/// </summary>
	public class TrigramWordDictionary : ICollection<TrigramWord>
	{
		#region Private data

		/// <summary>
		/// A simple lookup table from normalized strings to TrigramWords, and their optional tag.
		/// </summary>
		private Dictionary<string, (TrigramWord, object?)> _dictionary = new Dictionary<string, (TrigramWord, object?)>();

		/// <summary>
		/// A lookup table from 24-bit trigrams to sets of all of the words that contain each trigram.
		/// </summary>
		private Dictionary<int, List<TrigramWord>> _gramLookup = new Dictionary<int, List<TrigramWord>>();

		#endregion

		#region ICollection properties

		/// <summary>
		/// A count of how many words are known in this dictionary.
		/// </summary>
		public int Count => _dictionary.Count;

		/// <summary>
		/// This dictionary is not read-only.
		/// </summary>
		public bool IsReadOnly => false;

		#endregion

		#region Construction and mutation

		/// <summary>
		/// Add a known TrigramWord to the dictionary, with a "tag" value for that word.
		/// This will fail (with an ArgumentException) if the word already exists in the dictionary.
		/// </summary>
		public void Add(TrigramWord word, object? tag)
		{
			_dictionary.Add(word.NormalizedText, (word, tag));
			
			foreach (int gram in word.Grams)
			{
				if (!_gramLookup.TryGetValue(gram, out List<TrigramWord>? set))
					_gramLookup.Add(gram, set = new List<TrigramWord>());
				set.Add(word);
			}
		}

		/// <summary>
		/// Add a known word to the dictionary, with a "tag" value for that word.
		/// This will fail (with an ArgumentException) if the word already exists in the dictionary.
		/// </summary>
		public void Add(string word, object? tag)
			=> Add(new TrigramWord(word), tag);

		/// <summary>
		/// Add a known TrigramWord to the dictionary, with a default "tag" value for that word.
		/// This will fail (with an ArgumentException) if the word already exists in the dictionary.
		/// </summary>
		public void Add(TrigramWord word)
			=> Add(word, default);

		/// <summary>
		/// Add a known word to the dictionary, with a default "tag" value for that word.
		/// This will fail (with an ArgumentException) if the word already exists in the dictionary.
		/// </summary>
		public void Add(string word)
			=> Add(new TrigramWord(word), default);

		/// <summary>
		/// Add many words to the dictionary, all with the given tag.
		/// </summary>
		/// <param name="words">The words to add.</param>
		/// <param name="tag">An optional tag value to associate with each word.</param>
		/// <param name="ignoreDuplicates">Whether to throw an ArgumentException for a
		/// word that already exists in the dictionary (false), or whether to simply ignore
		/// that word (true).</param>
		public void AddRange(IEnumerable<string> words, object? tag = null, bool ignoreDuplicates = false)
		{
			foreach (string word in words)
			{
				TrigramWord trigramWord = new TrigramWord(word);
				if (ignoreDuplicates && _dictionary.ContainsKey(trigramWord.NormalizedText))
					continue;
				Add(trigramWord, tag);
			}
		}

		/// <summary>
		/// Add many words to the dictionary, all with the given tag.
		/// </summary>
		/// <param name="words">The words to add.</param>
		/// <param name="tag">An optional tag value to associate with each word.</param>
		/// <param name="ignoreDuplicates">Whether to throw an ArgumentException for a
		/// word that already exists in the dictionary (false), or whether to simply ignore
		/// that word (true).</param>
		public void AddRange(IEnumerable<TrigramWord> words, object? tag = null, bool ignoreDuplicates = false)
		{
			foreach (TrigramWord word in words)
			{
				if (ignoreDuplicates && _dictionary.ContainsKey(word.NormalizedText))
					continue;
				Add(word, tag);
			}
		}

		/// <summary>
		/// Remove the given word from the dictionary.
		/// </summary>
		/// <param name="word">The word to remove.</param>
		/// <returns>True if the word existed, false if it did not.</returns>
		public bool Remove(string word)
			=> Remove(new TrigramWord(word));

		/// <summary>
		/// Remove the given TrigramWord from the dictionary.
		/// </summary>
		/// <param name="word">The word to remove.</param>
		/// <returns>True if the word existed, false if it did not.</returns>
		public bool Remove(TrigramWord word)
		{
			bool result = _dictionary.Remove(word.NormalizedText);
			if (!result) return false;

			foreach (int gram in word.Grams)
			{
				if (_gramLookup.TryGetValue(gram, out List<TrigramWord>? set))
				{
					set.Remove(word);
					if (set.Count <= 0)
						_gramLookup.Remove(gram);
				}
			}

			return true;
		}

		/// <summary>
		/// Delete all words from the dictionary.
		/// </summary>
		public void Clear()
		{
			_dictionary.Clear();
			_gramLookup.Clear();
		}

		#endregion

		#region Value tagging

		/// <summary>
		/// Retrieve the current tag value (which may be null) for the given TrigramWord.
		/// </summary>
		public object? GetTag(TrigramWord word)
		{
			if (_dictionary.TryGetValue(word.NormalizedText, out (TrigramWord, object?) pair))
				return pair.Item2;
			return default;
		}

		/// <summary>
		/// Retrieve the current tag value (which may be null) for the given word.
		/// </summary>
		public object? GetTag(string word)
			=> GetTag(new TrigramWord(word));

		/// <summary>
		/// Assign a new tag value to the given TrigramWord, which must already exist.
		/// </summary>
		public void SetTag(TrigramWord word, object? tag)
		{
			(TrigramWord, object?) pair = _dictionary[word.NormalizedText];
			_dictionary[pair.Item1.NormalizedText] = (pair.Item1, tag);
		}

		/// <summary>
		/// Assign a new tag value to the given word, which must already exist.
		/// </summary>
		public void SetTag(string word, object? tag)
			=> SetTag(new TrigramWord(word), tag);

		#endregion

		#region ICollection<TrigramWord> implementation

		/// <summary>
		/// Determine if the dictionary contains the given TrigramWord.
		/// </summary>
		public bool Contains(TrigramWord word)
			=> _dictionary.ContainsKey(word.NormalizedText);

		/// <summary>
		/// Determine if the dictionary contains the given word.
		/// </summary>
		public bool Contains(string word)
			=> _dictionary.ContainsKey(new TrigramWord(word).NormalizedText);

		/// <summary>
		/// Copy the entire dictionary of TrigramWords to the given array, in
		/// alphabetical order.
		/// </summary>
		public void CopyTo(TrigramWord[] array, int arrayIndex)
		{
			foreach (KeyValuePair<string, (TrigramWord, object?)> pair in _dictionary.OrderBy(p => p.Key))
				array[arrayIndex++] = pair.Value.Item1;
		}

		/// <summary>
		/// Enumerate the dictionary of TrigramWords, in alphabetical order.
		/// </summary>
		public IEnumerator<TrigramWord> GetEnumerator()
			=> _dictionary.OrderBy(p => p.Key).Select(p => p.Value.Item1).GetEnumerator();

		/// <summary>
		/// Enumerate the dictionary of TrigramWords, in alphabetical order.
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		#region Pattern matching

		/// <summary>
		/// Match the given "pattern" word against the dictionary, trying to find words in
		/// the dictionary as similar to it as possible.
		/// </summary>
		/// <param name="pattern">The pattern (user text, misspelled text, garbage text, etc.) to search for.</param>
		/// <param name="maximumWords">The maximum number of answers to return.</param>
		/// <param name="minimumSimilarity">The minimum allowed similarity of each matching word.</param>
		/// <param name="includeTags">Whether to include tag data for each matching word.</param>
		/// <returns>The list of matching words in the dictionary, if any.</returns>
		public List<TrigramMatch> Match(string pattern,
			int maximumWords = 100, double minimumSimilarity = 0.5, bool includeTags = true)
			=> Match(new TrigramWord(pattern), maximumWords, minimumSimilarity, includeTags);

		/// <summary>
		/// Match the given "pattern" word against the dictionary, trying to find words in
		/// the dictionary as similar to it as possible.
		/// </summary>
		/// <param name="pattern">The pattern (user text, misspelled text, garbage text, etc.) to search for.</param>
		/// <param name="maximumWords">The maximum number of answers to return.</param>
		/// <param name="minimumSimilarity">The minimum allowed similarity of each matching word.</param>
		/// <param name="includeTags">Whether to include tag data for each matching word.</param>
		/// <returns>The list of matching words in the dictionary, if any.</returns>
		public List<TrigramMatch> Match(TrigramWord pattern,
			int maximumWords = 100, double minimumSimilarity = 0.5, bool includeTags = true)
		{
			HashSet<TrigramWord> candidates = FindGoodCandidates(pattern);

			if (candidates.Count < maximumWords * 2)
				AddMediocreCandidates(pattern, candidates);

			List<TrigramMatch> orderedCandidates = SortCandidatesBySimilarity(candidates, pattern, maximumWords, minimumSimilarity);

			CutoffUnlikelyCandidates(orderedCandidates, 0.2);

			if (includeTags)
				PopulateTags(orderedCandidates);

			return orderedCandidates;
		}

		/// <summary>
		/// Fill in tags into the given set of matches.
		/// </summary>
		/// <param name="matches">The matches to populate with tags.</param>
		private void PopulateTags(IEnumerable<TrigramMatch> matches)
		{
			foreach (TrigramMatch match in matches)
			{
				match.Tag = _dictionary[match.TrigramWord!.NormalizedText].Item2;
			}
		}

		/// <summary>
		/// Remove any really unlikely candidates by getting rid of any answers whose score is
		/// more than 'limit' from the best candidate's score.  (i.e., if we get a candidate whose
		/// score is 0.9, and the limit is 0.2, remove anything less than 0.7, because we got
		/// such a good candidate at 0.9 that nobody will care about something that scored 0.5.)
		/// </summary>
		private static void CutoffUnlikelyCandidates(List<TrigramMatch> orderedCandidates, double limit)
		{
			if (orderedCandidates.Count <= 0)
				return;

			TrigramMatch best = orderedCandidates[0];
			double bestSimilarity = best.Similarity;

			int cutoff = 1;
			while (cutoff < orderedCandidates.Count && bestSimilarity - orderedCandidates[cutoff].Similarity <= limit)
			{
				cutoff++;
			}

			if (cutoff < orderedCandidates.Count)
				orderedCandidates.RemoveRange(cutoff, orderedCandidates.Count - cutoff);
		}

		/// <summary>
		/// Given a set of candidates, calculate their similarity values, and then partially
		/// sort them, up to at most 'maximumWords' results, avoiding any work we don't have
		/// to compute, such that the candidates with the highest similarity scores will come
		/// out first, and those with the lowest scores will just be discarded.
		/// </summary>
		/// <param name="candidates">The possible candidate words to compare and sort.</param>
		/// <param name="pattern">The pattern we're trying to find.</param>
		/// <param name="maximumWords">The maximum number of answers to return.</param>
		/// <param name="minimumSimilarity">The minimum allowed similarity of each matching word.</param>
		/// <returns>A list of the most similar candidates to the given word, ordered by how
		/// similar they are, which is guaranteed to be less than 'maximumWords' long, and
		/// which will not contain any candidates below 'minimumSimilarity'.</returns>
		private static List<TrigramMatch> SortCandidatesBySimilarity(HashSet<TrigramWord> candidates, TrigramWord pattern, int maximumWords, double minimumSimilarity)
		{
			static int Compare(TrigramMatch m, TrigramMatch n)
				=>    m.Similarity < n.Similarity ? -1
					: m.Similarity > n.Similarity ? +1
					: 0;

			// For each candidate word, calculate its similarity score, and toss out
			// any answers worse than 'minimumSimilarity'.
			List<TrigramMatch> rawMatches = new List<TrigramMatch>(candidates.Count);
			foreach (TrigramWord word in candidates)
			{
				double similarity = pattern.CalculateSimilarity(word);
				if (similarity < minimumSimilarity)
					continue;
				TrigramMatch match = new TrigramMatch(word, similarity);
				rawMatches.Add(match);
			}

			// Construct a Heap from the set of matches, ordered by their similarity scores.
			Heap<TrigramMatch> candidateHeap = new Heap<TrigramMatch>(rawMatches, Compare);

			// Repeatedly extract the maximum score from the heap until we either fill out
			// the entire allowable result set or we run out of viable answers.
			List<TrigramMatch> bestCandidates = new List<TrigramMatch>(rawMatches.Count);
			while (candidateHeap.Count > 0 && bestCandidates.Count < maximumWords)
			{
				TrigramMatch nextBest = candidateHeap.ExtractMax();
				bestCandidates.Add(nextBest);
			}

			return bestCandidates;
		}

		/// <summary>
		/// Find "good" candidate matches for the given pattern.  "Good" matches are those
		/// that share at least one trigram with the pattern, where that trigram is neither
		/// an initial or trailing trigram.  So if we have a pattern that consists of "hello",
		/// we'll find all the words that start with "he", end with "lo", or contain any of
		/// "hel", "ell", or "llo", which represents a "good" initial set of candidates to try.
		/// </summary>
		/// <param name="pattern">The pattern to search for.</param>
		/// <returns>A reasonable initial set of candidate matches.</returns>
		private HashSet<TrigramWord> FindGoodCandidates(TrigramWord pattern)
		{
			HashSet<TrigramWord> candidates = new HashSet<TrigramWord>();

			for (int i = 1; i < pattern.Grams.Length - 1; i++)
			{
				int gram = pattern.Grams[i];
				if (_gramLookup.TryGetValue(gram, out List<TrigramWord>? gramMatches))
				{
					foreach (TrigramWord word in gramMatches)
					{
						if (IsCandidateExtremelyUnlikely(pattern, word))
							continue;
						candidates.Add(word);
					}
				}
			}

			return candidates;
		}

		/// <summary>
		/// An extremely unlikely candidate is one that has substantially more or fewer
		/// trigrams than the given word.  So if the given word is 'helol', the possible
		/// match 'heliotropic' is extremely unlikely because it's so much longer than the
		/// given word.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsCandidateExtremelyUnlikely(TrigramWord pattern, TrigramWord word)
			=> (word.NormalizedText.Length > pattern.NormalizedText.Length * 2
				|| pattern.NormalizedText.Length > word.NormalizedText.Length * 2);

		/// <summary>
		/// If we can't find enough possible candidates looking for "good" matches, then
		/// fill out the set of candidates with "mediocre" matches by looking at initial
		/// and trailing trigrams as well.  So if the pattern is "hello", and we didn't find
		/// any words with "he" or "hel" or "ell" or "llo" or "lo", then we'll add in all the
		/// words that start with "h" and end with "o", because there's still a small
		/// chance one of those words could be the best match.
		/// </summary>
		/// <param name="pattern">The pattern to search for.</param>
		/// <param name="candidates">The candidate collection to add to.</param>
		private void AddMediocreCandidates(TrigramWord pattern, HashSet<TrigramWord> candidates)
		{
			int gram = pattern.Grams[0];
			if (_gramLookup.TryGetValue(gram, out List<TrigramWord>? gramMatches))
			{
				foreach (TrigramWord word in gramMatches)
				{
					if (IsCandidateExtremelyUnlikely(pattern, word))
						continue;
					candidates.Add(word);
				}
			}

			gram = pattern.Grams[pattern.Grams.Length - 1];
			if (_gramLookup.TryGetValue(gram, out gramMatches))
			{
				foreach (TrigramWord word in gramMatches)
				{
					if (IsCandidateExtremelyUnlikely(pattern, word))
						continue;
					candidates.Add(word);
				}
			}
		}

		#endregion
	}
}
