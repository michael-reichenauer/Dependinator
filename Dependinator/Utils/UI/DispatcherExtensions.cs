﻿namespace System.Windows.Threading
{
    internal static class DispatcherExtensions
    {
        public static void BeginInvoke(
            this Dispatcher dispatcher, Action action) =>
            dispatcher.BeginInvoke(DispatcherPriority.Normal, action);


        public static void BeginInvokeBackground(
            this Dispatcher dispatcher, Action action) =>
            dispatcher.BeginInvoke(DispatcherPriority.Background, action);


        public static void BeginInvokeBackground<T>(
            this Dispatcher dispatcher, Action<T> action, T arg) =>
            dispatcher.BeginInvoke(DispatcherPriority.Background, action, arg);


        public static void InvokeBackground(
            this Dispatcher dispatcher, Action action) =>
            dispatcher.Invoke(DispatcherPriority.Background, action);


        public static void InvokeBackground<T1>(
            this Dispatcher dispatcher, Action<T1> action, T1 arg1) =>
            dispatcher.Invoke(DispatcherPriority.Background, action, arg1);


        public static void InvokeBackground<T1, T2>(
            this Dispatcher dispatcher, Action<T1, T2> action, T1 arg1, T2 arg2) =>
            dispatcher.Invoke(DispatcherPriority.Background, action, arg1, arg2);


        public static void InvokeBackground<T1, T2, T3>(
            this Dispatcher dispatcher, Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3) =>
            dispatcher.Invoke(DispatcherPriority.Background, action, arg1, arg2, arg3);


        public static void Invoke<T>(this Dispatcher dispatcher, Action<T> action, T arg) =>
            dispatcher.Invoke(DispatcherPriority.Normal, action, arg);
    }
}
