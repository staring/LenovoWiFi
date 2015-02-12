﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lenovo.WiFi.Client.Model
{
    public enum DeskbandCommand 
    { 
        ICS_Loading, 
        ICS_on, 
        ICS_off, 
        ICS_clientconnected, 
        ICS_error,
        DESKB_mouseenter, 
        DESKB_mouseleave,
        DESKB_lbuttonclick,
        DESKB_rbuttonclick,
        DESKB_exit,
        DESKB_handshake
    }

    public delegate int DeskbandDelegate(DeskbandCommand cmd);

    public class DeskbandPipe
    {
        private const string PIPENAME = "LenovoWiFi";
        private NamedPipeServerStream PipeSvrStream = null;
        private StreamReader SReader = null;
        private StreamWriter SWriter = null;

        private DeskbandDelegate m_delegate = null;

        public DeskbandPipe()
        { 
        }

        public void Dispose()
        {
            if (PipeStream.IsConnected)
            {
                PipeStream.Disconnect();
            }
        }

        protected NamedPipeServerStream PipeStream
        {

            get
            {
                if (null == PipeSvrStream)
                {
                    PipeSvrStream = new NamedPipeServerStream(PIPENAME, 
                        PipeDirection.InOut, 
                        1, 
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);
                }

                return PipeSvrStream;
            }
        }

        protected StreamReader Reader
        {
            get
            {
                if(null == SReader)
                {
                    SReader = new StreamReader(PipeStream, Encoding.Unicode);
                }

                return SReader;
            }
        }

        protected StreamWriter Writer
        {
            get
            {
                if (null == SWriter)
                {
                    SWriter = new StreamWriter(PipeStream, Encoding.Unicode);
                    SWriter.AutoFlush = true;
                }

                return SWriter;
            }
        }

        public void Disconnect()
        {
            if (PipeStream.IsConnected)
                PipeStream.Disconnect();
        }

        public bool Start()
        {
            if (null == m_delegate) return false;

            bool exit = false;

            //Task t = Task.Run(() =>
            //    {
            //        Thread.Sleep(10000);
            //        var uiDispatcher2 = App.Current.Dispatcher;
            //        uiDispatcher2.BeginInvoke(m_delegate, DeskbandCommand.DESKB_exit);

            //    });


            PipeStream.WaitForConnection();

            var uiDispatcher = App.Current.Dispatcher;

            while (true)
            {
                try
                {
                    var line = Reader.ReadLine();
                    switch (line)
                    {
                        case "mouseenter":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_mouseenter);
                            //m_delegate.BeginInvoke(DeskbandCommand.DESKB_mouseenter, null, null);
                            break;
                        case "mouseleave":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_mouseleave);
                            //m_delegate.BeginInvoke(DeskbandCommand.DESKB_mouseleave, null, null);
                            break;
                        case "lbuttonclick":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_lbuttonclick);
                            //m_delegate.BeginInvoke(DeskbandCommand.DESKB_lbuttonclick, null, null);
                            break;
                        case "rbuttonclick":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_rbuttonclick);
                            //m_delegate.BeginInvoke(DeskbandCommand.DESKB_rbuttonclick, null, null);
                            break;
                        case "exit":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_exit);
                            //m_delegate.BeginInvoke(DeskbandCommand.DESKB_exit, null, null);
                            exit = true;
                            break;
                        case "error":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.ICS_error);
                            break;
                        case "handshake":
                            uiDispatcher.BeginInvoke(m_delegate, DeskbandCommand.DESKB_handshake);
                            break;
                        default:
                            //log
                            break;
                    }

                    if (exit)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //log ex.string.
                    return false;
                }
                //finally
                //{
                //    if (PipeStream.IsConnected)
                //    {
                //        PipeStream.Disconnect();
                //    }
                //}
            }

            return true;
        }

        public void SetDeskbandDelegate(DeskbandDelegate del)
        {
            m_delegate = new DeskbandDelegate(del);
        }

        public bool SendCommandToDeskband(DeskbandCommand cmd)
        {

            try
            {
                if( PipeStream.IsConnected )
                {
                    switch(cmd)
                    {
                        case DeskbandCommand.ICS_Loading:
                            Writer.WriteLine("ics_loading");

                            break;
                        case DeskbandCommand.ICS_on:
                            Writer.WriteLine("ics_on");

                            break;
                        case DeskbandCommand.ICS_off:
                            Writer.WriteLine("ics_off");

                            break;
                        case DeskbandCommand.ICS_clientconnected:
                            Writer.WriteLine("ics_clientconnected");

                            break;
                        default:
                            break;
                    }
                }
            }catch(Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}
