# Traffic App sample

**Traffic App** is a sample Universal Windows Platform (UWP) app for commuters to track drive times to various locations. The user adds frequently-visited destinations using app-bar buttons or by tapping and holding on the map. Traffic App then provides a quick view into how long it will take to drive to each place from the user’s current location, following the fastest route. The user can also flag a particular destination and a background task will periodically monitor traffic to that location, alerting the user when traffic is adding ten minutes or more to the travel time. 

For a description of the goals and challenges of this project, see the [project overview](ProjectOverview.md).

![Traffic app screenshot](/Images/TrafficApp.png)

## Features

Traffic App highlights the following APIs:

* MapControl and MapItemsControl (Windows.UI.Xaml.Controls.Maps)
* Geolocator (Windows.Devices.Geolocation)
* MapLocationFinder and MapRouteFinder (Windows.Services.Maps)
* Background tasks (Windows.ApplicationModel.Background)
* Toast notifications (Windows.UI.Notifications)
* Local storage and serialization (Windows.Storage)

## Universal Windows Platform development

This sample requires Visual Studio 2015 RC and the Windows Software Development Kit (SDK) for Windows 10. To get Windows 10 Insider Preview and the software development kits and tools, join the [Windows Insider Program](https://insider.windows.com/ "Become a Windows Insider").

## Running the sample

Traffic App needs a Bing Maps key to run with full functionality. For security reasons, we can't provide a key as part of the sample - you'll need to get your own at https://www.bingmapsportal.com. Once you have a key, insert it into code in the LocationHelper class constructor and in the MapControl element in MainPage.xaml (you can find these easily by searching the solution for "ServiceToken").

**Note:** The platform target currently defaults to ARM, so be sure to change that to x64 or x86 if you want to test on a non-ARM device. 

The default project is TrafficApp and you can Start Debugging (F5) or Start Without Debugging (Ctrl+F5) to try it out. The app will run in the emulator or on physical devices. If you run in the emulator, be sure to set your location in the location area of the emulator - otherwise, the app won't work correctly.

By default, the app generates four sample locations at random positions around your current location. You can switch to "live mode" instead by changing the commented lines in MainPage.OnNavigatedTo so that locations are loaded from roaming storage.

## Code at a glance

If you’re just interested in code snippets for certain API and don’t want to browse or run the full sample, check out the following files for examples of some highlighted features:

* LocationHelper.cs
	- Getting the user’s current location using the Geolocator class.
	- Getting the route, travel time, and distance to a location using the MapRouteFinder class. 
	- Getting the address for a particular position using the MapLocationFinder class.
	- Displaying a toast notification.
* MainPage.xaml and MainPage.xaml.cs: 
	- Using MapControl and MapItemsControl, handling user gestures on the map. 
	- Displaying locations in the map and list controls using item templates and data binding (including x:Bind).
	- Using a Flyout for location editing, handling the editing experience and edit cancellation.
	- Displaying commands in an app bar.
* TrafficMonitor.cs:
	- Running background tasks on a specified interval.
* LocationDataStore.cs
	- Generating sample locations at random positions.
	- Serializing data and reading/writing it to roaming storage.