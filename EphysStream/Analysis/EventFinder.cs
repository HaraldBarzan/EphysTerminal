using TINS.Data;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// A class that finds events within a sequence of words.
	/// </summary>
	public class EventFinder
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public EventFinder()
		{
		}

		/// <summary>
		/// Create a new event finder.
		/// </summary>
		/// <param name="startWord"></param>
		public EventFinder(int startWord)
		{
			_prevWord = startWord;
		}

		/// <summary>
		/// Find events within the provided sequence of words.
		/// </summary>
		/// <param name="words">The list of words to search.</param>
		public void FindEvents(Vector<int> words)
		{
			FoundEvents.Clear();

			if (words is null || words.IsEmpty)
				return;

			// look for changes
			for (int i = 0; i < words.Size; ++i)
			{
				if (_prevWord != words[i])
				{
					_prevWord = words[i];
					FoundEvents.PushBack(new EventMarker(_prevWord, i));
				}
			}
		}

		/// <summary>
		/// The events found in the previous call to <c>FindEvents</c>.
		/// </summary>
		public Vector<EventMarker> FoundEvents { get; } = new();

		/// <summary>
		/// The number of identified events.
		/// </summary>
		public int FoundEventCount => FoundEvents.Size;

		/// <summary>
		/// The previously memorized word.
		/// </summary>
		protected int _prevWord;
	}
}
