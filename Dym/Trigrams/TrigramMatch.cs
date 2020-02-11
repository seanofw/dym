namespace Dym.Trigrams
{
	/// <summary>
	/// A match of a pattern to a known word.
	/// </summary>
	public class TrigramMatch
	{
		#region Properties

		/// <summary>
		/// The matching word that was found.
		/// </summary>
		public string Word { get; }

		/// <summary>
		/// How similar this match is to the given pattern, on a range of [0, 1], where
		/// '0' is "nothing in common" and '1' is "perfect match."
		/// </summary>
		public double Similarity { get; }

		/// <summary>
		/// Optional tag data associated with the found word.
		/// </summary>
		public object? Tag { get; }

		/// <summary>
		/// The actual TrigramWord object that was found.
		/// </summary>
		public TrigramWord TrigramWord { get; }

		#endregion

		#region Construction and methods

		/// <summary>
		/// Construct a new TrigramMatch.
		/// </summary>
		/// <param name="word">The matching word that was found.</param>
		/// <param name="similarity">How similar that word is, on a range of [0, 1].</param>
		public TrigramMatch(TrigramWord word, double similarity, object? tag)
		{
			Word = word.Text;
			TrigramWord = word;
			Similarity = similarity;
			Tag = tag;
		}

		/// <summary>
		/// Add a tag to this match.
		/// </summary>
		public TrigramMatch WithTag(object? tag)
			=> new TrigramMatch(TrigramWord, Similarity, tag);

		/// <summary>
		/// Convert this match to a string, for easy debugging.
		/// </summary>
		public override string ToString()
			=> $"{Word}: {Similarity:0.000}";

		#endregion
	}
}
