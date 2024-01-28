using System;
using System.Collections.Generic;
using TINS.Containers;
using TINS.Ephys.Analysis;


namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;

	/// <summary>
	/// Closed loop algorithm variant III:
	/// add two fixed exploration blocks at +- delta around current stimulus frequency, continue with best one
	/// </summary>
	public class CL2DichotomicExplorator : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		public class ExplorationBlock
		{
			public float StimFrequency { get; init; }
			public int PeakIdx { get; set; }
			public float PeakPower { get; set; }
			public float PeakFrequency { get; set; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2DichotomicExplorator(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// Get the name of the current block type.
		/// </summary>
		public override string CurrentBlockType
		{
			get => _currentBlock != null ? "cl3-exp" : "cl3";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, int periods, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, periods, out blockResult);
			
			// get frequency and power at peak
			using var spec	= Get1DPowerSpectrum(periods);
			var iPeak		= FindPeak(spec);
			float freq		= iPeak < 0 ? float.NaN : Numerics.Clamp(MathF.Round(spec.BinToFrequency(iPeak)), Protocol.Config.StimulationFrequencyRange);
			float pow		= iPeak < 0 ? float.NaN : spec[iPeak];

			// check exploration mode
			if (_currentBlock is not null)
			{
				// set the current block's results
				_currentBlock.PeakIdx		= iPeak;
				_currentBlock.PeakPower		= pow;
				_currentBlock.PeakFrequency = freq;
				if (_currentBlock.PeakIdx >= 0)
					_validBlocks.PushBack(_currentBlock);

				// check if there is a next block
				if (_queuedBlocks.Count == 0)
				{
					float result	= currentFrequency;
					blockResult		= "cl3-nopeak";

					// find winner within valid blocks
					if (_validBlocks.Size > 0)
					{
						int iMax = 0;
						for (int i = 1; i < _validBlocks.Size; ++i)
							if (_validBlocks[i].PeakPower > _validBlocks[iMax].PeakPower)
								iMax = i;

						// set results
						result		= _validBlocks[iMax].PeakFrequency;
						blockResult = "cl3-update";
					}

					// end exploration mode
					_currentBlock = null;
					_validBlocks.Clear();
					return result;
				}
				else
				{
					// continue with the next block
					_currentBlock	= _queuedBlocks.Dequeue();
					blockResult		= "cl3-exp";
					return _currentBlock.StimFrequency;
				}
			}
			else
			{
				// check if we have peak. we start exploration only on valid peaks
				if (iPeak >= 0)
				{
					// save current block as exploration block
					_validBlocks.PushBack(new ExplorationBlock()
					{
						StimFrequency	= currentFrequency,
						PeakIdx			= iPeak,
						PeakPower		= pow,
						PeakFrequency	= freq,
					});

					// add two blocks (upper delta and lower delta)
					_currentBlock =			new ExplorationBlock { StimFrequency = currentFrequency - Protocol.Config.ExplorationDelta };
					_queuedBlocks.Enqueue(	new ExplorationBlock { StimFrequency = currentFrequency + Protocol.Config.ExplorationDelta });

					return _currentBlock.StimFrequency;
				}
				else
				{
					// keep going at current frequency
					return currentFrequency;
				}
			}
		}


		protected Queue<ExplorationBlock>	_queuedBlocks	= new();
		protected Vector<ExplorationBlock>	_validBlocks	= new();
		protected ExplorationBlock			_currentBlock	= null;
	}
}
