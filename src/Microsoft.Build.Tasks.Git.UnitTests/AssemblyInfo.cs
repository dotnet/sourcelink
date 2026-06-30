// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Xunit;

// LocateRepositoryTests temporarily changes the process current working directory to validate that
// repository discovery resolves paths against the task's project directory rather than the process CWD.
// Disable test parallelization in this assembly so that the CWD mutation can't race with other tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
