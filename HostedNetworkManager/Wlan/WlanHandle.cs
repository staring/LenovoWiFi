﻿using System;
using System.Runtime.ConstrainedExecution;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;

namespace HostedNetworkManager.Wlan
{
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    sealed class WlanHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal WlanHandle() : base(true)
        {
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return Convert.ToBoolean(WlanApi.WlanCloseHandle(handle, IntPtr.Zero));
        }
    }
}