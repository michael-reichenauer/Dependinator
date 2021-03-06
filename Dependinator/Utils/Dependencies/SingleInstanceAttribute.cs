﻿using System;


namespace Dependinator.Utils.Dependencies
{
    /// <summary>
    ///     Attribute used to mark types that should be registered as a single instance in
    ///     dependency injection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
    public sealed class SingleInstanceAttribute : Attribute
    {
    }
}
