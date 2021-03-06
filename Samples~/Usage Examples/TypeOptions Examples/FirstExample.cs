﻿namespace TypeReferences.Demo.TypeOptions_Examples
{
    using System;
    using UnityEngine;
    using Utils;

    public class FirstExample : TypeReferenceExample
    {
        [InfoBox("Example usage of TypeReference. You can choose between several types and initialize " +
                 "the class dynamically.")]
        [Inherits(typeof(IGreetingLogger))]
        public TypeReference GreetingLoggerType;

        [Button]
        public void LogGreeting()
        {
            if (GreetingLoggerType.Type == null)
            {
                Debug.LogWarning("No greeting logger was specified.");
            }
            else
            {
                var greetingLogger = Activator.CreateInstance(GreetingLoggerType) as IGreetingLogger;
                greetingLogger.LogGreeting();
            }
        }
    }
}
