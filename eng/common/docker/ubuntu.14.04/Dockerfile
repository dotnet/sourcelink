#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

FROM ubuntu:14.04

RUN apt-get update && \
    apt-get -qqy install \
        curl \
        unzip \
        gettext && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

RUN apt-get update && \
    apt-get -qqy install \
        libunwind8 \
        libkrb5-3 \
        libicu52 \
        liblttng-ust0 \
        libssl1.0.0 \
        zlib1g \
        libuuid1 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

