using System;
using TINS.Data;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// A class that finds events within a sequence of words.
	/// </summary>
	public class LineEventFinder
	{
		/// <summary>
		/// Create a new event finder.
		/// </summary>
		/// <param name="startEvent">The starting state of the line event finder.</param>
		/// <param name="correctArtefactEvents">Correct short-lived (one sampling cycle) events that are generally the result of bad sampling.</param>
		/// <param name="eventFilter">Filter event codes that fail this check. If null, all events are passed.</param>
		public LineEventFinder(int startEvent = 0, bool correctArtefactEvents = true, Predicate<int> eventFilter = null)
		{
			_prevEvent				= startEvent;
			_correctArtefactEvents	= correctArtefactEvents;
			_eventFilter			= eventFilter;
		}

		/// <summary>
		/// Find events within the provided line.
		/// </summary>
		/// <param name="line">The line to parse.</param>
		public void FindEvents(Vector<int> line)
		{
			FoundEvents.Clear();

			if (line is null || line.IsEmpty)
				return;

			// look for changes
			for (int i = 0; i < line.Size; ++i)
			{
				int currentEvent = line[i];
				if (_prevEvent != currentEvent)
				{
					// do the artefact trigger replacement
					if (_correctArtefactEvents	&& 
						FoundEvents.Size > 0	&& FoundEvents.Back.Timestamp == i - 1)
					{
						// get the event
						var artefactEvent		= FoundEvents.PopBack();
						artefactEvent.EventCode = currentEvent;

						// check the new event against the event filter
						if (_eventFilter is not null && !_eventFilter(line[i]))
							continue;

						// put the event back in the series with the new code
						FoundEvents.PushBack(artefactEvent);
					}
					else
					{
						// check against the filter
						if (_eventFilter is not null && !_eventFilter(line[i]))
							continue;

						// add the event to the found list
						FoundEvents.PushBack(new EventMarker(currentEvent, i));
					}
				}

				_prevEvent = currentEvent;
			}
		}

		/// <summary>
		/// The events found in the previous call to <c>FindEvents</c>.
		/// </summary>
		public Vector<EventMarker> FoundEvents { get; } = new();

		/// <summary>
		/// Set the event filter. The filter will only pass matching events through.
		/// </summary>
		/// <param name="eventFilter">The event filter predicate.</param>
		public void SetFilter(Predicate<int> eventFilter)
			=> _eventFilter = eventFilter;

		/// <summary>
		/// The number of identified events.
		/// </summary>
		public int FoundEventCount => FoundEvents.Size;

		/// <summary>
		/// The previously memorized word.
		/// </summary>
		protected int				_prevEvent;
		protected Predicate<int>	_eventFilter;
		protected bool				_correctArtefactEvents;
	}
}
