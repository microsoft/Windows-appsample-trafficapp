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

using Location;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;

namespace TrafficApp
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; set; } = new MainViewModel();

        public MainPage()
        {
            InitializeComponent();
            ViewModel.MapViewReset += async (s, e) => await OnMapViewReset();
            ViewModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        private async Task OnMapViewReset()
        {
            if (ViewModel.MappedLocations.Count == 1)
            {
                InputMap.Center = new Geopoint(ViewModel.MappedLocations[0].Position);
                InputMap.ZoomLevel = 12;
            }
            else if (ViewModel.MappedLocations.Count > 1)
            {
                var bounds = GeoboundingBox.TryCompute(ViewModel.MappedLocations.Select(loc => loc.Position));
                double viewWidth = ApplicationView.GetForCurrentView().VisibleBounds.Width;
                var margin = new Thickness((viewWidth >= 500 ? 300 : 10), 10, 10, 10);
                await InputMap.TrySetViewBoundsAsync(bounds, margin, MapAnimationKind.Default);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(ViewModel.IsInEditMode): UpdateFlyout(isEditModeChanging: true); break;
                case nameof(ViewModel.IsInMapSelectionMode): UpdateMapSelectionMode(); break;
                case nameof(ViewModel.SelectedLocation):
                case nameof(ViewModel.SelectedLocationFastestRoute):
                case nameof(ViewModel.IsRouteShowing): UpdateRouteDisplay(); break;
                default: break;
            }
        }

        private void UpdateFlyout(bool isEditModeChanging)
        {
            var item = LocationsView.ContainerFromItem(ViewModel.SelectedLocation) as ListViewItem;
            if (item == null) return;

            var element = item.ContentTemplateRoot as FrameworkElement;
            var flyout = Flyout.GetAttachedFlyout(element) as Flyout;

            if (ViewModel.IsInEditMode && !ViewModel.IsInMapSelectionMode)
            {
                // If edit mode is just beginning, then set the data context. 
                if (isEditModeChanging) (flyout.Content as FrameworkElement).DataContext = ViewModel.SelectedLocationEditCopy;

                flyout.ShowAt(element);
            }
            else flyout.Hide();
        }

        private void UpdateMapSelectionMode()
        {
            UpdateFlyout(isEditModeChanging: false);

            if (ViewModel.IsInMapSelectionMode)
            {
                InputMap.MapTapped += InputMap_MapTapped;
                InputMap.MapHolding -= InputMap_MapHolding;
            }
            else
            {
                InputMap.MapTapped -= InputMap_MapTapped;
                InputMap.MapHolding += InputMap_MapHolding;
            }
        }

        private void UpdateRouteDisplay()
        {
            LocationsView.UpdateLayout();
            InputMap.Routes.Clear();
            if (ViewModel.IsRouteShowing && ViewModel.SelectedLocation != null)
            {
                var route = ViewModel.SelectedLocation.FastestRoute;
                if (route != null) InputMap.Routes.Add(new MapRouteView(route));
            }
        }

        private async void InputMap_MapHolding(MapControl sender, MapInputEventArgs args) => 
            await ViewModel.AddLocationAsync(new LocationData { Position = args.Location.Position });

        private async void InputMap_MapTapped(MapControl sender, MapInputEventArgs args) => 
            await ViewModel.UpdatePositionForSelectedLocationAsync(args.Location.Position);

        private void EditLocation_Click(object sender, RoutedEventArgs e) => 
            ViewModel.EditSelectedLocation();

        private async void DeleteLocation_Click(object sender, RoutedEventArgs e) => 
            await ViewModel.DeleteSelectedLocationAsync();

        private async void ShowRouteButton_Click(object sender, RoutedEventArgs e) => 
            await ViewModel.ShowRouteToSelectedLocationInMapsAppAsync();

        private async void TrackButton_Click(object sender, RoutedEventArgs e) => 
            await ViewModel.SetTrackingForSelectedLocationAsync((sender as ToggleButton).IsChecked.Value);

        private async void FlyoutSave_Click(object sender, RoutedEventArgs e) => 
            await ViewModel.SaveAsync();

        private void Flyout_Closed(object sender, object e) => 
            ViewModel.OnEditorClosed();

        private void EnterMapSelectionMode(object sender, RoutedEventArgs e) => 
            ViewModel.EnterMapSelectionMode();

        private async void TextBox_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await ViewModel.SaveAsync();
        }
    }
}
