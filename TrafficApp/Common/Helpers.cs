using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace TrafficApp.Common
{
    public static class Helpers
    {
        /// <summary>
        /// Runs the specified handler on the UI thread at Normal priority. 
        /// </summary>
        public static async Task CallOnUiThreadAsync(DispatchedHandler handler) => await
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, handler);

        /// <summary>
        /// Starts a timer to perform the specified action at the specified interval.
        /// </summary>
        /// <param name="intervalInMinutes">The interval.</param>
        /// <param name="action">The action.</param>
        public static void StartTimer(int intervalInMinutes, Action action)
        {
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, intervalInMinutes, 0);
            timer.Tick += (s, e) => action();
            timer.Start();
        }

    }
}
