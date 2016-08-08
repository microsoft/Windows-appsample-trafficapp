# Traffic app project overview

For the Traffic app, our goal was to create a UWP app that is useful on both PC and mobile devices, with a single UI that adapts to different screen sizes. We also wanted to take advantage of location and map APIs and services, and provide a foundation for future integration of external services such as traffic camera feeds and platform features such as Cortana. 

![Traffic app screenshot](/Images/TrafficApp.png)

We started by creating a simple prototype using Windows 8.1. At the time, Windows 10 was still in primary development, and we wanted to make fast progress in a stable environment while also understanding at least some of what an existing app would have to do to take full advantage of Windows 10 platform features. 

## LocationHelper, before and after

The first version was just a list of locations, with no map control, using the Maps app to display routes and the Bing maps service to look up addresses and to calculate travel distances and times. This version included the LocationHelper class, but it was a lot more complex.

In particular, the initial versions of the LocationHelper class used the Bing maps service, so there was a lot of code to craft appropriate URIs, use HttpClient to retrieve JSON strings from the service, and parse the JSON into usable values. All of this code has now been replaced with the vastly simpler MapLocationFinder and MapRouteFinder APIs in Windows 10.

```c#
MapLocationFinderResult locationResult = await 
	MapLocationFinder.FindLocationsAsync(location.Address, currentLocation.Geopoint);
	
MapRouteFinderResult routeResult = await
	MapRouteFinder.GetDrivingRouteAsync(currentLocation.Geopoint, location.Geopoint,
	    MapRouteOptimization.TimeWithTraffic, MapRouteRestrictions.None);
```

## Code behind, converted to MVVM

We decided to start by creating a conventional code-behind app because the UI was so simple and it was a prototype anyway. We also reasoned that we would then convert it to a Model-View-ViewModel (MVVM) architecture, and we would document the conversion. This kind of conversion experience is useful to understand exactly when and why MVVM becomes more useful than a code-behind architecture.

The code-behind version had reached the point where additional growth would increase the complexity unnecessarily, making it ever harder to test, and increasingly brittle with unexpected side effects. So the conversion to MVVM made practical sense. We had tried to keep the code-behind version well organized to demonstrate a fair conversion, and of course the factoring of functionality in the code-behind version (with the LocationHelper classes) was already a good step toward a clean separation of concerns. So the MVVM conversion was fairly straightforward. 

For the original, code-behind version, see the [master branch](../..). For the MVVM version, see the [MVVM branch](../../tree/MVVM). For more info on the differences between the two versions, see the [MVVM conversion notes](../../tree/MVVM/MVVM.md). 

## Data binding

Even in the original code-behind version, the LocationData class is effectively a view model because it provides a binding source and implements INotifyPropertyChanged (indirectly, by inheriting the BindableBase helper class). 

Of course, all the code-behind in the original version keeps it from being a true MVVM example, but the key to MVVM is the separation of concerns enabled by data binding. The value of MVVM is more a matter of how separated; fully decoupled code modules are independently testable and more easily replaceable. However, even partial separation is a useful step in the right direction, and even very basic data binding gives you that.

Both versions of the traffic app use binding in several places. First, the LocationTemplate and InputMapItemTemplate make use of the new x:Bind markup extension to connect UI elements to properties of the LocationData class. The app also uses the existing Binding markup extension in the LocationsViewItemStyle to bind the ListViewItem.IsSelected property to a Visibility property in the control template. This binding enables the selected item in the locations ListView to display a row of buttons.

![The selected location in the locations list](/Images/SelectedLocation.png)

The existing Binding markup extension is also used in the editor Flyout declared at the end of the LocationTemplate. That way, the flyout can have a different data source than the ListViewItem it is attached to. Specifically, the DataContext of the flyout's content is set to a copy of the selected location so that the user can discard any changes if necessary. If the user saves the changes, the values are copied to the original location object.

The bindings in the InputMapItemTemplate use x:Bind, but they bind to non-string values, unlike the other bindings. A Visibility binding is used with a reversible BooleanToVisibilityConverter in order to show or hide the appropriate icon depending on whether it's for the user's current location or a saved one. 

![App closeup showing different map icons](/Images/Icons.png)

```xaml
<!-- This DataTemplate provides the UI for the locations as they appear in the MapControl. -->
<DataTemplate x:Key="InputMapItemTemplate" x:DataType="loc:LocationData">
	<Grid>
	
	    <!-- The geopoint icon is used for the current location. -->
	    <TextBlock Text="&#xE1D2;" FontFamily="Segoe MDL2 Assets" FontSize="40" 
	        Visibility="{x:Bind IsCurrentLocation, Converter={StaticResource BooleanToVisibilityConverter}}"
	        maps:MapControl.Location="{x:Bind Geopoint, Mode=OneWay}" 
	        maps:MapControl.NormalizedAnchorPoint="{x:Bind NormalizedAnchorPoint, Mode=OneWay}"/>
	
	    <!-- The custom map pin image is used for saved locations. -->
	    <Image Source="{x:Bind ImageSource, Mode=OneWay}" Width="38" Height="60"
	        Visibility="{x:Bind IsCurrentLocation, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Reverse}"
	        maps:MapControl.Location="{x:Bind Geopoint, Mode=OneWay}" 
	        maps:MapControl.NormalizedAnchorPoint="{x:Bind NormalizedAnchorPoint, Mode=OneWay}"/>
	
	</Grid>
</DataTemplate>
```

The other bindings in this template target some attached properties exposed by the MapControl class. MapControl.Location binds to the LocationData.Geopoint property, which exists to convert the LocationData.Position property (of type BasicGeoposition) into the Geopoint type required by the binding. MapControl.NormalizedAnchorPoint binds to the LocationData.NormalizedAnchorPoint property, which returns the Point value appropriate for the map icon to be used (which depends on the IsCurrentLocation property). 

## MapControl for user input

The current version of the app uses the MapControl for user input and to display the current and saved locations and the route to the selected location. Although the app displays routes in the MapControl, it still provides the option to display the route using the Maps app instead. This is a handy way to take advantage of built-in, more robust functionality that the user may be using extensively already.  

We first added the MapControl to solve the user input problem. The first version of the app enabled the user to type the address of a new position. For typed addresses, however, there is a greater likelihood of getting ambiguous results from the address lookup service. We felt that, if this is the main way to add new locations, we'd need a system for validating and disambiguating results. With input from the map control, however, the geoposition would be unambiguous. And with map input as the main way to add new (non-current) locations, the address ambiguity problem would no longer have any urgency. 

For map input, the user can tap and hold to add the held location to the list. (This is the same gesture used by the Maps app.) The user can also edit a location and click the Map button, which causes the app to enter "map selection mode" where a simple tap on the map control repositions the selected location.

Because the map control is the primary form of user input, we needed a way to support its use on smaller screens. The main problem is that the locations list obscures the app. The problem is therefore solved by providing an app-bar button so that the user can hide the list at will (and thereby use the Holding gesture), and by automatically hiding the list in map selection mode. 

## MapControl for display

When we had the map control working for user input, it became obvious that we needed to display the current locations as well or the app would seem too incomplete even for a V1. 

The MapControl provides multiple display options. For the first version of the app, we simply copied code from the MapControlSample (in the [Windows-universal-samples](https://github.com/Microsoft/Windows-universal-samples) repo) that programmatically added pin icons to the MapControl.MapElements collection. This started getting a bit complicated when we wanted to show pins not only for saved locations, but for new and repositioned locations that had not yet been saved. This required us to manipulate the MapElements collection depending on the editing state. 

Next, we decided to show the user's current position, using a slightly different pin icon. This worked well enough, except that the "target" icon is now the universal symbol for "you are here", so we felt the need to switch to that. However, this required us to either create a bitmap version of the target icon, or switch approaches. So we decided to use the data binding approach enabled by the MapItemsControl class and demonstrated in another scenario of the MapControlSample. This enabled us to take easy advantage of the new Segoe MDL2 Assets font. Although we are using it for the target icon only at this time, it is nice to know that a giant collection of scalable icons are easily placed on the map control using this technique. And of course, the binding approach still enables the use of bitmap icons. 

When that was all working correctly, it became a relatively simple matter to make the necessary adjustments to highlight the pin associated with the currently selected location. This is handled entirely through data binding in the InputMapItemTemplate. 

The last piece for the maps display was to add the route to the selected location. The LocationFinder class was already using MapRouteFinder to calculate travel times, and that class returns a MapRoute object with all the relevant info. It was a simple matter to store the returned route object in the LocationData class, and use that to populate the MapControl.Routes object whenever selection changes. For that code, see MainPage.SelectedLocation.

![App closeup showing a route on the map](/Images/Route.png)

## The locations list
 
We initially used a SplitView control for the locations list, but this didn't quite work because it doesn't have an IsSticky property like the app bar does, which prevents the light-dismiss behavior used to hide the app bar and flyouts when you click somewhere else. We tried customizing the control template to get rid of the light dismiss behavior, but after looking at the Maps app and other apps, we realized that the SplitView is intended for navigation, including the kind of navigation that just shows or hides other lists. For example, in the Maps app, most of the buttons in the side bar display associated panels, but those panels are not part of the SplitView. This is obvious when you have one of the panels open and you click the Hamburger button, which then displays the flyout navigation menu on top of the open panel.

Because we currently only have one list, there is no need for navigation in the Traffic app just yet, and therefore no need for a SplitView control. This will be a useful control for a future version that includes a list of saved traffic cameras, for example.  

One additional requirement for the locations list is that it should float on top of the map control. The visual effect was easy to achieve by setting the appropriate Opacity property in the ListViewItem control template, but it was more complicated to enable proper handling of user gestures. The main issue was that the ListView takes up the entire vertical space even if there are only enough items to fill some of the space. The remaining space can be transparent, so the map control can show through, but gestures still go to the ListView instead of to the map. 

To fix this issue, we first determined exactly which UI element was getting the gestures over the transparent part of the ListView. This was easy enough to determine in Visual Studio 2015 using the Live Visual Tree window with the option to "Enable selection in the running application". With this setup, we just clicked the empty space, and the element that intercepted the click was highlighted in the Live Visual Tree window.

![Screenshot of the app and Visual Studio showing the Live Visual Tree and Live Property Explorer windows](/Images/LiveVisualTree.png)

Finding the element in the visual tree, we then used the Live Property Explorer window to experimentally reset the Background property by clicking the box to the right of the property and choosing Reset. After doing this, clicking the same space highlighted a different element that includes the map control. Turning off the "Enable selection in the running application" revealed that the map control was now draggable below the ListView control. 

Next, it was a small matter to make the necessary adjustment in XAML. We used the Document Outline window to select the Grid element we'd modified in the Live Property Explorer. We right-clicked the icon in the outline, then selected Edit Template | Edit a Copy | Define in Application. In the template XAML, we changed the ScrollViewer element to add Background="{TemplateBinding Background}", causing it to inherent the ListView.Background property value. This achieved the same effect as using resetting the background at run time because we didn't set the Background property on the ListView. By using a TemplateBinding, the Grid inherits the default value, but we can override this behavior at will by adding a Background attribute to the ListView element. 

## Known issues and feedback request

The current release is intended as a preview while we finish some additional work. The goal for the initial release is to demonstrate basic working functionality, not completely robust functionality, so there is some polishing to do, particularly around address and route lookup failures and improved handling of connectivity/service failures. 

Another goal of this project is to show how to integrate multiple platform features, so if there is anything in particular that you'd like to see in this sample, let us know on the Issues list. 
