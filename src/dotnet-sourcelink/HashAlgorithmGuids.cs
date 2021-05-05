// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;

namespace Microsoft.SourceLink.Tools
{
    internal static class HashAlgorithmGuids
    {
        public static readonly Guid MD5 = new Guid("406ea660-64cf-4c82-b6f0-42d48172a799");
        public static readonly Guid Sha1 = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");
        public static readonly Guid Sha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");
        
        public static HashAlgorithmName? TryGetName(Guid guid)
        {
            if (guid == MD5) return new HashAlgorithmName("MD5");
            if (guid == Sha1) return new HashAlgorithmName("SHA1");
            if (guid == Sha256) return new HashAlgorithmName("SHA256");
            return null;
        }

        public static HashAlgorithmName GetName(Guid guid)
            => TryGetName(guid) ?? throw new CryptographicException("unknown HashAlgorithm " + guid);
    }
}
