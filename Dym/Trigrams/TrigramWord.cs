using System;
using System.Runtime.CompilerServices;

namespace Clockwerk.Dym.Trigrams
{
	/// <summary>
	/// Create a "word" object based on the trigrams found in the given string,
	/// after "normalizing" it to remove punctuation, whitespace, and accent marks,
	/// and case-fold any alphabetic letters.  This is immutable once constructed.
	/// </summary>
	public class TrigramWord : IEquatable<TrigramWord>, IComparable<TrigramWord>, IComparable
	{
		#region Properties and fields

		/// <summary>
		/// The origianl text.
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// The text we actually compare with, after "normalization."
		/// </summary>
		internal string NormalizedText { get; }

		/// <summary>
		/// The trigrams, sorted for fast comparison.
		/// </summary>
		internal int[] Trigrams { get; }

		/// <summary>
		/// The hash code of this object, lazily precomputed.
		/// </summary>
		private int HashCode { get => _hashCode ??= NormalizedText.GetHashCode(); }
		private int? _hashCode;

		#endregion

		#region Construction

		/// <summary>
		/// Construct a new TrigramWord based on the given string.
		/// </summary>
		/// <param name="text">The string to base this TrigramWord off of.  This must
		/// use only Latin characters or punctuation; non-Latin characters (CJK ideographics,
		/// Cyrillic, Arabic, Hebrew, etc.) will simply be discarded.  You may also use tabs or
		/// newlines (CR or LF or CRLF) to separate multiple fields, if you are creating a
		/// "word" object that consists of many ordered substrings (such as a name+address+city
		/// tuple, if you're trying to match customer accounts, for example).</param>
		public TrigramWord(string text)
		{
			if (string.IsNullOrEmpty(text))
				throw new ArgumentNullException(nameof(text));

			NormalizedText = Normalize(text);
			if (string.IsNullOrEmpty(NormalizedText))
				throw new ArgumentException("Text in exclusively non-Latin languages is not supported.", nameof(text));

			Text = text;
			Trigrams = CalculateGrams(NormalizedText);
			Array.Sort(Trigrams);
		}

		#endregion

		#region Similarity scoring

		/// <summary>
		/// Calculate the similarity between this TrigramWord and another TrigramWord,
		/// on a scale of [0, 1], where 0 is "nothing in common" and 1 is "exact match."
		/// This runs in O(n + m) time, where 'n' and 'm' are the lengths of each word.
		/// </summary>
		/// <param name="other">The other word to compare against.</param>
		/// <returns>A similarity score in the range of [0, 1].</returns>
		public double CalculateSimilarity(TrigramWord other)
		{
			unchecked
			{
				if (other == null!)
					throw new ArgumentNullException(nameof(other));

				// This is really just calculating the intersection of Grams and other.Grams,
				// counting up how many grams each collection shares.  But because they're both
				// sorted and pure integers, we can do this much more efficiently than something
				// like HashSet<string>.IntersectWith().  Note that unlike simple sets, this
				// correctly calculates duplicates; if the same gram is featured multiple times
				// in a given set, it will be expected to be found multiple times in the other
				// set, too.

				int[] g1 = Trigrams, g2 = other.Trigrams;
				uint i1 = 0, i2 = 0;
				uint e1 = (uint)g1.Length, e2 = (uint)g2.Length;
				uint match = 0;

				// Spin over each set, comparing the current value of one to the current value
				// of the other; when they don't match, "fast forward" over non-matching elements
				// until they do again.  This is partially-unrolled for speed, and inside-out:
				// instead of counting up differences and subtracting those from the total, this
				// counts up matches and can then early-out as soon as it runs out of significant
				// content.
				if (i1 < e1 && i2 < e2)
				{
					int a = g1[i1], b = g2[i2];
					while (true)
					{
						// First copy.
						if (a == b)
						{
							match += 2;
							if (++i1 >= e1 || ++i2 >= e2) break;
							a = g1[i1];
							b = g2[i2];
						}
						else if (a < b)
						{
							if (++i1 >= e1) break;
							a = g1[i1];
						}
						else
						{
							if (++i2 >= e2) break;
							b = g2[i2];
						}

						// Second copy.
						if (a == b)
						{
							match += 2;
							if (++i1 >= e1 || ++i2 >= e2) break;
							a = g1[i1];
							b = g2[i2];
						}
						else if (a < b)
						{
							if (++i1 >= e1) break;
							a = g1[i1];
						}
						else
						{
							if (++i2 >= e2) break;
							b = g2[i2];
						}

						// Third copy.
						if (a == b)
						{
							match += 2;
							if (++i1 >= e1 || ++i2 >= e2) break;
							a = g1[i1];
							b = g2[i2];
						}
						else if (a < b)
						{
							if (++i1 >= e1) break;
							a = g1[i1];
						}
						else
						{
							if (++i2 >= e2) break;
							b = g2[i2];
						}

						// Fourth copy.
						if (a == b)
						{
							match += 2;
							if (++i1 >= e1 || ++i2 >= e2) break;
							a = g1[i1];
							b = g2[i2];
						}
						else if (a < b)
						{
							if (++i1 >= e1) break;
							a = g1[i1];
						}
						else
						{
							if (++i2 >= e2) break;
							b = g2[i2];
						}
					}
				}

				// The match scores are naturally biased toward the bottom of [0, 1], so the square
				// root un-biases them back toward something closer to a linear distribution.
				return Math.Sqrt((double)match / (e1 + e2));
			}
		}

		#endregion

		#region Normalization

		private const char NullChar = ' ';
		private const char FieldSeparator = '"';
		private const char Whitespace = '/';
		private const char TerminatorPunctuation = '.';
		private const char JoinerPunctuation = '-';
		private const char Alphanumeric = '0';
		private const char LastAscii = '\x7F';

		private static readonly char[] _remapTable =
		{
			'/', '/', '/', '/', '/', '/', '/', '/', '/', '"', '"', '/', '/', '"', '/', '/',
			'/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/', '/',
			'/', '.', '.', '.', '.', '.', '.', '.', '.', '.', '-', '-', '.', '-', '.', '-',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '.', '.', '-', '.', '.',
			'.', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
			'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '.', '-', '.', '-', '-',
			'.', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
			'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '.', '-', '.', '-', '/',
		};

		/// <summary>
		/// Transform the given text into a "normalized" form of itself, by:
		/// 
		///    - Stripping off accent marks
		///    - Case-folding letters to uppercase
		///    - Compacting all whitespace except newlines to single '/' characters
		///    - Compacting newlines (cr/lf) or tabs to single ' ' characters (i.e., as field separators)
		///    - Replacing all punctuation with either '.' or '-'
		///    - Removing any punctuation next to whitespace
		///    - Removing any whitespace at the start or end of the string
		///    - Removing anything else that isn't letters or digits
		/// 
		/// We perform this "cleanup" quickly, using table lookups and a state machine,
		/// all of which runs in O(n) time (except for the 'Stripping off accent marks'
		/// part, which uses String.Normalize(), but only if it absolutely has to).
		/// 
		/// This works well for Latin-based inputs, but it basically destroys the input
		/// for any non-Latin scripts (CJK ideographics, Cyrillic, Arabic, Hebrew, etc.).
		///
		/// <b>Warning:</b> This method is intended to be <i>internal</i>. Its behavior <i>may</i> change in a future
		/// version. Anything that relies on it <i>must not</i> make assumptions about what the method does or how it
		/// does it. For more information take a look at the pull request https://github.com/seanofw/dym/pull/1.
		/// </summary>
		/// <param name="text">The text to "normalize."</param>
		/// <returns>The "normalized" input string.</returns>
		public static string Normalize(string text)
		{
			if (!IsAscii(text))
				text = text.Normalize(System.Text.NormalizationForm.FormD);

			char[] buffer = new char[text.Length];
			int src = 0, dest = 0, end = text.Length;
			char lastch = '\0';

			while (src < end)
			{
				char ch = text[src++];
				ch = (ch <= LastAscii ? _remapTable[ch] : Whitespace);
				if (ch >= Alphanumeric)
					lastch = buffer[dest++] = ch;
				else if (ch == Whitespace)
				{
					if (dest > 0)
					{
						if (lastch >= Alphanumeric)
							lastch = buffer[dest++] = Whitespace;
						else if (lastch > FieldSeparator)
							lastch = buffer[dest - 1] = Whitespace;
					}
				}
				else if (ch == FieldSeparator)
				{
					if (dest > 0)
					{
						if (lastch >= Alphanumeric)
							lastch = buffer[dest++] = FieldSeparator;
						else lastch = buffer[dest - 1] = FieldSeparator;
					}
				}
				else
				{
					if (lastch >= Alphanumeric)
						lastch = buffer[dest++] = ch;
				}
			}

			if (dest > 0 && lastch < Alphanumeric)
				dest--;

			return new string(buffer, 0, dest);
		}

		/// <summary>
		/// Determine whether the given input text is entirely composed of
		/// ASCII characters.
		/// </summary>
		private static bool IsAscii(string text)
		{
			for (int i = 0; i < text.Length; i++)
				if (text[i] >= 128)
					return false;
			return true;
		}

		/// <summary>
		/// Calculate an array of trigrams based on the given text input.  The trigrams
		/// will be represented as 18-bit integers, with each input character collapsed
		/// into a single 6-bit component.  We know that we can collapse them to 6 bits
		/// each, because after normalization, the input will only use ASCII characters
		/// in the range of [32, 90], which is a mere 59 code points (and actually, we
		/// don't even use all of those:  In reality, only 26+10+5 = 41 total code points
		/// will ever be used).
		/// </summary>
		/// <param name="text">The text to generate a set of trigram values from.</param>
		/// <returns>The set of generated trigrams, which include "tail codes" for
		/// the start and end of the text.</returns>
		private int[] CalculateGrams(string text)
		{
			int[] grams = new int[text.Length + 2];
			char c0, c1, c2;

			if (text.Length == 1)
			{
				grams[0] = MakeGram(NullChar, NullChar, c0 = text[0]);
				grams[1] = MakeGram(NullChar, c0, NullChar);
				grams[2] = MakeGram(c0, NullChar, NullChar);
			}
			else if (text.Length == 2)
			{
				grams[0] = MakeGram(NullChar, NullChar, c0 = text[0]);
				grams[1] = MakeGram(NullChar, c0, c1 = text[1]);
				grams[2] = MakeGram(c0, c1, NullChar);
				grams[3] = MakeGram(c1, NullChar, NullChar);
			}
			else
			{
				grams[0] = MakeGram(NullChar, NullChar, c0 = text[0]);
				grams[1] = MakeGram(NullChar, c0, c1 = text[1]);
				int n = 0;
				while (n < text.Length - 2)
				{
					c2 = text[n + 2];
					grams[n++ + 2] = MakeGram(c0, c1, c2);
					c0 = c1;
					c1 = c2;
				}
				grams[n++ + 2] = MakeGram(c0, c1, NullChar);
				grams[n + 2] = MakeGram(c1, NullChar, NullChar);
			}

			return grams;
		}

		/// <summary>
		/// Make a single trigram by mashing together the only meaningful bits of each
		/// of the three given characters.  Returns a single 18-bit value.  Inlined for speed.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int MakeGram(ushort c1, ushort c2, ushort c3)
			   => ((c1 - 32) << 12)
				| ((c2 - 32) << 6)
				|  (c3 - 32);

		#endregion

		#region Equality and hash codes

		/// <summary>
		/// Compare this TrigramWord against another, for equality.  This compares
		/// their normalized forms (i.e., what the pattern-matcher actually matches
		/// against), not the original source text provided.
		/// </summary>
		public override bool Equals(object? obj)
			=> Equals(obj as TrigramWord);

		/// <summary>
		/// Compare this TrigramWord against another, for equality.  This compares
		/// their normalized forms (i.e., what the pattern-matcher actually matches
		/// against), not the original source text provided.
		/// </summary>
		public bool Equals(TrigramWord? other)
			=> ReferenceEquals(other, this)
				|| !ReferenceEquals(other, null)
					&& NormalizedText == other.NormalizedText;

		/// <summary>
		/// Get a hash code suitable for using this TrigramWord as a dictionary key.
		/// </summary>
		public override int GetHashCode()
			=> HashCode;

		/// <summary>
		/// Compare this TrigramWord against another, for equality.  This compares
		/// their normalized forms (i.e., what the pattern-matcher actually matches
		/// against), not the original source text provided.
		/// </summary>
		public static bool operator ==(TrigramWord a, TrigramWord b)
			=> ReferenceEquals(a, null) ? ReferenceEquals(b, null) : a.Equals(b);

		/// <summary>
		/// Compare this TrigramWord against another, for equality.  This compares
		/// their normalized forms (i.e., what the pattern-matcher actually matches
		/// against), not the original source text provided.
		/// </summary>
		public static bool operator !=(TrigramWord a, TrigramWord b)
			=> ReferenceEquals(a, null) ? !ReferenceEquals(b, null) : !a.Equals(b);

		#endregion

		#region Order comparison

		/// <summary>
		/// Compare these two words for lexicographic ordering (based on their normalized text).
		/// Null always comes before any real data.
		/// </summary>
		public int CompareTo(TrigramWord? other)
			=> ReferenceEquals(other, null) ? +1
				: NormalizedText.CompareTo(other.NormalizedText);

		/// <summary>
		/// Compare this word to another, for lexicographic ordering (based on their normalized text).
		/// Null always comes before any real data.
		/// </summary>
		public int CompareTo(object? other)
			=> CompareTo(other as TrigramWord);

		#endregion

		/// <summary>
		/// Convert this to a string representation (which is just the original text
		/// provided).
		/// </summary>
		public override string ToString()
			=> Text;
	}
}