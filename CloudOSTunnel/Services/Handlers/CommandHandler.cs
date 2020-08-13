﻿using CloudOSTunnel.Clients;
using FxSsh;
using FxSsh.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CloudOSTunnel.Services.Handlers
{
    public class CommandHandler : TunnelHandler
    {
        string consoleOutput = "";
        object _sender;
        CommandRequestedArgs _session;
        string command = "";
        VMWareClient _client;
        static string mode;

        string _type = "";

        public CommandHandler(string type, VMWareClient client)
        {
            _type = type;
            _client = client;
        }

        public override event EventHandler<byte[]> DataReceived;
        public override event EventHandler EofReceived;
        public override event EventHandler<uint> CloseReceived;

        public List<byte> cachedFile = new List<byte>();
        public List<byte> currentCommand = new List<byte>();
        public bool PipeToExistingProcess = false;
        public long PID;

        public override void OnData(byte[] data, string type = "")
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            if (data.Length == 1)
            {
                var loop = 0;
                System.Diagnostics.Debug.WriteLine("Received Write");
                if (data.Count() == 1)
                {
                    if (data.Last() == 13)
                    {
                        System.Diagnostics.Debug.WriteLine("Detected execution request");
                    }
                    else if (data.Last() == 8)
                    {
                        System.Diagnostics.Debug.WriteLine("Detected Backspace");

                        if (currentCommand.Count() > 0)
                        {
                            currentCommand.RemoveAt(currentCommand.Count() - 1);
                        }
                    }
                }
                currentCommand.AddRange(data);
                DataReceived?.Invoke(this, Encoding.ASCII.GetBytes("\0"));
                System.Diagnostics.Debug.WriteLine("Current Command: " + System.Text.Encoding.UTF8.GetString(currentCommand.ToArray()));
            }
            else
            {

                Console.WriteLine("Received: " + System.Text.Encoding.UTF8.GetString(data));
                //used to allow contiued communication
                bool isComplete = false;
                var result = _client.ExecuteLinuxCommand(System.Text.Encoding.UTF8.GetString(data), out isComplete, out PID);
                System.Diagnostics.Debug.WriteLine("Got result: " + result);

                var finalBytes = Encoding.ASCII.GetBytes(result);

                /*for (var i = 0; i < finalBytes.Length; i += Session.LocalChannelDataPacketSize)
                {
                    var size = finalBytes.Skip(i).Take(i + Session.LocalChannelDataPacketSize <= finalBytes.Length ? Session.LocalChannelDataPacketSize : finalBytes.Length - i).ToArray();
                    DataReceived?.Invoke(this, size);

                }*/

                var splitResult = result.Split("\r\n");//.SelectMany(r => r.Split('\r')).SelectMany(r => r.Split('\n'));

                foreach (var section in splitResult)
                {
                    if (section != "")
                    {
                        Console.WriteLine("Replying: " + section);
                        DataReceived?.Invoke(this, Encoding.ASCII.GetBytes(section));
                    }
                }
                // DataReceived?.Invoke(this, Encoding.ASCII.GetBytes(""));
                /*var totalBytes = Encoding.ASCII.GetBytes(result);
                foreach*/
                // DataReceived?.Invoke(this, totalBytes);
                //EofReceived?.Invoke(this, EventArgs.Empty);
                if (isComplete)
                {

                    if (stopwatch.ElapsedMilliseconds < 2500)
                    {
                        Console.WriteLine("Add mandatory wait...");
                        Thread.Sleep(2500 - (int)stopwatch.ElapsedMilliseconds);
                    }
                    Console.WriteLine("Sending Closed Received.");
                    CloseReceived?.Invoke(this, 0);
                }
                else
                {
                    Console.WriteLine("PROCESS WAS NOT COMPLETED!");
                }
            }
            Console.WriteLine("Full request took : " + stopwatch.ElapsedMilliseconds);
            return;
        }

        public override void OnClose()
        {

            System.Diagnostics.Debug.WriteLine("Detected close");
            //CloseReceived?.Invoke(this, 0);
        }

        public override void Print(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        public override void SetType(string type)
        {
            _type = type;
        }

        public override void OnEndOfFile()
        {
            System.Diagnostics.Debug.WriteLine("Detected end of file");
        }
    }
}
