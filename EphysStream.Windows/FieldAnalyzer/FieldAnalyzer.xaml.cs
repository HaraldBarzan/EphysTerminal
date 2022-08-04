using Microsoft.Win32;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using TINS.Containers;
using TINS.Data;
using TINS.Data.EPD;
using TINS.Ephys.Processing;

namespace TINS.Ephys
{
	/// <summary>
	/// Interaction logic for FieldAnalyzer.xaml
	/// </summary>
	public partial class FieldAnalyzer : Window
	{
		/// <summary>
		/// 
		/// </summary>
		public FieldAnalyzer()
		{
			InitializeComponent();
			txbvTrialString.ValidationFunction		= ValidateTrialString;
			txbvRefEventString.ValidationFunction	= ValidateRefEventString;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRun_Click(object sender, RoutedEventArgs e)
		{

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="tabItem"></param>
		protected void TabSwitchToSpecial(TabItem tabItem)
		{
			tabControl	.SelectedItem	= tabItem;
			tabItem		.Visibility		= Visibility.Visible;
			tabTrials	.Visibility		= Visibility.Hidden;
			tabProcess	.Visibility		= Visibility.Hidden;
			tabAnalysis	.Visibility		= Visibility.Hidden;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="switchTo"></param>
		protected void TabReturnToGeneral(TabItem switchTo = null)
		{
			if (tabControl.SelectedItem is TabItem ti)
				ti.Visibility = Visibility.Hidden;
			tabControl.SelectedItem		= switchTo ?? tabTrials;
			tabTrials	.Visibility		= Visibility.Visible;
			tabProcess	.Visibility		= Visibility.Visible;
			tabAnalysis	.Visibility		= Visibility.Visible;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected Display.ValidationResult ValidateTrialString()
		{
			if (_dataset is null)
				return Display.ValidationResult.Unvalidated;

			try
			{
				var pattern = new EventPattern(txbvTrialString.Text);
				var session = new AnalysisSession(_dataset, pattern);

				if (session.Trials.IsEmpty)
					return Display.ValidationResult.False;
				else
				{
					// replace session if trial number is different
					if (_session is null || _session.Trials.Size != session.Trials.Size)
						_session = session;
					return Display.ValidationResult.True;
				}
			}
			catch (Exception)
			{
				_session = null;
				return Display.ValidationResult.False;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected Display.ValidationResult ValidateRefEventString()
		{
			if (_dataset is null)
				return Display.ValidationResult.Unvalidated;

			if (_session is null && ValidateTrialString() != Display.ValidationResult.True)
				return Display.ValidationResult.Unvalidated;

			try
			{
				var eventGroup	= new EventGroup(txbvRefEventString.Text);
				foreach (var grp in _session.EventPattern)
					if (grp == eventGroup)
						return Display.ValidationResult.True;
				return Display.ValidationResult.False; 
			}
			catch (Exception)
			{
				_markers = null;
				return Display.ValidationResult.False;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		class Condition : Vector<(string Key, string Value)>
		{
			public override string ToString()
			{
				if (IsEmpty)
					return "Empty condition.";

				using var sl		= new StringList();
				using var trials	= Session.FilterKeep(TrialMatch.All, ToArray());
				foreach (var kvp in this)
					sl.PushBack(kvp.ToString());
				return $"{sl.Join(',')} - {trials.Trials.Size} trials";
			}

			public AnalysisSession Session { get; init; }
		}

		/// <summary>
		/// 
		/// </summary>
		class PreprocessingStep
		{
			
			public override string ToString()
			{
				var name = Pipe.GetType().Name;
				switch (name)
				{
					case "FilterBank":
						return $"{name}: ...";
					case "Decimator":
						return $"{name}: ...";
					default:
						return "Invalid processing object.";
				}
			}

			public ProcessingComponent Pipe { get; init; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnConfirmCondition_Click(object sender, RoutedEventArgs e)
		{
			var condition	= new Condition() { Session = _session };
			var fields		= _conditionData.Rows[0].ItemArray;

			for (int i = 0; i < fields.Length; ++i)
			{
				if (fields[i] is string value && !string.IsNullOrEmpty(value))
					condition.PushBack((_conditionData.Columns[i].ColumnName, value));
			}

			if (condition.IsEmpty)
			{
				MessageBox.Show("Condition is empty. Please create a valid condition.");
				return;
			}

			cmbConditions.Items.Add(condition);
			cmbConditions.SelectedItem = condition;

			ResetAddConditionTab();
			TabReturnToGeneral(tabTrials);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnCancelCondition_Click(object sender, RoutedEventArgs e)
		{
			ResetAddConditionTab();
			TabReturnToGeneral(tabTrials);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnConfirmProcess_Click(object sender, RoutedEventArgs e)
		{
			if (tabControlProc.SelectedItem == tabAddProcessing)



			ResetAddProcessTab();
			TabReturnToGeneral(tabProcess);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnCancelProcess_Click(object sender, RoutedEventArgs e)
		{
			ResetAddProcessTab();
			TabReturnToGeneral(tabProcess);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnGotoConditionCreator_Click(object sender, RoutedEventArgs e)
		{
			if (_trialInfo is null || (_session is null && ValidateTrialString() != Display.ValidationResult.True))
			{
				MessageBox.Show("Please specify a valid trial info (.eti) file.");
				return;
			}

			for (int i = 0; i < _conditionData.Rows[0].ItemArray.Length; ++i)
				_conditionData.Rows[0].ItemArray[i] = string.Empty;

			TabSwitchToSpecial(tabAddCondition);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRemoveConditions_Click(object sender, RoutedEventArgs e)
		{
			if (cmbConditions.SelectedItem is not null)
			{
				cmbConditions.Items.Remove(cmbConditions.SelectedItem);
				if (cmbConditions.Items.Count > 0)
					cmbConditions.SelectedIndex = 0;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnAddProc_Click(object sender, RoutedEventArgs e)
			=> TabSwitchToSpecial(tabAddProcessing);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRemoveProc_Click(object sender, RoutedEventArgs e)
		{
			if (lsbPreproc.SelectedItem is not null)
			{
				lsbPreproc.Items.Remove(lsbPreproc.SelectedItem);
				if (lsbPreproc.Items.Count > 0)
					lsbPreproc.SelectedIndex = 0;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnEtiBrowse_Click(object sender, RoutedEventArgs e)
		{
			if (_session is null && ValidateTrialString() != Display.ValidationResult.True)
			{
				MessageBox.Show("Please specify a valid trial string.");
				return;
			}

			var ofd = new OpenFileDialog()
			{
				Filter		= "Trial info files (*.eti)|*.eti",
				Multiselect = false
			};
			
			if (ofd.ShowDialog() == true)
			{
				try
				{
					var eti = new TrialInfoFile(ofd.FileName);
					if (eti.TrialCount != _session.Trials.Size)
					{
						MessageBox.Show("Trial number mismatch between the ETI and the parsed trials.");
						return;
					}

					// assign eti
					_trialInfo		= eti;
					_session		= new AnalysisSession(_session.Dataset, _session.EventPattern, eti);
					txbEtiPath.Text = ofd.FileName;
					cmbConditions.Items.Clear(); // clear the trial filters when eti is replaced
					PopulateAddTrialFilterTab();
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Selected file is not a valid ETI file. Exception message: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnLoad_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new OpenFileDialog()
			{
				Filter		= "EEG Processor Dataset (*.epd)|*.epd",
				Multiselect = false
			};

			if (ofd.ShowDialog() == true)
			{
				try
				{
					var ds = new Dataset(ofd.FileName);
					ClearAll();

					// assign
					_dataset				= ds;
					lblDatasetPath.Content	= ofd.FileName;
					lblDsFSample.Content	= _dataset.SamplingRate;
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Selected file is not a valid EPD file. Exception message: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private void PopulateAddTrialFilterTab()
		{
			// populate table header
			_trialData		??= new DataTable();
			_conditionData	??= new DataTable();
			_trialData		.Clear();
			_conditionData	.Clear();

			// headers
			foreach (var field in _trialInfo.Fields)
			{
				_trialData		.Columns.Add(field);
				_conditionData	.Columns.Add(field);
			}

			// items
			var tr = new object[_trialInfo.FieldCount];
			for (int i = 0; i < _trialInfo.TrialCount; ++i)
			{
				for (int j = 0; j < _trialInfo.FieldCount; ++j)
					tr[j] = _trialInfo[i, j];
				_trialData.Rows.Add(tr);
			}
			_conditionData.Rows.Add(new object[_trialInfo.FieldCount]);

			tableAllTrials.ItemsSource = _trialData.DefaultView;
			tableCondition.ItemsSource = _conditionData.DefaultView;
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ResetAddConditionTab()
		{
			foreach (DataColumn col in _conditionData.Columns)
				_conditionData.Rows[0].SetField(col, string.Empty);
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ResetAddProcessTab()
		{
			// reset the tab
			cmbFilterPass.SelectedIndex = 0;
			txbCutoffLow.Text			= "";
			txbCutoffHigh.Text			= "";
			txbFilterOrder.Text			= "3";
			txbDecimation.Text			= "1";
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ClearAll()
		{
			// dispose and nullate
			_trialInfo		?.Dispose();
			_session		?.Dispose();
			_markers		?.Dispose();
			_trialData		?.Dispose();
			_conditionData	?.Dispose();
			_dataset		= null;
			_markers		= null;
			_trialInfo		= null;
			_session		= null;
			_trialData		= null;
			_conditionData	= null;

			// reset fields
			lblDatasetPath.Content = "<no-dataset-loaded>";
			cmbConditions.Items.Clear();
			tableAllTrials.Items.Clear();
			txbEtiPath.Text = "";
		}


		protected Dataset				_dataset		= null;
		protected TrialInfoFile			_trialInfo		= null;
		protected AnalysisSession		_session		= null;
		protected Vector<EventMarker>	_markers		= null;
		protected DataTable				_trialData		= null;
		protected DataTable				_conditionData	= null;


	}
}

