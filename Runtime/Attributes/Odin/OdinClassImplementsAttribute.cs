﻿namespace TypeReferences.Odin
{
    using System;
    using System.Linq;

    /// <summary>
    /// Constraint that allows selection of classes that implement a specific interface
    /// when selecting a <see cref="ClassTypeReference"/> with the Unity inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OdinClassImplementsAttribute : ClassTypeConstraintAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OdinClassImplementsAttribute"/> class.
        /// </summary>
        /// <param name="interfaceType">Type of interface that selectable classes must implement.</param>
        public OdinClassImplementsAttribute(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }

        /// <summary>
        /// Gets the type of interface that selectable classes must implement.
        /// </summary>
        public Type InterfaceType { get; private set; }

        /// <inheritdoc/>
        public override bool IsConstraintSatisfied(Type type)
        {
            if (!base.IsConstraintSatisfied(type))
                return false;

            bool specifiedTypeIsInCollection = type.GetInterfaces()
                .Any(interfaceType => interfaceType == InterfaceType);

            return specifiedTypeIsInCollection;
        }
    }
}