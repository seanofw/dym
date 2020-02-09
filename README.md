# Dym

Version 1.0
Copyright (C) 2020 by Sean Werkema
MIT Open-Source License

-----------------------------------------------------------------------------

## What's Dym?

Dym is short for "Did You Mean?" and is a .NET Core library for fuzzy-matching
unknown inputs to a dictionary of known words or phrases.  The current
implementation provides a high-speed trigram/shingle matcher, which is a good
technique for matching very noisy inputs.

Some scenarios where Dym can be useful:

  - **Spell checking**:  "Misspelled word 'programmr'.  Suggestions: programmer, programer, programme, program, programmed."
  - **Address matching**:  "John Smith Co Inc / 123 Main St. / Anytwn PA" ==> "John Smith Incorporated / 123 Main Street / Anytown, PA 18641"
  - **Name searching**:  "carmen mendez" ==> "Carmine Menendez"
  - **Command-line help**: `Unknown command-line option '--stats'; did you mean '--status'?`

Dym is also _fast_:  It uses not only efficient data structures and efficient
algorithms, but also has many micro-optimizations too.  For example, for
a 126,000-word English dictionary, setup takes only about 300 msec on a
typical PC, and Dym can then find suggestions for a misspelled word in under
10 msec each!

(Finally, how do you pronounce 'Dym'?  Like _dime_, and rhymes with _time_.)

-----------------------------------------------------------------------------

## Basic Usage

Dym is a NuGet library, and currently has builds for .NET Core 3.0.  It has
no dependencies:  Just install it into your project, and then add a suitable
`using` directive.

### Setup

First, you need to construct a "dictionary" of all of your known-good words:

```c#
using Dym.Trigrams;

string[] knownOptions = { "status", "push", "clone", "merge" };

var dictionary = new TrigramWordDictionary(knownOptions);
```

### Querying

You can then ask Dym to answer what it thinks a "best match" is for an unknown
word:

```c#
List<Match> matches = dictionary.Match("stats");

foreach (var match in matches)
{
    Console.WriteLine("did you mean '{0}'?", match.Word);
}
```

The returned set of `Match` objects will always be in the order of best-to-worst, so
if you only want the "best" match, that's simply `matches.FirstOrDefault()`:

```c#
Match bestMatch = dictionary.Match("stats").FirstOrDefault();

if (bestMatch != null)
{
    Console.WriteLine("did you mean '{0}'?", bestMatch.Word);
}
```

There are options to constrain the search:

```c#
List<Match> matches = dictionary.Match("stats", maximumWords: 10, minimumScore: 0.7);
```

A `Match` object that's returned includes the `string Word` that Dym found, as well
as a `double Similiarity` score (in a range of 0.0 to 1.0) describing how similar
the match is, and an optional `object Tag` that can be attached to each word in the
dictionary.

### Tags

Tags allow you to associate arbitrary data with each word or phrase in the dictionary;
for example, if you are using Dym to search for customer names, you might want to
attach each customer ID to their name:

```c#
var dictionary = new TrigramWordDictionary();

foreach (var customer in database.Customers)
{
    dictionary.Add(customer.Name, customer.ID);
}
```

Now, with the ID attached to each customer, you can get that ID back directly when
you perform a match:

```c#
List<Match> matches = dictionary.Match("Jon Smythe", includeTags: true);
forach (var match in Matches)
{
    Console.WriteLine("Found customer named '{0}' with ID '{1}'.", match.Word, match.Tag);
}
```

### Similarity testing

You can use the `TrigramWord` class to directly answer how similar one word or phrase
is to another, without performing a search:

```c#
var knownWord = new TrigramWord("abc");
var userInput = new TrigramWord("abcd");

double similiarity = knownWord.CalculateSimilarity(userInput);

  ==> 0.7385
```

### Field separators

Dym is smart enough to know that when you include `\t` or `\r` or `\n` in a word,
either in the dictionary or in the input, that you intend for the "word" to really
have multiple fields.  This is useful, for example, for address searching; the
input might look something like this:

```c#
string databaseContact1 =
@"John Smith Incorporated
123 Main Street
Anytown, PA 18641";

string databaseContact2 = ...

...

var dictionary = new TrigramsDictionary(databaseContacts);

string userInput =
@"John Smith Co Inc
123 Main St.
Anytwn PA";

List<Match> matches = dictionary.Match(userInput);
```

Dym will respect the newlines as boundaries between fields; but Dym also
understands that field data is sometimes bad (for example, a city name
might appear in a `Street` field), and so it will use the field boundaries as
_strong suggestions_ to help the match, but not as hard requirements.

-----------------------------------------------------------------------------

## How it works

There are a few major stages during Dym's execution:

### Dictionary search

1. **Setup**.

   During the setup phase, a collection of _trigrams_ is computed for each
   word provided.  Each trigram is simply a set of three adjacent letters
   in the input word, plus special trigrams for the letters at the start and
   end.  For example, `abcd` has these trigrams:

    - `  a`, ` ab` (partial initial trigrams)
    - `abc`, `bcd` (whole trigrams)
    - `cd `, `d  ` (partial final trigrams)

   These are then stored in lookup tables, so that if an input word also
   contains the trigram `abc`, we know that `abcd` is a possible match.

2. **Search**.

   When `Match()` is called, it calculates the trigrams for the input string,
   and then follows this sequence:

   1. Find the "best candidates" first.  The "best candidates" are any words
      in the dictionary that have whole trigrams that match the whole trigrams
      of the input word, but that aren't substantially longer or shorter than
      the input (more than twice as long or less than half as long).

   2. Find "mediocre candidates" if necessary.  If we didn't find enough "best
      candidates," repeat the search, but this time, search on partial trigrams
      too.

   3. Assign scores by using `CalculateSimilarity()` to compare each candidate
      word to the input word, and then _partially_ sort the results by score,
      keeping only those that are high-scoring answers.

   4. Discard any extremely-low-scoring answers, and then return the result.

### Calculating similarity

The core of a search really boils down to how `CalculateSimilarity()` works,
which is very simple:  Compare the sets of trigrams, count how many trigrams
match between them, and then take the square root of the result.  So, for
example, comparing "abc" and "abcd", we have these sets of trigrams:

```
abc:   a  ab  abc  bc  c
abcd:  a  ab  abc         bcd  cd  d
```

In total, there are 11 trigrams between the two strings.  6 of those trigrams
are identical, and 5 are different:  So the "similarity score" for these two
words is `sqrt(6 / 11)`, or about 0.7385, which implies they are fairly similar
(and they are), but not the same.

### Normalization

Dym _normalizes_ input words, which is to say, it applies the operations below
to all inputs before performing any kind of analysis or comparison:

  - Stripping off accent marks
  - Case-folding letters to uppercase
  - Compacting all whitespace except newlines to single `/` characters
  - Compacting newlines (cr/lf) or tabs to single ` ` characters (i.e., as field separators)
  - Replacing all punctuation with either `.` or `-`
  - Removing any punctuation next to whitespace
  - Removing any whitespace at the start or end of the string
  - Removing anything else that isn't letters or digits

This works well for Latin-based inputs, but it basically destroys the input
for any non-Latin scripts (CJK ideographics, Cyrillic, Arabic, Hebrew, etc.).
Dym is not currently suited for use with languages that use non-Latin scripts.

-----------------------------------------------------------------------------

## Future directions

Dym is very useful and very fast, and it works well in many real-world
scenarios, but it isn't perfect.  Some possible future enhancements might
include:

  - Internationalization and non-Latin scripts.
  - Adding edit distance as a possible search metric.
  - Adding shorter and longer _k_-grams than just trigrams.
  - Adding other kinds of statistical search metrics.
  - Support for case matching (i.e., preferring "AMP" over "amp" when matching "AMG").
  - Numerical prioritization (i.e., preferring "29 Oak Ln" over "16 Oak Lane" when matching "29 Oak Lane").
  - Better punctuation analysis (i.e., preferring "can't" over "man" when given "man't").

-----------------------------------------------------------------------------

## Contact and questions

Dym was initially written in February 2020, intentionally open-source this
time, based on concepts Sean Werkema had devised and implemented in three (!)
separate previous closed-source libraries for various customers.

For questions or support, please file a GitHub issue.

