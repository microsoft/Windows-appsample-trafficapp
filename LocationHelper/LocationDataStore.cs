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
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace Location
{
    /// <summary>
    /// Provides access to stored location data. 
    /// </summary>
    public static class LocationDataStore
    {
        private const string dataFileName = "TrafficAppData.txt";

        /// <summary>
        /// Gets a list of four sample locations randomply positioned around the user's current 
        /// location or around the Microsoft main campus if the Geolocator is unavailable. 
        /// </summary>
        /// <returns>The sample locations.</returns>
        public static async Task<List<LocationData>> GetSampleLocationDataAsync()
        {
            var center = (await LocationHelper.GetCurrentLocationAsync())?.Position ?? 
                new BasicGeoposition { Latitude = 47.640068, Longitude = -122.129858 };

            int latitudeRange = 36000;
            int longitudeRange = 53000;
            var random = new Random();
            Func<int, double, double> getCoordinate = (range, midpoint) => 
                (random.Next(range) - (range / 2)) * 0.00001 + midpoint;

            var locations =
                (from name in new[] { "Work", "Home", "School", "Friend" }
                 select new LocationData
                 {
                     Name = name,
                     Position = new BasicGeoposition
                     {
                         Latitude = getCoordinate(latitudeRange, center.Latitude),
                         Longitude = getCoordinate(longitudeRange, center.Longitude)
                     }
                 }).ToList();

            await Task.WhenAll(locations.Select(async location =>
                await LocationHelper.TryUpdateMissingLocationInfoAsync(location, null)));

            return locations;
        }

        /// <summary>
        /// Load the saved location data from roaming storage. 
        /// </summary>
        public static async Task<List<LocationData>> GetLocationDataAsync()
        {
            List<LocationData> data = null;
            try
            {
                StorageFile dataFile = await ApplicationData.Current.RoamingFolder.GetFileAsync(dataFileName);
                string text = await FileIO.ReadTextAsync(dataFile);
                byte[] bytes = Encoding.Unicode.GetBytes(text);
                var serializer = new DataContractJsonSerializer(typeof(List<LocationData>));
                using (var stream = new MemoryStream(bytes))
                {
                    data = serializer.ReadObject(stream) as List<LocationData>;
                }
            }
            catch (FileNotFoundException)
            {
                // Do nothing.
            }
            return data ?? new List<LocationData>();
        }

        /// <summary>
        /// Save the location data to roaming storage. 
        /// </summary>
        /// <param name="locations">The locations to save.</param>
        public static async Task SaveLocationDataAsync(IEnumerable<LocationData> locations)
        {
            StorageFile sampleFile = await ApplicationData.Current.RoamingFolder.CreateFileAsync(
                dataFileName, CreationCollisionOption.ReplaceExisting);
            using (MemoryStream stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(List<LocationData>));
                serializer.WriteObject(stream, locations.ToList());
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream))
                {
                    string locationString = reader.ReadToEnd();
                    await FileIO.WriteTextAsync(sampleFile, locationString);
                }
            }
        }

    }
}
