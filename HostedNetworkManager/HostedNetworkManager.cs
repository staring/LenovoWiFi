﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using HostedNetworkManager.Wlan;

namespace HostedNetworkManager
{
    public class HostedNetworkManager : IDisposable
    {
        readonly object l = new object();

        private readonly WlanHandle _wlanHandle;
        private WlanHostedNetworkConnectionSettings _connectionSettings;
        private WlanHostedNetworkSecuritySettings _securitySettings;
        private WlanHostedNetworkState _hostedNetworkState;

        public HostedNetworkManager()
        {
            uint returnValue = 0;

            
            var enabled = IntPtr.Zero;
            var connectionSettings = IntPtr.Zero;
            var securitySettings = IntPtr.Zero;
            var status = IntPtr.Zero;

            try
            {
                Lock();

                uint negotiatedVersion;

                WlanHandle clientHandle;
                returnValue = WlanApi.WlanOpenHandle(
                    WlanApiVersion.Version,
                    IntPtr.Zero,
                    out negotiatedVersion,
                    out clientHandle);

                Utilities.ThrowOnError(returnValue);

                if (negotiatedVersion != (uint) WlanApiVersion.Version)
                {
                    throw new WlanException("Wlan API version negotiation failed.");
                }

                this._wlanHandle = clientHandle;

                WlanNotificationSource previousNotificationSource;
                returnValue = WlanApi.WlanRegisterNotification(
                    clientHandle,
                    WlanNotificationSource.HostedNetwork,
                    true,
                    OnNotification,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out previousNotificationSource);

                Utilities.ThrowOnError(returnValue);

                WlanHostedNetworkReason faileReason;
                returnValue = WlanApi.WlanHostedNetworkInitSettings(
                    clientHandle,
                    out faileReason,
                    IntPtr.Zero);

                Utilities.ThrowOnError(returnValue);

                uint dataSize;
                WlanOpcodeValueType opcodeValueType;
                returnValue = WlanApi.WlanHostedNetworkQueryProperty(
                    clientHandle,
                    WlanHostedNetworkOpcode.Enable,
                    out dataSize,
                    out enabled,
                    out opcodeValueType,
                    IntPtr.Zero);

                Utilities.ThrowOnError(returnValue);

                this.IsHostedNetworkAllowed = Convert.ToBoolean(Marshal.ReadInt32(enabled));

                returnValue = WlanApi.WlanHostedNetworkQueryProperty(
                    clientHandle,
                    WlanHostedNetworkOpcode.ConnectionSettings,
                    out dataSize,
                    out connectionSettings,
                    out opcodeValueType,
                    IntPtr.Zero);

                Utilities.ThrowOnError(returnValue);

                if (connectionSettings == IntPtr.Zero
                    || Marshal.SizeOf(typeof(WlanHostedNetworkConnectionSettings)) < dataSize)
                {
                    Utilities.ThrowOnError(13);
                }

                this._connectionSettings =
                    (WlanHostedNetworkConnectionSettings)
                        Marshal.PtrToStructure(connectionSettings, typeof (WlanHostedNetworkConnectionSettings));

                returnValue = WlanApi.WlanHostedNetworkQueryProperty(
                    clientHandle,
                    WlanHostedNetworkOpcode.SecuritySettings,
                    out dataSize,
                    out securitySettings,
                    out opcodeValueType,
                    IntPtr.Zero);

                Utilities.ThrowOnError(returnValue);

                this._securitySettings =
                    (WlanHostedNetworkSecuritySettings)
                        Marshal.PtrToStructure(securitySettings, typeof (WlanHostedNetworkSecuritySettings));

                returnValue = WlanApi.WlanHostedNetworkQueryStatus(
                        clientHandle,
                        out status,
                        IntPtr.Zero);

                Utilities.ThrowOnError(returnValue);

                WlanHostedNetworkStatus wlanHostedNetworkStatus =
                    (WlanHostedNetworkStatus)
                        Marshal.PtrToStructure(status, typeof (WlanHostedNetworkStatus));

                _hostedNetworkState = wlanHostedNetworkStatus.HostedNetworkState;
            }
            finally
            {
                if (returnValue != 0 && !this._wlanHandle.IsInvalid)
                {
                    this._wlanHandle.Dispose();
                }

                Unlock();

                if (enabled != IntPtr.Zero)
                {
                    WlanApi.WlanFreeMemory(enabled);
                }

                if (connectionSettings != IntPtr.Zero)
                {
                    WlanApi.WlanFreeMemory(connectionSettings);
                }

                if (securitySettings != IntPtr.Zero)
                {
                    WlanApi.WlanFreeMemory(securitySettings);
                }

                if (status != IntPtr.Zero)
                {
                    WlanApi.WlanFreeMemory(status);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
        protected virtual void Dispose(bool disposing)
        {
            if (_wlanHandle != null && !_wlanHandle.IsInvalid)
            {
                _wlanHandle.Dispose();
            }
        }

        public bool IsHostedNetworkAllowed { get; private set; }

        public event EventHandler HostedNetworkEnabled;
        public event EventHandler HostedNetworkStarted;
        public event EventHandler HostedNetworkStopped;
        public event EventHandler HostedNetworkDisabled;

        public event EventHandler<DeviceConnectedEventArgs> DeviceConnected;
        public event EventHandler<DeviceDisconnectedEventArgs> DeviceDisconnected;

        public string GetHostedNetworkName()
        {
            return this._connectionSettings.HostedNetworkSSID.SSID;
        }

        public void SetHostedNetworkName(string name)
        {
            var newSettings = new WlanHostedNetworkConnectionSettings
            {
                HostedNetworkSSID = new Dot11SSID {SSID = name, SSIDLength = (uint) name.Length},
                MaxNumberOfPeers = this._connectionSettings.MaxNumberOfPeers
            };

            WlanHostedNetworkReason faileReason;
            IntPtr newSettingPtr = Marshal.AllocHGlobal(Marshal.SizeOf(newSettings));
            Marshal.StructureToPtr(newSettings, newSettingPtr, false);

            Utilities.ThrowOnError(
                WlanApi.WlanHostedNetworkSetProperty(
                    this._wlanHandle,
                    WlanHostedNetworkOpcode.ConnectionSettings,
                    (uint)Marshal.SizeOf(newSettings),
                    newSettingPtr,
                    out faileReason,
                    IntPtr.Zero));
        }

        public string GetHostedNetworkKey()
        {
            string result = string.Empty;

            uint keyLength;
            IntPtr keyData;
            bool isPassPhrase;
            bool isPersistent;
            WlanHostedNetworkReason failReason;

            uint error = WlanApi.WlanHostedNetworkQuerySecondaryKey(
                this._wlanHandle,
                out keyLength,
                out keyData,
                out isPassPhrase,
                out isPersistent,
                out failReason,
                IntPtr.Zero);

            Utilities.ThrowOnError(error);

            if (keyLength != 0 && keyData != IntPtr.Zero)
            {
                result = Marshal.PtrToStringAnsi(keyData, (int)keyLength);
                WlanApi.WlanFreeMemory(keyData);
            }

            return result;
        }

        public void SetHostedNetworkKey(string key)
        {
            WlanHostedNetworkReason failReason;

            uint error = WlanApi.WlanHostedNetworkSetSecondaryKey(
                this._wlanHandle,
                (uint)key.Length + 1,
                key,
                true,
                true,
                out failReason,
                IntPtr.Zero);

            Utilities.ThrowOnError(error);
        }

        public void StartHostedNetwork()
        {
            if (_hostedNetworkState == WlanHostedNetworkState.Active)
            {
                return;
            }

            Lock();

            try
            {
                WlanHostedNetworkReason failReason;
                Utilities.ThrowOnError(
                    WlanApi.WlanHostedNetworkStartUsing(
                        this._wlanHandle,
                        out failReason,
                        IntPtr.Zero));
            }
            finally
            {
                Unlock();
            }
        }

        public void StopHostedNetwork()
        {
            if (_hostedNetworkState != WlanHostedNetworkState.Active)
            {
                return;
            }

            Lock();

            try
            {
                WlanHostedNetworkReason failReason;
                Utilities.ThrowOnError(
                    WlanApi.WlanHostedNetworkStartUsing(
                        this._wlanHandle,
                        out failReason,
                        IntPtr.Zero));
            }
            finally
            {
                Unlock();
            }
        }

        private void Lock()
        {
            Monitor.Enter(l);
        }

        private void Unlock()
        {
            Monitor.Exit(l);
        }

        private void OnNotification(WlanNotificationData notificationData, IntPtr context)
        {
            if (notificationData.NotificationSource == WlanNotificationSource.HostedNetwork)
            {
                switch (notificationData.NotificationCode)
                {
                    case WlanHostedNetworkNotificationCode.StateChange:
                        if (Marshal.SizeOf(typeof(WlanHostedNetworkStateChange)) == notificationData.DataSize
                            && notificationData.Data != IntPtr.Zero)
                        {
                            var stateChange =
                                (WlanHostedNetworkStateChange) Marshal.PtrToStructure(notificationData.Data,
                                    typeof (WlanHostedNetworkStateChange));

                            switch (stateChange.NewState)
                            {
                                case WlanHostedNetworkState.Active:
                                    OnHostedNetworkStarted();
                                    break;
                                case WlanHostedNetworkState.Idle:
                                    if (stateChange.OldState == WlanHostedNetworkState.Active)
                                    {
                                        OnHostedNetworkStopped();
                                    }
                                    else
                                    {
                                        OnHostedNetworkEnabled();
                                    }
                                    break;
                                case WlanHostedNetworkState.Unavailable:
                                    if (stateChange.OldState == WlanHostedNetworkState.Active)
                                    {
                                        OnHostedNetworkStopped();
                                    }
                                    OnHostedNetworkDisabled();
                                    break;
                            }
                        }
                        break;
                    case WlanHostedNetworkNotificationCode.PeerStateChange:
                        if (Marshal.SizeOf(typeof(WlanHostedNetworkDataPeerStateChange)) == notificationData.DataSize
                            && notificationData.Data != IntPtr.Zero)
                        {
                            var peerStateChange =
                                (WlanHostedNetworkDataPeerStateChange)
                                    Marshal.PtrToStructure(notificationData.Data,
                                        typeof (WlanHostedNetworkDataPeerStateChange));

                            if (peerStateChange.NewState.PeerAuthState == WlanHostedNetworkPeerAuthState.Authenticated)
                            {
                                OnDeviceConnected(peerStateChange.NewState);
                            }
                            if (peerStateChange.NewState.PeerAuthState == WlanHostedNetworkPeerAuthState.Invalid)
                            {
                                OnDeviceDisconnected(peerStateChange.NewState.PeerMacAddress);
                            }
                        }
                        break;
                }
            }
        }

        void OnHostedNetworkEnabled()
        {
            _hostedNetworkState = WlanHostedNetworkState.Idle;

            if (HostedNetworkEnabled != null)
            {
                HostedNetworkEnabled(this, EventArgs.Empty);
            }
        }

        void OnHostedNetworkStarted()
        {
            _hostedNetworkState = WlanHostedNetworkState.Active;

            if (HostedNetworkStarted != null)
            {
                HostedNetworkStarted(this, EventArgs.Empty);
            }
        }

        void OnHostedNetworkStopped()
        {
            _hostedNetworkState = WlanHostedNetworkState.Idle;

            if (HostedNetworkStopped != null)
            {
                HostedNetworkStopped(this, EventArgs.Empty);
            }
        }

        void OnHostedNetworkDisabled()
        {
            _hostedNetworkState = WlanHostedNetworkState.Unavailable;

            if (HostedNetworkDisabled != null)
            {
                HostedNetworkDisabled(this, EventArgs.Empty);
            }
        }

        void OnDeviceConnected(WlanHostedNetworkPeerState peerState)
        {
            if (DeviceConnected != null)
            {
                var args = new DeviceConnectedEventArgs(peerState.PeerMacAddress,
                    Convert.ToBoolean(peerState.PeerAuthState));
                DeviceConnected(this, args);
            }
        }

        void OnDeviceDisconnected(byte[] deviceMacAddress)
        {
            if (DeviceDisconnected != null)
            {
                var args = new DeviceDisconnectedEventArgs(deviceMacAddress);
                DeviceDisconnected(this, args);
            }
        }
    }
}
