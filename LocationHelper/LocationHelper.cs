//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.UI.Notifications;

namespace Location
{
    public static class LocationHelper
    {
        private static string routeFinderUnavailableMessage = "Unable to access map route finder service.";

        /// <summary>
        /// Gets the Geolocator singleton used by the LocationHelper.
        /// </summary>
        public static Geolocator Geolocator { get; } = new Geolocator();

        /// <summary>
        /// Gets or sets the CancellationTokenSource used to enable Geolocator.GetGeopositionAsync cancellation.
        /// </summary>
        private static CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Initializes the LocationHelper. 
        /// </summary>
        static LocationHelper()
        {
            // TODO Replace the placeholder string below with your own Bing Maps key from https://www.bingmapsportal.com
            MapService.ServiceToken = "<insert your Bing Maps key here>";
        }

        /// <summary>
        /// Gets the current location if the geolocator is available.
        /// </summary>
        /// <returns>The current location.</returns>
        public static async Task<LocationData> GetCurrentLocationAsync()
        {
            try
            {
                // Request permission to access the user's location.
                var accessStatus = await Geolocator.RequestAccessAsync();

                switch (accessStatus)
                {
                    case GeolocationAccessStatus.Allowed:

                        LocationHelper.CancellationTokenSource = new CancellationTokenSource();
                        var token = LocationHelper.CancellationTokenSource.Token;

                        Geoposition position = await Geolocator.GetGeopositionAsync().AsTask(token);
                        return new LocationData { Position = position.Coordinate.Point.Position };

                    case GeolocationAccessStatus.Denied: 
                    case GeolocationAccessStatus.Unspecified:
                    default:
                        return null;
                }
            }
            catch (TaskCanceledException)
            {
                // Do nothing.
            }
            finally
            {
                LocationHelper.CancellationTokenSource = null;
            }
            return null;
        }

        /// <summary>
        /// Cancels any waiting GetGeopositionAsync call.
        /// </summary>
        public static void CancelGetCurrentLocation()
        {
            if (LocationHelper.CancellationTokenSource != null)
            {
                LocationHelper.CancellationTokenSource.Cancel();
                LocationHelper.CancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Launches the Maps app and displays the route from the current location
        /// to the specified location.
        /// </summary>
        /// <param name="location">The location to display the route to.</param>
        public static async Task ShowRouteToLocationInMapsAppAsync(LocationData location, LocationData currentLocation)
        {
            var mapUri = new Uri("bingmaps:?trfc=1&rtp=" + 
                $"pos.{Math.Round(currentLocation.Position.Latitude, 6)}_{Math.Round(currentLocation.Position.Longitude, 6)}~" + 
                $"pos.{location.Position.Latitude}_{location.Position.Longitude}");
            await Windows.System.Launcher.LaunchUriAsync(mapUri);
        }

        /// <summary>
        /// Shows the specified text in a toast notification if notifications are enabled.
        /// </summary>
        /// <param name="text">The text to show.</param>
        private static void ShowToast(string text)
        {
            var toastXml = new XmlDocument();
            toastXml.LoadXml("<toast duration='short'><visual><binding template='ToastText01'>" +
                $"<text id='1'>{text}</text></binding></visual></toast>");
            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        /// <summary>
        /// Registers the TrafficMonitor background task.
        /// </summary>
        public static void RegisterTrafficMonitor()
        {
            BackgroundTaskHelper.RegisterBackgroundTask(
                taskEntryPoint: "TrafficMonitor.TrafficMonitor",
                taskName: "TrafficMonitor",
                trigger: new MaintenanceTrigger(freshnessTime: 15, oneShot: false),
                condition: new SystemCondition(SystemConditionType.InternetAvailable));
        }

        /// <summary>
        /// Unregisters the TrafficMonitor background task.
        /// </summary>
        public static void UnregisterTrafficMonitor()
        {
            BackgroundTaskHelper.UnregisterBackgroundTask("TrafficMonitor");
        }

        /// <summary>
        /// Loads the location data from storage then raises an alert for each flagged location 
        /// if traffic is currently increasing the travel time by 10 minutes or more.
        /// </summary>
        public static async Task CheckTravelInfoForMonitoredLocationsAsync()
        {
            var locations = await LocationDataStore.GetLocationDataAsync();
            var flaggedLocations = locations.Where(location => location.IsMonitored).ToList();
            if (flaggedLocations.Count > 0)
            {
                var currentLocation = await LocationHelper.GetCurrentLocationAsync();
                if (!await LocationHelper.TryUpdateLocationsTravelInfoAsync(flaggedLocations, currentLocation))
                {
                    LocationHelper.ShowToast("Can't get location/traffic info.");
                }
            }
        }

        /// <summary>
        /// Attempts to update the travel distance and time info for the specified locations, 
        /// relative to the current location, and raises an alert for each flagged location 
        /// if traffic is currently increasing the travel time by 10 minutes or more.
        /// </summary>
        /// <param name="locations">The locations to update.</param>
        /// <param name="currentLocation">The current location, providing context to disambiguate locations, if needed. </param>
        /// <returns>true if all the locations were successfully updated; false if a service failure occurred.</returns>
        public static async Task<bool> TryUpdateLocationsTravelInfoAsync(IEnumerable<LocationData> locations, LocationData currentLocation)
        {
            try
            {
                await Task.WhenAll(locations.Select(async location =>
                {
                    await LocationHelper.UpdateTravelInfoAsync(location, currentLocation);

                    int travelTimeDifference = location.CurrentTravelTime - location.CurrentTravelTimeWithoutTraffic;

                    if (location.IsMonitored && travelTimeDifference >= 10)
                    {
                        LocationHelper.ShowToast(
                            $"+{travelTimeDifference} min. to {location.Name}, total {location.CurrentTravelTime} min.");
                    }
                }));
                return true;
            }
            catch (Exception ex) when (ex.Message.Equals(routeFinderUnavailableMessage))
            {
                return false;
            }

        }

        /// <summary>
        /// Updates the travel distance and time info for the specified location, relative to the specified current location.
        /// </summary>
        /// <param name="location">The location to update.</param>  
        /// <param name="currentLocation">The current location.</param>
        public static async Task UpdateTravelInfoAsync(LocationData location, LocationData currentLocation)
        {
            var routeResultTask = MapRouteFinder.GetDrivingRouteAsync(
                currentLocation.Geopoint, location.Geopoint,
                MapRouteOptimization.TimeWithTraffic, MapRouteRestrictions.None);
            var routeResultWithoutTrafficTask = MapRouteFinder.GetDrivingRouteAsync(
                currentLocation.Geopoint, location.Geopoint,
                MapRouteOptimization.Time, MapRouteRestrictions.None);

            MapRouteFinderResult routeResult = await routeResultTask;
            MapRouteFinderResult routeResultWithoutTraffic = await routeResultWithoutTrafficTask;
            if (routeResult.Status == MapRouteFinderStatus.Success)
            {
                location.FastestRoute = routeResult.Route;
                location.CurrentTravelDistance = Math.Round(routeResult.Route.LengthInMeters * 0.00062137, 1); // convert to miles
                location.CurrentTravelTime = (int)routeResult.Route.EstimatedDuration.TotalMinutes;
                location.Timestamp = DateTimeOffset.Now;
                if (routeResultWithoutTraffic.Status == MapRouteFinderStatus.Success)
                {
                    location.CurrentTravelTimeWithoutTraffic = routeResultWithoutTraffic.Route.EstimatedDuration.Minutes;
                }
                else
                {
                    // Fall back to the with-traffic value if the request fails.
                    location.CurrentTravelTimeWithoutTraffic = routeResult.Route.EstimatedDuration.Minutes;
                }
            }
            else throw new Exception(routeFinderUnavailableMessage);
        }

        /// <summary>
        /// Attempts to update either the address or the coordinates of the specified location 
        /// if the other value is missing, using the specified current location to provide 
        /// context for prioritizing multiple locations returned for an address.  
        /// </summary>
        /// <param name="location">The location to update.</param>
        /// <param name="currentLocation">The current location.</param>
        public static async Task<bool> TryUpdateMissingLocationInfoAsync(LocationData location, LocationData currentLocation)
        {
            bool hasNoAddress = String.IsNullOrEmpty(location.Address);
            if (hasNoAddress && location.Position.Latitude == 0 && location.Position.Longitude == 0) return true;

            var results = hasNoAddress ?
                await MapLocationFinder.FindLocationsAtAsync(location.Geopoint) :
                await MapLocationFinder.FindLocationsAsync(location.Address, currentLocation.Geopoint);

            if (results.Status == MapLocationFinderStatus.Success && results.Locations.Count > 0)
            {
                var result = results.Locations.First();
                location.Position = result.Point.Position;
                location.Address = result.Address.FormattedAddress;
                if (String.IsNullOrEmpty(location.Name)) location.Name = result.Address.Town;

                // Sometimes the returned address is poorly formatted. This fixes one of the issues.
                if (location.Address.Trim().StartsWith(",")) location.Address = location.Address.Trim().Substring(1).Trim();
                return true;
            }
            else
            {
                return false;
            }
        }

    }

}
    
