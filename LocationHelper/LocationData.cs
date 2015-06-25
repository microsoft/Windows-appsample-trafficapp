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
using System.Runtime.Serialization;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Services.Maps;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Location
{
    /// <summary>
    /// Represents a saved location for use in tracking travel time, distance, and routes. 
    /// </summary>
    public class LocationData : BindableBase
    {
        private string name;
        /// <summary>
        /// Gets or sets the name of the location.
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set { this.SetProperty(ref this.name, value); }
        }

        private string address;
        /// <summary>
        /// Gets or sets the address of the location.
        /// </summary>
        public string Address
        {
            get { return this.address; }
            set { this.SetProperty(ref this.address, value); }
        }

        private BasicGeoposition position;
        /// <summary>
        /// Gets the geographic position of the location.
        /// </summary>
        public BasicGeoposition Position
        {
            get { return this.position; }
            set
            {
                this.SetProperty(ref this.position, value);
                this.OnPropertyChanged(nameof(Geopoint));
            }
        }

        /// <summary>
        /// Gets a Geopoint representation of the current location for use with the map service APIs.
        /// </summary>
        public Geopoint Geopoint => new Geopoint(this.Position);

        private bool isCurrentLocation;
        /// <summary>
        /// Gets or sets a value that indicates whether the location represents the user's current location.
        /// </summary>
        public bool IsCurrentLocation
        {
            get { return this.isCurrentLocation; }
            set
            {
                this.SetProperty(ref this.isCurrentLocation, value);
                this.OnPropertyChanged(nameof(NormalizedAnchorPoint));
            }
        }

        private bool isSelected;
        /// <summary>
        /// Gets or sets a value that indicates whether the location is 
        /// the currently selected one in the list of saved locations.
        /// </summary>
        [IgnoreDataMember]
        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                this.SetProperty(ref this.isSelected, value);
                this.OnPropertyChanged(nameof(ImageSource));
            }
        }

        /// <summary>
        /// Gets a path to an image to use as a map pin, reflecting the IsSelected property value. 
        /// </summary>
        public string ImageSource => IsSelected ? "Assets/mappin-yellow.png" : "Assets/mappin.png"; 

        private Point centerpoint = new Point(0.5, 0.5);
        private Point pinpoint = new Point(0.5, 0.9778);
        /// <summary>
        /// Gets a value for the MapControl.NormalizedAnchorPoint attached property
        /// to reflect the different map icon used for the user's current location. 
        /// </summary>
        public Point NormalizedAnchorPoint => IsCurrentLocation ? centerpoint : pinpoint;

        private MapRoute fastestRoute;
        /// <summary>
        /// Gets or sets the route with the shortest travel time to the 
        /// location from the user's current position.
        /// </summary>
        [IgnoreDataMember]
        public MapRoute FastestRoute
        {
            get { return this.fastestRoute; }
            set { this.SetProperty(ref this.fastestRoute, value); }
        }

        private int currentTravelTimeWithoutTraffic;
        /// <summary>
        /// Gets or sets the number of minutes it takes to drive to the location,
        /// without taking traffic into consideration.
        /// </summary>
        public int CurrentTravelTimeWithoutTraffic
        {
            get { return this.currentTravelTimeWithoutTraffic; }
            set { this.SetProperty(ref this.currentTravelTimeWithoutTraffic, value); }
        }

        private int currentTravelTime;
        /// <summary>
        /// Gets or sets the number of minutes it takes to drive to the location,
        /// taking traffic into consideration.
        /// </summary>
        public int CurrentTravelTime
        {
            get { return this.currentTravelTime; }
            set
            {
                this.SetProperty(ref this.currentTravelTime, value);
                this.OnPropertyChanged(nameof(FormattedCurrentTravelTime));
            }
        }

        /// <summary>
        /// Gets a display-string representation of the current travel time. 
        /// </summary>
        public string FormattedCurrentTravelTime =>
            this.CurrentTravelTime == 0 ? "??:??" :
            new TimeSpan(0, this.CurrentTravelTime, 0).ToString("hh\\:mm");

        private double currentTravelDistance;
        /// <summary>
        /// Gets or sets the current driving distance to the location, in miles.
        /// </summary>
        public double CurrentTravelDistance
        {
            get { return this.currentTravelDistance; }
            set
            {
                this.SetProperty(ref this.currentTravelDistance, value);
                this.OnPropertyChanged(nameof(FormattedCurrentTravelDistance));
            }
        }

        /// <summary>
        /// Gets a display-string representation of the current travel distance.
        /// </summary>
        public string FormattedCurrentTravelDistance =>
            this.CurrentTravelDistance == 0 ? "?? miles" :
            this.CurrentTravelDistance + " miles";

        private DateTimeOffset timestamp;
        /// <summary>
        /// Gets or sets a value that indicates when the travel info was last updated. 
        /// </summary>
        public DateTimeOffset Timestamp
        {
            get { return this.timestamp; }
            set
            {
                this.SetProperty(ref this.timestamp, value);
                this.OnPropertyChanged(nameof(FormattedTimeStamp));
            }
        }

        /// <summary>
        /// Raises a change notification for the timestamp in order to update databound UI.   
        /// </summary>
        public void RefreshFormattedTimestamp() => this.OnPropertyChanged(nameof(FormattedTimeStamp));

        /// <summary>
        /// Gets a display-string representation of the freshness timestamp. 
        /// </summary>
        public string FormattedTimeStamp
        {
            get
            {
                double minutesAgo = this.Timestamp == DateTimeOffset.MinValue ? 0 :
                    Math.Floor((DateTimeOffset.Now - this.Timestamp).TotalMinutes);
                return $"{minutesAgo} minute{(minutesAgo == 1 ? "" : "s")} ago";
            }
        }

        private bool isMonitored;
        /// <summary>
        /// Gets or sets a value that indicates whether this location is 
        /// being monitored for an increase in travel time due to traffic. 
        /// </summary>
        public bool IsMonitored
        {
            get { return this.isMonitored; }
            set { this.SetProperty(ref this.isMonitored, value); }
        }

        /// <summary>
        /// Resets the travel time and distance values to 0, which indicates an unknown value.
        /// </summary>
        public void ClearTravelInfo()
        {
            this.CurrentTravelDistance = 0;
            this.currentTravelTime = 0;
            this.Timestamp = DateTimeOffset.Now;
        }

        /// <summary>
        /// Returns the name of the location, or the geocoordinates if there is no name. 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => String.IsNullOrEmpty(this.Name) ? 
            $"{this.Position.Latitude}, {this.Position.Longitude}" : this.Name;

        /// <summary>
        /// Return a new LocationData with the same property values as the current one.
        /// </summary>
        /// <returns>The new LocationData instance.</returns>
        public LocationData Clone()
        {
            var location = new LocationData();
            location.Copy(this);
            return location;
        }

        /// <summary>
        /// Copies the property values of the current location into the specified location.
        /// </summary>
        /// <param name="location">The location to receive the copied values.</param>
        public void Copy(LocationData location)
        {
            this.Name = location.Name;  
            this.Address = location.Address;
            this.Position = location.Position;
            this.CurrentTravelDistance = location.CurrentTravelDistance;
            this.CurrentTravelTime = location.CurrentTravelTime;
            this.Timestamp = location.Timestamp;
            this.IsMonitored = location.IsMonitored;
        }

    }
}
