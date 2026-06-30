// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

namespace TestUtilities
{
    public sealed class MockEngine : IBuildEngine4
    {
        private StringBuilder _log = new StringBuilder();
        public MessageImportance MinimumMessageImportance = MessageImportance.Low;

        private readonly Dictionary<object, object?> _registeredTaskObjects = new();

        public string Log
        {
            set { _log = new StringBuilder(value); }
            get { return _log.ToString(); }
        }

        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            _log.Append("ERROR ");
            _log.Append(eventArgs.Code);
            _log.Append(": ");
            _log.Append(eventArgs.Message);
            _log.AppendLine();
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            _log.Append("WARNING ");
            _log.Append(eventArgs.Code);
            _log.Append(": ");
            _log.Append(eventArgs.Message);
            _log.AppendLine();
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            _log.AppendLine(eventArgs.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            _log.AppendLine(eventArgs.Message);
        }

        public string ProjectFileOfTaskNode => "";
        public int ColumnNumberOfTaskNode => 0;
        public int LineNumberOfTaskNode => 0;
        public bool ContinueOnError => true;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            => throw new NotImplementedException();

        // IBuildEngine2
        public bool IsRunningMultipleNodes => false;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
            => throw new NotImplementedException();

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
            => throw new NotImplementedException();

        // IBuildEngine3
        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
            => throw new NotImplementedException();

        public void Yield() { }

        public void Reacquire() { }

        // IBuildEngine4
        public void RegisterTaskObject(object key, object? obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
            => _registeredTaskObjects[key] = obj;

        public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            => _registeredTaskObjects.TryGetValue(key, out var value) ? value : null;

        public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            if (_registeredTaskObjects.TryGetValue(key, out var value))
            {
                _registeredTaskObjects.Remove(key);
                return value;
            }

            return null;
        }
    }
}
