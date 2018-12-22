#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

FROM microsoft/dotnet-buildtools-prereqs:opensuse-42.3-d46ee12-20180327014902

RUN zypper -n install binutils \
              tar \
              ncurses-utils \
              curl && \
    zypper clean -a

# Dependencies of CoreCLR and CoreFX.

RUN zypper -n install --force-resolution \
                      libunwind \
                      libicu \
                      lttng-ust \
                      libuuid1 \
                      libopenssl1_0_0 \
                      libcurl4 \
                      krb5 && \
    zypper clean -a