#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

FROM microsoft/dotnet-buildtools-prereqs:ubuntu-18.04-f90bc20-20180320154721

RUN apt-get update && \
    apt-get -qqy install \
        curl \
        libcurl4 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*