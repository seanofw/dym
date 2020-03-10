using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Clockwerk.Dym.Collections
{
	/// <summary>
	/// This class implements the Heap data structure, which provides
	/// an efficient "ExtractMax" operation that runs in O(lg n) time.
	/// This implements most of a reasonable ICollection{T} implementation
	/// so you can treat it like a normal collection object.
	/// </summary>
	public class Heap<T> : ICollection<T>
	{
		#region Private data

		private T[] _items;
		private int _heapSize;

		#endregion

		#region Public properties

		/// <summary>
		/// The comparison function used to compare two T's for ordering
		/// (whatever IComparable{T} provides is used by default, but you can
		/// provide your own.
		/// </summary>
		public Func<T, T, int> Compare { get; }

		/// <summary>
		/// The current size of the heap (number of items on it).
		/// </summary>
		public int Count => _heapSize;

		/// <summary>
		/// This is a mutatable collection.
		/// </summary>
		public bool IsReadOnly => false;

		#endregion

		#region Construction

		/// <summary>
		/// Make a new, empty heap, using IComparable{T} as the comparison function.
		/// </summary>
		public Heap()
		{
			_items = new T[0];
			Compare = UseIComparable();
		}

		/// <summary>
		/// Make a new, empty heap, using the provided comparison function.
		/// </summary>
		public Heap(Func<T, T, int> compare)
		{
			_items = new T[0];
			Compare = compare;
		}

		/// <summary>
		/// Make a new heap populated from the given set of items, optionally
		/// using the given comparison function.  This runs in O(n) time.
		/// </summary>
		/// <param name="items">The items to initially populate the heap.</param>
		/// <param name="compare">The comparison function to use; if null, the
		/// IComparable{T} implementation will be used instead.</param>
		public Heap(IEnumerable<T> items, Func<T, T, int>? compare = null)
		{
			_items = items.ToArray();
			Compare = compare ?? UseIComparable();
			BuildHeap();
		}

		/// <summary>
		/// Get a static Compare(T a, T b) function from the IComparable{T}
		/// interface, or throw an exception if we can't.
		/// </summary>
		private static Func<T, T, int> UseIComparable()
		{
			if (typeof(T).GetInterfaces().Contains(typeof(IComparable<T>)))
			{
				static int AutoCompare(T a, T b)
					=> ((IComparable<T>)a!).CompareTo(b);
				return AutoCompare;
			}

			throw new ArgumentException($"'{typeof(T).FullName}' doesn't implement IComparable<T>, and no custom comparison function was provided.");
		}

		#endregion

		#region Internal heap mechanics

		/// <summary>
		/// Swap the items at the given positions in the heap array.  Heavily inlined
		/// because we do it a lot.  Runs in O(1) time.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Exchange(int i, int j)
		{
			T temp = _items[i];
			_items[i] = _items[j];
			_items[j] = temp;
		}

		/// <summary>
		/// Perform the 'heapify' operation around the item at index 'i', which is to
		/// say, bubble it up the array until it is the root of a valid sub-heap.  Runs
		/// in O(lg n) time.
		/// </summary>
		private void Heapify(int i)
		{
			while (true)
			{
				int l = i * 2 + 1;
				int r = i * 2 + 2;

				int largest = i;
				if (l < _heapSize && Compare(_items[l], _items[largest]) > 0)
					largest = l;
				if (r < _heapSize && Compare(_items[r], _items[largest]) > 0)
					largest = r;

				if (largest == i)
					break;

				Exchange(i, largest);

				i = largest;
			}
		}

		/// <summary>
		/// Build a complete heap from the given items using Floyd's method.  Runs in
		/// O(n) time.
		/// </summary>
		private void BuildHeap()
		{
			_heapSize = _items.Length;
			for (int i = _items.Length / 2 - 1; i >= 0; i--)
			{
				Heapify(i);
			}
		}

		#endregion

		#region Heap mutation (Add, ExtractMax, Clear)

		/// <summary>
		/// Extract the largest element of the given heap.  Runs in O(lg n) time.
		/// If the heap is empty, this will throw an InvalidOperationException.
		/// </summary>
		public T ExtractMax()
		{
			if (_heapSize <= 0)
				throw new InvalidOperationException("Cannot perform ExtractMax() on an empty Heap.");

			T max = _items[0];
			_items[0] = _items[--_heapSize];
			Heapify(0);

			if (_heapSize <= _items.Length / 4 && _items.Length >= 64)
			{
				// Way much empty space, so shrink the array.
				T[] newItems = new T[_items.Length / 2];
				Array.Copy(_items, 0, newItems, 0, _heapSize);
				_items = newItems;
			}

			return max;
		}

		/// <summary>
		/// Add a single item to the heap.  Runs in O(lg n) time.
		/// </summary>
		public void Add(T item)
		{
			if (_heapSize >= _items.Length)
			{
				// No room, so grow the array.
				T[] newItems = _items.Length > 0 ? new T[_items.Length * 2] : new T[16];
				Array.Copy(_items, 0, newItems, 0, _heapSize);
				_items = newItems;
			}

			int index = _heapSize++;
			_items[index] = item;
			Heapify(index);
		}

		/// <summary>
		/// Add many items to the heap.  Runs in O(n lg n) time, where 'n' is the
		/// final size of the heap.  This is a trivial implementation that just
		/// repeatedly invokes Add().
		/// </summary>
		public void AddRange(IEnumerable<T> items)
		{
			foreach (T item in items)
				Add(item);
		}

		/// <summary>
		/// Delete all items from the heap.  Runs in O(1) time.
		/// </summary>
		public void Clear()
		{
			_heapSize = 0;
			_items = new T[0];
		}

		#endregion

		#region ICollection<T> methods

		/// <summary>
		/// Determine whether the heap contains the given item.
		/// </summary>
		public bool Contains(T item)
		{
			for (int i = 0; i < _heapSize; i++)
			{
				T current = _items[i];
				if (ReferenceEquals(current, null))
				{
					if (ReferenceEquals(item, null))
						return true;
				}
				else if (current.Equals(item))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Copy the entire heap to the given array.
		/// </summary>
		public void CopyTo(T[] array, int arrayIndex)
		{
			for (int i = 0; i < _heapSize; i++)
			{
				array[arrayIndex++] = _items[i];
			}
		}

		/// <summary>
		/// Direct removal of a specific item from the heap is not currently supported.
		/// </summary>
		bool ICollection<T>.Remove(T item)
			=> throw new NotSupportedException();

		/// <summary>
		/// Enumerate the heap, in root-to-descendant order.
		/// </summary>
		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < _heapSize; i++)
			{
				yield return _items[i];
			}
		}

		/// <summary>
		/// Enumerate the heap, in root-to-descendant order.
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion
	}
}
