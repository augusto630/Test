namespace FractalView
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class ActionExtension
    {
        private class DebounceControl
        {
            public Task LatestTask { get; set; }
        }

        private class FastThrottleControl
        {
            public Task DelayTask { get; set; }

            public Task ContinuationTask { get; set; }
        }

        /// <summary>
        /// Throttle an action
        /// </summary>
        /// <param name="action">Action to throttled</param>
        /// <param name="throttleControl">The previous task, create a object field for this ref, don't just pass a local field. This method will create and assign a task to this field when required.</param>
        /// <param name="lockObject">A lock object wrapped around the action</param>
        /// <param name="delay_ms">Throttling delay, i.e. how long is the interval between invoking the action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <param name="continuationOptions">Optional task continuation options when executing the action</param>
        /// <param name="taskScheduler">Optional task scheduler when executing the action</param>
        /// <returns></returns>
        public static Task Throttle(this Action action, ref object throttleControl, object lockObject = null, int delay_ms = 50, CancellationToken? token = null, TaskContinuationOptions? continuationOptions = null, TaskScheduler taskScheduler = null)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            var controlObject = throttleControl as Task;

            if (delay_ms <= 0)
            {
                throttleControl = Task.Factory.StartNew(
                    () =>
                    {
                        lock (lockObject ?? action)
                        {
                            action?.Invoke();
                        }
                    },
                    token ?? CancellationToken.None,
                    TaskCreationOptions.None,
                    taskScheduler ?? TaskScheduler.Default);

                return throttleControl as Task;
            }

            if (controlObject == null || controlObject?.Status == TaskStatus.RanToCompletion)
            {
                var delayTask = Task.Delay(delay_ms);
                delayTask.ContinueWith(
                    (task) =>
                    {
                        lock (lockObject ?? action)
                        {
                            action?.Invoke();
                        }
                    },
                    token ?? CancellationToken.None,
                    continuationOptions ?? TaskContinuationOptions.None,
                    taskScheduler ?? TaskScheduler.Default);

                throttleControl = delayTask;
            }

            return throttleControl as Task;
        }

        /// <summary>
        /// Throttle an action, do not invoke action on a separate thread, but block any calls after a short period
        /// Note: Calls might be handled on the caller thread as well as through a new <see cref="Task"/>
        /// </summary>
        /// <param name="action">Action to throttled</param>
        /// <param name="controlObject">The previous task, create a object field for this ref, don't just pass a local field. This method will create and assign a task to this field when required.</param>
        /// <param name="lockObject">A lock object wrapped around the action</param>
        /// <param name="delay_ms">Throttling delay, i.e. how long is the interval between invoking the action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <param name="continuationOptions">Optional task continuation options when executing the action</param>
        /// <param name="taskScheduler">Optional task scheduler when executing the action</param>
        /// <returns></returns>
        public static Task FastThrottle(this Action action, ref object controlObject, object lockObject = null, int delay_ms = 50, CancellationToken? token = null, TaskContinuationOptions? continuationOptions = null, TaskScheduler taskScheduler = null)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            if (!(controlObject is FastThrottleControl throttleControl))
            {
                throttleControl = new FastThrottleControl();
                controlObject = throttleControl;

                throttleControl.DelayTask = Task.Delay(delay_ms);

                lock (lockObject ?? action)
                {
                    action?.Invoke();
                }

                return Task.CompletedTask;
            }

            bool ranToCompletion;
            lock (throttleControl)
            {
                ranToCompletion = throttleControl.DelayTask?.Status == TaskStatus.RanToCompletion;
                if (ranToCompletion)
                {
                    throttleControl.DelayTask = Task.Delay(delay_ms);
                    throttleControl.ContinuationTask = null;
                }
                else if (throttleControl.ContinuationTask == null && throttleControl.DelayTask != null)
                {
                    Task delayTask = throttleControl.DelayTask;
                    throttleControl.ContinuationTask = delayTask.ContinueWith(
                        (task, obj) =>
                        {
                            lock (lockObject ?? action ?? new object())
                            {
                                action?.Invoke();
                            }

                            lock (obj ?? new object())
                            {
                                var control = (FastThrottleControl)obj;
                                control.DelayTask = Task.Delay(delay_ms);
                                control.ContinuationTask = null;
                            }
                        },
                        throttleControl,
                        token ?? CancellationToken.None,
                        continuationOptions ?? TaskContinuationOptions.None,
                        taskScheduler ?? TaskScheduler.Default);

                    return throttleControl.ContinuationTask;
                }
            }

            if (ranToCompletion)
            {
                lock (lockObject ?? action)
                {
                    action?.Invoke();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Debounce an action
        /// </summary>
        /// <param name="action">Action to throttled</param>
        /// <param name="debounceControl">The previous task, create a object field for this ref, don't just pass a local field. This method will create and assign a task to this field when required.</param>
        /// <param name="lockObject">A lock object wrapped around the action</param>
        /// <param name="delay_ms">Throttling delay, i.e. how long is the interval between invoking the action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <param name="continuationOptions">Optional task continuation options when executing the action</param>
        /// <param name="taskScheduler">Optional task scheduler when executing the action</param>
        /// <returns></returns>
        public static Task Debounce(this Action action, ref object debounceControl, object lockObject = null, int delay_ms = 50, CancellationToken? token = null, TaskContinuationOptions? continuationOptions = null, TaskScheduler taskScheduler = null)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            if (!(debounceControl is DebounceControl controlObject))
            {
                controlObject = new DebounceControl();
                debounceControl = controlObject;
            }

            if (delay_ms <= 0)
            {
                controlObject.LatestTask = Task.Factory.StartNew(
                    () =>
                    {
                        lock (lockObject ?? action)
                        {
                            action?.Invoke();
                        }
                    },
                    token ?? CancellationToken.None,
                    TaskCreationOptions.None,
                    taskScheduler ?? TaskScheduler.Default);
                return controlObject.LatestTask;
            }

            var delayTask = Task.Delay(delay_ms);
            Task executionTask = delayTask.ContinueWith(
                (task, obj) =>
                {
                    var control = (DebounceControl)obj;
                    if (task != control.LatestTask)
                    {
                        return;
                    }

                    control.LatestTask = null;

                    lock (lockObject ?? action)
                    {
                        action?.Invoke();
                    }
                },
                controlObject,
                token ?? CancellationToken.None,
                continuationOptions ?? TaskContinuationOptions.None,
                taskScheduler ?? TaskScheduler.Default);

            controlObject.LatestTask = delayTask;

            return executionTask;
        }
    }
}
