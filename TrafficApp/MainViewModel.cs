using Location;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TrafficApp.Common;
using Windows.Devices.Geolocation;
using Windows.Networking.Connectivity;
using Windows.Services.Maps;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TrafficApp
{
    /// <summary>
    /// Represents the behavior and state of the app, independent of the view that renders the app.
    /// The view (MainPage.xaml and MainPage.xaml.cs) handles events that originate in the view model, 
    /// and routes all of its events to the view model. Some of this event routing is handled automatically
    /// through data bindings, but some of it is implemented explicitly in MainPage.xaml.cs, primarily 
    /// for the sake of convenience in cases where there isn't a good property to bind to. 
    /// </summary>
    public class MainViewModel : BindableBase
    {
        public MainViewModel()
        {
            LocationHelper.RegisterTrafficMonitor();
            InitializeCollections();
            InitializeTimers();
            InitializeData();
        }

        private void InitializeCollections()
        {
            Locations = new ObservableCollection<LocationData>();
            MappedLocations = new ObservableCollection<LocationData>(Locations);

            // MappedLocations is a superset of Locations, so any changes in Locations
            // need to be reflected in MappedLocations. 
            Locations.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null) foreach (LocationData item in e.NewItems) MappedLocations.Add(item);
                if (e.OldItems != null) foreach (LocationData item in e.OldItems) MappedLocations.Remove(item);
            };
        }

        private void InitializeTimers()
        {
            // Update the travel times every 5 minutes.
            Helpers.StartTimer(5, async () =>
            {
                var currentLocation = await GetCurrentLocationAsync();
                if (currentLocation != null) await TryUpdateLocationsTravelInfoAsync(Locations, currentLocation);
            });

            // Update the freshness timestamp every minute.
            Helpers.StartTimer(1, () => { foreach (var location in Locations) location.RefreshFormattedTimestamp(); });
        }

        private async void InitializeData()
        {
            // Load location data from storage if it exists;
            // otherwise, load sample location data.
            var locations = await LocationDataStore.GetLocationDataAsync();
            if (locations.Count == 0) locations = await LocationDataStore.GetSampleLocationDataAsync();
            foreach (var location in locations) this.Locations.Add(location);

            // Raise the MapViewReset event to refresh the bounds of the map control. 
            ResetMapView();

            // Update the locations when access to the geolocator or the network is restored after interruption. 
            // These event handlers are added after loading the data so that the view doesn't get refreshed 
            // before there is something to show.
            HandleStatusChangedEvents();
        }

        private void HandleStatusChangedEvents()
        {
            LocationHelper.Geolocator.StatusChanged += async (s, e) =>
            {
                switch (e.Status)
                {
                    case PositionStatus.Initializing: break;
                    case PositionStatus.Ready: Helpers.CallOnUiThreadAsync(() => IsGeolocatorAvailable = true).Wait(); break;
                    default: await Helpers.CallOnUiThreadAsync(() => IsGeolocatorAvailable = false); break;
                }
            };
            NetworkInformation.NetworkStatusChanged += async s => await Helpers.CallOnUiThreadAsync(() =>
                IsNetworkAvailable = (NetworkInformation.GetInternetConnectionProfile() != null));
        }

        /// <summary>
        /// Gets or sets the saved locations. 
        /// </summary>
        public ObservableCollection<LocationData> Locations { get; private set; }

        /// <summary>
        /// Gets or sets the locations represented on the map; this is a superset of Locations, and 
        /// includes the current location and any locations being added but not yet saved. 
        /// </summary>
        public ObservableCollection<LocationData> MappedLocations { get; private set; }

        /// <summary>
        /// Gets or sets the SelectedLocation property value as type object so that 
        /// UI properties of type object can x:Bind to it (because x:Bind uses strong typing). 
        /// </summary>
        public object SelectedItem
        {
            get { return SelectedLocation; }
            set { SelectedLocation = value as LocationData; }
        }

        private LocationData _selectedLocation;

        /// <summary>
        /// Gets or sets the LocationData object corresponding to the current selection in the locations list. 
        /// </summary>
        public LocationData SelectedLocation
        {
            get { return _selectedLocation; }
            set
            {
                if (_selectedLocation != value)
                {
                    if (_selectedLocation != null)
                    {
                        _selectedLocation.IsSelected = false;
                        _selectedLocation.PropertyChanged -= SelectedLocation_PropertyChanged;
                    }
                    _selectedLocation = value;
                    if (_selectedLocation != null)
                    {
                        _selectedLocation.IsSelected = true;
                        _selectedLocation.PropertyChanged += SelectedLocation_PropertyChanged;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedItem));
                }
            }
        }

        private void SelectedLocation_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) => 
            OnPropertyChanged(nameof(SelectedLocationFastestRoute));

        public MapRoute SelectedLocationFastestRoute => SelectedLocation?.FastestRoute; 

        public LocationData SelectedLocationEditCopy { get; set; } = new LocationData();

        private BasicGeoposition SelectedLocationCachedPosition { get; set; }

        private bool _isInListDisplayMode = true;

        public bool IsInListDisplayMode
        {
            get { return _isInListDisplayMode; }
            set
            {
                if (SetProperty(ref _isInListDisplayMode, value))
                {
                    OnPropertyChanged(nameof(LocationsViewVisibility));
                    OnPropertyChanged(nameof(ListDisplayModeButtonLabel));
                    OnPropertyChanged(nameof(ListDisplayModeButtonIcon));
                }
            }
        }

        public void ToggleListDisplayMode() => IsInListDisplayMode = !IsInListDisplayMode;

        public Visibility LocationsViewVisibility =>  
            IsInListDisplayMode && !IsInMapSelectionMode ? Visibility.Visible : Visibility.Collapsed;

        public string ListDisplayModeButtonLabel => $"{(IsInListDisplayMode ? "Hide" : "Show")} locations list";

        private SymbolIcon _openSymbolIcon = new SymbolIcon { Symbol = Symbol.OpenPane };
        private SymbolIcon _closeSymbolIcon = new SymbolIcon { Symbol = Symbol.ClosePane };

        public SymbolIcon ListDisplayModeButtonIcon => IsInListDisplayMode ? _closeSymbolIcon : _openSymbolIcon;

        public bool IsListDisplayModeButtonEnabled => !IsInMapSelectionMode;

        private bool _isNetworkAvailable = true;

        public bool IsNetworkAvailable
        {
            get { return _isNetworkAvailable; }
            set
            {
                if (SetProperty(ref _isNetworkAvailable, value))
                {
                    OnPropertyChanged(nameof(MapServicesDisabledMessageVisibility));
                    if (_isNetworkAvailable) ResetMapView();
                }
            }
        }

        public Visibility MapServicesDisabledMessageVisibility => 
            IsNetworkAvailable ? Visibility.Collapsed : Visibility.Visible;

        private bool _isGeolocatorAvailable = true;

        public bool IsGeolocatorAvailable
        {
            get { return _isGeolocatorAvailable; }
            set
            {
                if (SetProperty(ref _isGeolocatorAvailable, value))
                {
                    OnPropertyChanged(nameof(LocationDisabledMessageVisibility));
                    if (_isGeolocatorAvailable) ResetMapView();
                }
            }
        }

        public Visibility LocationDisabledMessageVisibility => 
            IsGeolocatorAvailable ? Visibility.Collapsed : Visibility.Visible;

        private bool _isInMapSelectionMode = false;

        public bool IsInMapSelectionMode
        {
            get { return _isInMapSelectionMode; }
            set
            {
                if (SetProperty(ref _isInMapSelectionMode, value))
                {
                    OnPropertyChanged(nameof(LocationsViewVisibility));
                    OnPropertyChanged(nameof(MapSelectionModeMessageVisibility));
                    OnPropertyChanged(nameof(IsListDisplayModeButtonEnabled));
                    OnPropertyChanged(nameof(IsAddCurrentLocationButtonEnabled));
                    OnPropertyChanged(nameof(IsAddNewLocationButtonEnabled));
                }
            }
        }

        private bool _isRouteShowing = true;

        public bool IsRouteShowing
        {
            get { return _isRouteShowing; }
            set { SetProperty(ref _isRouteShowing, value); }
        }

        public Visibility MapSelectionModeMessageVisibility =>
            IsInMapSelectionMode ? Visibility.Visible : Visibility.Collapsed;

        private bool _isInEditMode = false;

        public bool IsInEditMode
        {
            get { return _isInEditMode; }
            set
            {
                if (SetProperty(ref _isInEditMode, value) && _isInEditMode)
                {
                    SelectedLocationEditCopy.Copy(SelectedLocation);
                    SelectedLocationCachedPosition = SelectedLocation.Position;
                }
            }
        }

        private bool IsNewLocationInEdit { get; set; }

        public bool IsAddNewLocationButtonEnabled => !IsInMapSelectionMode;

        private bool _isAddCurrentLocationButtonEnabled = true;

        public bool IsAddCurrentLocationButtonEnabled
        {
            get { return _isAddCurrentLocationButtonEnabled && !IsInMapSelectionMode; }
            set { SetProperty(ref _isAddCurrentLocationButtonEnabled, value); }
        }

        /// <summary>
        /// Gets the current location if the geolocator is available, 
        /// and updates the IsGeolocatorAvailable property depending on the results.
        /// </summary>
        /// <returns>The current location.</returns>
        public async Task<LocationData> GetCurrentLocationAsync()
        {
            var currentLocation = await LocationHelper.GetCurrentLocationAsync();
            IsGeolocatorAvailable = (currentLocation != null);
            return currentLocation;
        }

        /// <summary>
        /// Attempts to update the travel distance and time info for the specified locations, 
        /// relative to the current location, and raises an alert for each flagged location 
        /// if traffic is currently increasing the travel time by 10 minutes or more; also 
        /// updates the network status message depending on the results.
        /// </summary>
        public async Task<bool> TryUpdateLocationsTravelInfoAsync(IEnumerable<LocationData> locations, LocationData currentLocation)
        {
            IsNetworkAvailable = await LocationHelper.TryUpdateLocationsTravelInfoAsync(Locations, currentLocation);
            return IsNetworkAvailable;
        }

        public async Task ShowRouteToSelectedLocationInMapsAppAsync()
        {
            var currentLocation = await GetCurrentLocationAsync();
            if (currentLocation != null)
            {
                await LocationHelper.ShowRouteToLocationInMapsAppAsync(SelectedLocation, currentLocation);
            }
        }

        public async Task SetTrackingForSelectedLocationAsync(bool isTracked)
        {
            SelectedLocation.IsMonitored = isTracked;
            this.UpdateTrafficMonitor(isTracked);
            await LocationDataStore.SaveLocationDataAsync(Locations);
        }

        /// <summary>
        /// Registers or unregisters the traffic monitoring background task depending 
        /// on whether the number of tracked locations changes from 1 to 0 or from 0 to 1.
        /// </summary>
        /// <param name="isIncrement">true if a location was just flagged; 
        /// false if a location was just unflagged.</param>
        private void UpdateTrafficMonitor(bool isIncrement)
        {
            var monitoredLocationCount = Locations.Count(location => location.IsMonitored);
            if (isIncrement && monitoredLocationCount == 1) LocationHelper.RegisterTrafficMonitor();
            else if (monitoredLocationCount == 0) LocationHelper.UnregisterTrafficMonitor();
        }

        public async Task DeleteSelectedLocationAsync()
        {
            var locationToDelete = SelectedLocation;
            SelectedLocation = null; 
            Locations.Remove(locationToDelete);
            await LocationDataStore.SaveLocationDataAsync(Locations);
        }

        public async Task AddLocationAsync(LocationData location)
        {
            // Resolve the address given the geocoordinates. In this case, because the 
            // location is unambiguous, there is no need to pass in the current location.
            await LocationHelper.TryUpdateMissingLocationInfoAsync(location, null);

            EditNewLocation(location);
        }

        public async Task AddCurrentLocationAsync()
        {
            IsAddCurrentLocationButtonEnabled = false;
            var currentLocation = await GetCurrentLocationAsync();
            if (currentLocation != null)
            {
                // Resolve the address given the geocoordinates.
                await LocationHelper.TryUpdateMissingLocationInfoAsync(currentLocation, currentLocation);

                this.EditNewLocation(currentLocation);
            }
            IsAddCurrentLocationButtonEnabled = true;
        }

        public void AddNewLocation() => EditNewLocation(new LocationData());

        public void EditNewLocation(LocationData location)
        {
            IsNewLocationInEdit = true;
            IsRouteShowing = false;
            IsInListDisplayMode = true;
            Locations.Add(location);
            SelectedLocation = location;
            EditSelectedLocation();
        }

        public void EditSelectedLocation() => IsInEditMode = true;

        public async Task UpdatePositionForSelectedLocationAsync(BasicGeoposition position)
        {
            IsInMapSelectionMode = false;

            // Don't bother showing the fastest route to the new location until the user saves the change. 
            IsRouteShowing = false;

            // Update SelectedLocation so the new position appears on the map. 
            // (Note that the original position of SelectedLocation is cached when entering edit mode.)
            SelectedLocation.Position = position;

            // Update the edit copy as well, and resolve the new address so that it appears in the edit flyout.  
            SelectedLocationEditCopy.Position = position;
            SelectedLocationEditCopy.Address = string.Empty;
            await LocationHelper.TryUpdateMissingLocationInfoAsync(SelectedLocationEditCopy, null);
        }

        public void EnterMapSelectionMode() => IsInMapSelectionMode = true;

        public void LeaveMapSelectionMode() => IsInMapSelectionMode = false;

        /// <summary>
        /// Reverts all edits if the location editor closed because the user has cancelled the edit. 
        /// </summary>
        public void OnEditorClosed()
        {
            // Do nothing if the editor closed in order to enter map selection mode 
            // or because the user has saved any changes they have just made. 
            if (IsInMapSelectionMode || !IsInEditMode) return;

            if (IsNewLocationInEdit)
            {
                // If a new location was in edit, delete it. 
                SelectedLocation = null;
                Locations.RemoveAt(Locations.Count - 1);
                IsNewLocationInEdit = false;
            }
            else
            {
                // If an existing location was in edit, revert its position. 
                // (This has no effect if the position was never altered.)
                SelectedLocation.Position = SelectedLocationCachedPosition;
            }

            IsInEditMode = false;
            IsRouteShowing = true;
        }

        /// <summary>
        /// Saves the edited location, resolving the address first if the geoposition was changed.  
        /// </summary>
        public async Task SaveAsync()
        {
            IsInEditMode = false;
            IsNewLocationInEdit = false;

            bool isAddressNew = SelectedLocationEditCopy.Address != SelectedLocation.Address;
            bool areCoordinatesNew = !SelectedLocationEditCopy.Position.Equals(SelectedLocationCachedPosition);

            // If just the address OR just the coordinates are new, 
            // clear the other value so that it can be updated.
            if (isAddressNew ^ areCoordinatesNew)
            {
                if (isAddressNew) SelectedLocationEditCopy.Position = new BasicGeoposition();
                if (areCoordinatesNew) SelectedLocationEditCopy.Address = string.Empty;
            }

            // If the address, the coordinates, or both have changed, clear the travel 
            // info and the route so that it doesn't reflect the old position.
            if (isAddressNew || areCoordinatesNew)
            {
                SelectedLocationEditCopy.ClearTravelInfo();
                IsRouteShowing = false;
            }

            SelectedLocation.Copy(SelectedLocationEditCopy);

            var currentLocation = await GetCurrentLocationAsync();
            if (currentLocation != null)
            {
                if (isAddressNew ^ areCoordinatesNew)
                {
                    await LocationHelper.TryUpdateMissingLocationInfoAsync(SelectedLocation, currentLocation);
                }
            }

            await LocationDataStore.SaveLocationDataAsync(Locations);

            if (currentLocation != null)
            {
                bool isNetworkAvailable = await TryUpdateLocationsTravelInfoAsync(Locations, currentLocation);
                if (isNetworkAvailable) IsRouteShowing = true;
            }
        }

        /// <summary>
        /// Updates the MappedLocations collection to account for the user's current position, if available; 
        /// this resets the display bounds of the data-bound MapControl declared in MainPage.xaml. 
        /// </summary>
        private async void ResetMapView()
        {
            if (!IsGeolocatorAvailable || !IsNetworkAvailable) return;

            LocationData currentLocation = await GetCurrentLocationAsync();
            if (currentLocation != null)
            {
                if (MappedLocations.Count > 0 && MappedLocations[0].IsCurrentLocation) MappedLocations.RemoveAt(0);
                MappedLocations.Insert(0, new LocationData { Position = currentLocation.Position, IsCurrentLocation = true });
                await TryUpdateLocationsTravelInfoAsync(Locations, currentLocation);
                MapViewReset(this, EventArgs.Empty);
            }
        }

        public event EventHandler MapViewReset = delegate { };
    }
}

