using System;
using System.Windows.Threading;
using Dependinator.Utils.UI;


namespace Dependinator.Common.ProgressHandling
{
	internal class ProgressDialogViewModel : ViewModel
	{
		private static readonly string Indicators = "o o o o o o o o o o o o o o o o o o o o o o ";
		private static readonly TimeSpan InitialIndicatorTime = TimeSpan.FromMilliseconds(10);
		private static readonly TimeSpan IndicatorInterval = TimeSpan.FromMilliseconds(500);

		private readonly DispatcherTimer timer = new DispatcherTimer();
		private int progressCount;
		private int indicatorIndex;


		public ProgressDialogViewModel()
		{
			timer.Tick += UpdateIndicator;
		}


		public string Text { get => Get(); set => Set(value); }

		public string IndicatorText { get => Get(); private set => Set(value); }


		public void Start()
		{
			progressCount++;

			if (progressCount == 1)
			{
				indicatorIndex = 0;
				timer.Interval = InitialIndicatorTime;
				timer.Start();
			}
		}


		public void Stop()
		{
			progressCount--;

			if (progressCount == 0)
			{
				timer.Stop();
				indicatorIndex = 0;
				IndicatorText = "";
			}
		}


		private void UpdateIndicator(object sender, EventArgs e)
		{
			if (progressCount > 0)
			{
				string indicatorText = Indicators;

				indicatorIndex = (indicatorIndex + 1) % (Indicators.Length / 2);
				IndicatorText = indicatorText.Substring(0, indicatorIndex * 2);
				timer.Interval = IndicatorInterval;
			}
			else
			{
				timer.Stop();
				indicatorIndex = 0;
				IndicatorText = "";
			}
		}
	}
}