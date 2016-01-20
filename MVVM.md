# Traffic app MVVM conversion notes

The purpose of the Model-View-ViewModel (MVVM) architecture is to separate UI logic from non-UI logic, making it easier to change them independently. There are many ways to do this, but in its simplest form, MVVM doesn't have to be complicated. This topic shows how the Traffic App sample was converted to MVVM using only built-in XAML features with minimal abstractions. 

## TrafficApp MVVM conversion

This section summarizes the TrafficApp MVVM conversion. If you are unfamiliar with the concepts and terminology of MVVM, the subsequent sections provide a brief elaboration.

### Moving the simple stuff 

Some things were easy to move directly from the code-behind file into a new MainViewModel class. Aside from the move, the only change required was to redirect the UI to the new view model. Specifically, we: 

  * Created a [MainViewModel](TrafficApp/MainViewModel.cs#L23) class, deriving it from a handy [BindableBase](LocationHelper/BindableBase.cs#L35) helper class (see below for more on this). 
  * Moved all properties from MainPage to MainViewModel, including Locations, MappedLocations, and SelectedLocation. 
  * Moved some event handlers from MainPage to MainViewModel, renaming them and removing the parameters. 
  * Added a MainPage.ViewModel property and initialized it to a new MainViewModel instance. 
  * Added "ViewModel" to the beginning of MainPage.xaml bindings (not including those in the Page.Resources block). For example:
    * **Before:** `ItemsSource="{x:Bind MappedLocations}"`
    * **After:** `ItemsSource="{x:Bind ViewModel.MappedLocations}"`
  * Changed some event handler declarations in MainPage.xaml to x:Bind expressions with paths to view-model methods. For example:
    * **Before:** `Tapped="TextBlock_Tapped"`
    * **After:** `Tapped="{x:Bind ViewModel.LeaveMapSelectionMode}"`

See the [diff of the XAML changes](../../commit/3fcd64bdfc06e742115193606095eac590b6ee17). 

All x:Bind expressions use the page itself as the root data source, so in the MVVM version, the redirected binding paths start with "ViewModel" to refer to the MainPage.ViewModel property. In this way, the UI properties and events are bound to UI-agnostic view-model properties and methods. The UI independence is also reflected in name changes such as the switch from the view-centric "TextBox_Tapped" to the more abstract and descriptive "LeaveMapSelectionMode". 

### Moving other event handlers

Next, we dealt with event handlers that weren't quite so easily separated. Some of the events can't bind directly to view-model methods because they are declared in XAML Style elements, where x:Bind is not supported. For other events, the event handlers require some data from the event. 

For both of these cases, we used code-behind event handlers that do nothing but call view-model methods, passing in data extracted from the event arguments if necessary. For events with data, we could have put the full event-handler signature in the view model and used event binding like normal. However, using the pass-through approach helps us keep the event specifics (like the event-args types) out of the view model.

Here's an example pass-through event handler:

```C#
private async void InputMap_MapTapped(MapControl sender, MapInputEventArgs args) =>  
    await ViewModel.UpdatePositionForSelectedLocationAsync(args.Location.Position); 
````

See the [pass-through event handlers in the code-behind](TrafficApp/MainPage.xaml.cs#L123-L153). 

### Tracking app state and providing converter properties

Next, we:
  * Added MainViewModel properties like IsGeolocatorAvailable to keep track of various app states and raise appropriate PropertyChanged events. 
  * Added MainViewModel properties like LocationDisabledMessageVisibility to expose some app state in formats and data types more suitable for UI binding.  
  * Changed some MainPage.xaml attribute values to x:Bind expressions with paths to the new view-model properties. For example:
      * **Before:** `Visibility="Collapsed"`
      * **After:** `Visibility="{x:Bind ViewModel.LocationDisabledMessageVisibility, Mode=OneWay}"`

In the non-MVVM version of the app, the code-behind makes direct changes to UI properties like Visibility. In the MVVM version, the UI properties are bound to view-model properties instead. The view model uses UI-agnostic properties to keep track of the app state, but exposes that state through wrapper properties that convert the values to whatever types or formats the UI needs.  

This isn't the cleanest separation because UI types like Visibility end up in the view model. However, putting the converter properties in the code behind (like we did with the pass-through event handlers) would require excessive extra code in order to raise change notifications for these properties from the view model. Putting them in the view model instead is clean enough as long as we don't accumulate a large number of them. If that happened, we could refactor things to achieve cleaner separation by using [IValueConverter](https://msdn.microsoft.com/library/windows/apps/windows.ui.xaml.data.ivalueconverter.aspx) implementations (not covered here). 

### Handling view-model events to update the UI directly

Finally, we dealt with UI features that aren't exposed as bindable properties. In these cases, we kept some code in the code-behind to manipulate the UI elements directly through appropriate methods. In the non-MVVM version of the app, this manipulation occurs in normal UI event handlers. In the MVVM version, however, it occurs only in response to PropertyChanged and other events from the view model. The effect is similar to what would have happened with view-model binding except that the event handler is implemented explicitly in the code-behind rather than being hidden behind XAML bindings. 

Here's how the code-behind handles view-model events:

```C#
ViewModel.MapViewReset += async (s, e) => await OnMapViewReset(); 
ViewModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName); 
```

See the [UI logic left in the code-behind](TrafficApp/MainPage.xaml.cs#L45-L121) for more details. 

## MVVM concepts and terminology

This section provides more detail about MVVM in the context of the TrafficApp sample.

### Code-behind files

Typical non-MVVM XAML apps put all their app logic in code-behind (in files such as MainPage.xaml.cs), where it can directly manipulate the associated UI (in files such as MainPage.xaml). This is convenient for exploring and prototying, but the tight coupling between app logic and UI logic makes it harder to change either one without having to change the other. Over time, as a codebase gets larger, it becomes prohibitively complex to make even small changes. 

MVVM helps maintain flexibility by decoupling the app logic and UI logic. The app logic is placed outside the code-behind file, and has no dependencies on UI particulars. Instead, it manipulates the UI only indirectly, by raising events. This flexibility is crucial at large scales, but it's useful even at small scales if you prefer an exploratory or "design by doing" style of coding.  

### Data logic

The first step toward separating different kinds of logic has already been done in the original, [non-MVVM version](../..) of Traffic App. The LocationHelper library encapsulates the data-related logic, including calls to the Geolocator, MapLocationFinder, MapRouteFinder, and storage APIs. It also provides a LocationData class, which encapsulates all the data to be exposed in the UI. 

Data logic is often trivial to separate from other logic, so it was useful to do this even in the non-MVVM version of Traffic App. In that version, the logic in the code-behind file makes calls to the LocationHelper library, and then updates the UI either directly, or by making changes to local properties that the UI binds to. 

### Data binding and UI logic

Data binding is the main way to achieve clean separation between UI and data logic because the only connection between the two is declared in XAML through Binding or x:Bind markup. This markup directs the built-in binding engine to transfer values between data objects and UI controls, usually in response to property change events. The required UI logic is provided by the system, so it automatically stays separate from other logic. 

You might call this approach a "Model-View" architecture, where the model is the data and the view is the XAML that binds to the data, plus everything in the code-behind file. This approach is common in XAML apps, and although it isn't MVVM by itself, it is the first step on the path toward MVVM.

Unfortunately, not every UI control is easily manipulated solely through bindings. Some UI features are available only through methods, which cannot be binding targets. This is why both versions of Traffic App still have some logic in the code-behind file that manipulates the UI directly. This UI logic is tightly coupled to the XAML, so it is a proper part of the view, making it appropriate for the code-behind file. The MVVM version of the app, however, invokes this code only in response to events raised by the separated app logic.  

### App logic 

In the "Model-View" approach, anything that isn't part of the view is part of the model. In MVVM, the Model and the ViewModel are both just model layers, but for different kinds of data and logic. You can have a single model layer or several model layers if you decide that it makes sense to separate things out that far. In fact, for small projects, there is often just a ViewModel layer with no separate Model layer. This is like the "Model-View" approach, but qualifies as MVVM as long as the app logic is not tightly coupled to the view logic.

Traffic App uses all three layers because it involves more than just the display of location data from the Model layer. It must also keep track of user choices such as list selection, and provide experiences such as the location editor. Each of these experiences involves data and logic that is essential for the app, but distinct from both the underlying data and the UI particulars. In the non-MVVM version of Traffic App, this app logic is mingled with the UI logic in the code-behind file. 

The term "view model" is really just a way of saying that the supplemental app data and logic represent the view's own model, a model for the view itself, built on top of any lower-level data model(s), and closest to the UI. The view model provides all the data and logic that that view needs regardless of where it comes from and how it is rendered. The UI logic, in contrast, deals with UI specifics, typically to implement control-specific workarounds when regular binding is unavailable due to the absence of suitable binding target properties.

### Separation

Putting different kinds of data and logic in different files isn't enough to properly separate them, and there are often plenty of explicit connections. The important kind of separation is to ensure that there are no references to higher levels from any lower levels. In other words, all dependencies are one-way dependencies flowing down from the view, which is at the highest level. The view can explicitly reference the view model, and the view model can explicitly reference the model, which is at the lowest level. But all communication in the other direction is through events, typically property-changed events monitored by the binding infrastructure. 

Event publishers don't need to have any dependencies on event subscribers, so communication by events is indirect enough to achieve meaningful separation. As long as the same events are raised for the same reasons, the code that raises the events can change arbitrarily without affecting the UI layer. Similarly, as long as the events are handled to generate appropriate experiences, the UI can change without requiring changes in the view model or other models. 

To make full use of the binding infrastructure, every layer that wants to communicate property value changes to higher layers must implement [INotifyPropertyChanged](https://msdn.microsoft.com/library/system.componentmodel.inotifypropertychanged.aspx) and raise appropriate [PropertyChanged](https://msdn.microsoft.com/library/system.componentmodel.inotifypropertychanged.propertychanged(v=vs.110).aspx) events. To help with this, you can derive from a standard helper class like [BindableBase](LocationHelper/BindableBase.cs#L35), or copy that code into your class. 

Properties that expose collections need to communicate changes in a slightly different way because it is the collection contents that change, not the collection property values (which stay set to the same collection instances). To communicate collection changes, use collection properties of type [ObservableCollection<T>](https://msdn.microsoft.com/en-us/library/windows/apps/ms668604(v=vs.105).aspx), which implements [INotifyCollectionChanged](https://msdn.microsoft.com/library/system.collections.specialized.inotifycollectionchanged.aspx) and raises [CollectionChanged](https://msdn.microsoft.com/library/system.collections.specialized.inotifycollectionchanged.collectionchanged(v=vs.110).aspx) events whenever items are added to or removed from the collection. 

For both property and collection changes, the binding engine subscribes to the change events and handles them by displaying the new values in the bound UI controls. Of course, your code-behind can also handle these events to invoke supplemental UI logic. To further supplement the binding system's built-in change-notification handling, each layer can also raise other events, which the higher layers can listen for explicitly. For example, in the MVVM version of Traffic App, event handlers in the code-behind listen for MainViewModel events like MapViewReset in order to manipulate the map control in ways that don't work well with binding.

## Conclusion

To summarize, in the MVVM version of Traffic App:
  * The view-model keeps track of UI-agnostic app state and provides access to the underlying model (the location data and logic). 
  * The view binds many UI properties directly to view-model and model properties, and binds many UI events directly to view-model methods. 
  * When a UI property cannot bind directly to a given view-model property, the view-model provides a wrapper property that exposes the app state in whatever format or data type the UI requires.
  * When a UI event cannot bind directly to a given view-model method or the method needs some data from the event, the view uses a code-behind event handler to call the view-model method, passing it any relevant data extracted from the event args. 
  * When a UI control exposes some features through methods only, the view calls those methods from code-behind in response to any relevant view-model events.
