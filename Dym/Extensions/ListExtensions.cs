using System.Collections.Generic;

namespace Dym.Extensions
{
	public static class ListExtensions
	{
		public static void Uniq<T>(this List<T> list)
		{
			if (list.Count <= 1) return;

			T last = list[0];
			int dest = 1;
			for (int src = 1; src < list.Count; src++)
			{
				T current = list[src];
				if (!EqualityComparer<T>.Default.Equals(last, current))
					list[dest++] = last = current;
			}

			if (dest < list.Count)
				list.RemoveRange(dest, list.Count - dest);
		}
	}
}
