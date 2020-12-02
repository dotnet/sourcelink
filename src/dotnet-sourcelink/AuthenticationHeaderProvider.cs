// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Net.Http.Headers;
using System.Text;

namespace Microsoft.SourceLink.Tools
{
    internal interface IAuthenticationHeaderValueProvider
    {
        AuthenticationHeaderValue GetValue();
    }

    internal sealed class BasicAuthenticationHeaderValueProvider : IAuthenticationHeaderValueProvider
    {
        private readonly string _username;
        private readonly string _password;
        private readonly Encoding _encoding;

        public BasicAuthenticationHeaderValueProvider(string username, string password, Encoding encoding)
        {
            _username = username;
            _password = password;
            _encoding = encoding;
        }

        public AuthenticationHeaderValue GetValue()
            => new("Basic", Convert.ToBase64String(_encoding.GetBytes($"{_username}:{_password}")));
    }
}
