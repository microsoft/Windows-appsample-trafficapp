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

using System.Linq;
using Windows.ApplicationModel.Background;

namespace Location
{
    /// <summary>
    /// Provides helper methods for registering and unregistering background tasks. 
    /// </summary>
    public static class BackgroundTaskHelper
    {
        /// <summary>
        /// Registers a background task with the specified taskEntryPoint, name, trigger,
        /// and condition (optional).
        /// </summary>
        /// <param name="taskEntryPoint">Task entry point for the background task.</param>
        /// <param name="taskName">A name for the background task.</param>
        /// <param name="trigger">The trigger for the background task.</param>
        /// <param name="condition">Optional parameter. A conditional event that must be true for the task to fire.</param>
        /// <returns>The registered background task.</returns>
        public static BackgroundTaskRegistration RegisterBackgroundTask(
            string taskEntryPoint, string taskName, IBackgroundTrigger trigger, IBackgroundCondition condition)
        {
            // Check for existing registrations of this background task.
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name.Equals(taskName))
                {
                    // The task is already registered.
                    return task.Value as BackgroundTaskRegistration;
                }
            }

            // Register the background task.
            var builder = new BackgroundTaskBuilder { Name = taskName, TaskEntryPoint = taskEntryPoint };
            if (condition != null) builder.AddCondition(condition);
            builder.SetTrigger(trigger);

            return builder.Register();
        }

        /// <summary>
        /// Unregisters the background task with the specified name. 
        /// </summary>
        /// <param name="taskName">The name of the background task to unregister.</param>
        public static void UnregisterBackgroundTask(string taskName)
        {
            BackgroundTaskRegistration.AllTasks.First(task => 
                taskName.Equals(task.Value.Name)).Value.Unregister(cancelTask: true);
        }
    }
}
